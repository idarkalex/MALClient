using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;

namespace MALPlus.Controls;

/// <summary>
///     The one vertical-poster grid for the whole app: anime list, manga list, search results,
///     Related, Discover, ... They all used to hand-roll a CollectionView + GridItemsLayout and
///     hardcode the cell height, which is why the spacing drifted apart between pages and why
///     cards ended up with a visible gap next to the artwork.
///
///     Here the cell width comes from the width the control actually gets, and the cell height is
///     DERIVED from it through <see cref="PosterAspect" />. A poster therefore always fills its cell
///     exactly, on any screen, and rows are flush with a single <see cref="Spacing" /> between them.
/// </summary>
public partial class AnimePosterGrid : ContentView
{
    /// <summary>Poster aspect (width / height). MAL covers are 2:3.</summary>
    public const double PosterAspect = 2d / 3d;

    /// <summary>
    ///     The single spacing value used by every poster grid in the app. Zero: posters sit edge to
    ///     edge with no gutter and no frame, which is the look that was signed off.
    /// </summary>
    public const double DefaultSpacing = 0d;

    /// <summary>Cards per row. The app is designed around three.</summary>
    public const int DefaultSpan = 3;

    /// <summary>
    ///     Card height the app has always used. It is deliberately NOT derived from the cell width:
    ///     the posters are AspectFill, so the cell crops instead of letterboxing, and 195 is the
    ///     proportion that was signed off.
    /// </summary>
    public const double DefaultCellHeight = 195d;

    private bool _spanApplied;
    private double _lastWidth = -1;

    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable), typeof(AnimePosterGrid),
            default(IEnumerable), propertyChanged: OnItemsSourceChanged);

    private static void OnItemsSourceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var grid = (AnimePosterGrid)bindable;
        if (oldValue is INotifyCollectionChanged oldNotifier)
            oldNotifier.CollectionChanged -= grid.OnItemsSourceCollectionChanged;
        if (newValue is INotifyCollectionChanged newNotifier)
            newNotifier.CollectionChanged += grid.OnItemsSourceCollectionChanged;
        grid.Inner.ItemsSource = newValue as IEnumerable;
        grid.ApplyLayout(force: true);
    }

    /// <summary>
    ///     A fixed-height grid lives inside a ScrollView, so it has to grow as items stream in.
    /// </summary>
    private void OnItemsSourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        => ApplyLayout(force: true);

    public static readonly BindableProperty SpanProperty =
        BindableProperty.Create(nameof(Span), typeof(int), typeof(AnimePosterGrid), 0,
            propertyChanged: (b, o, n) => ((AnimePosterGrid)b).ApplyLayout(force: true));

    /// <summary>Gap between two cards, and half of it on each outer edge.</summary>
    public static readonly BindableProperty SpacingProperty =
        BindableProperty.Create(nameof(Spacing), typeof(double), typeof(AnimePosterGrid), DefaultSpacing,
            propertyChanged: (b, o, n) => ((AnimePosterGrid)b).ApplyLayout(force: true));

    /// <summary>Optional cell content. Null uses the standard library cell.</summary>
    public static readonly BindableProperty CellTemplateProperty =
        BindableProperty.Create(nameof(CellTemplate), typeof(DataTemplate), typeof(AnimePosterGrid),
            default(DataTemplate), propertyChanged: (b, o, n) => ((AnimePosterGrid)b).ApplyLayout(force: true));

    /// <summary>Width of a single card, derived from the available width.</summary>
    public static readonly BindableProperty CellWidthProperty =
        BindableProperty.Create(nameof(CellWidth), typeof(double), typeof(AnimePosterGrid), 0d);

    /// <summary>Height of a single card, derived from <see cref="CellWidth" />.</summary>
    public static readonly BindableProperty CellHeightProperty =
        BindableProperty.Create(nameof(CellHeight), typeof(double), typeof(AnimePosterGrid), 0d);

    /// <summary>Vertical pitch of one row: card height plus its margin.</summary>
    public static readonly BindableProperty RowHeightProperty =
        BindableProperty.Create(nameof(RowHeight), typeof(double), typeof(AnimePosterGrid), 0d);

    /// <summary>Half the spacing, so the row keeps the same gutter on its outer edges.</summary>
    public static readonly BindableProperty CellMarginProperty =
        BindableProperty.Create(nameof(CellMargin), typeof(Thickness), typeof(AnimePosterGrid), new Thickness(0));

    /// <summary>
    ///     Set when the grid lives inside a ScrollView and therefore cannot take its height from a
    ///     scroller: the control measures itself instead of letting the host guess.
    /// </summary>
    public static readonly BindableProperty FixedHeightProperty =
        BindableProperty.Create(nameof(FixedHeight), typeof(bool), typeof(AnimePosterGrid), false,
            propertyChanged: (b, o, n) => ((AnimePosterGrid)b).ApplyLayout(force: true));

    /// <summary>Raised with the bound item when a card is tapped.</summary>
    public event EventHandler<object> ItemTapped;

    public IEnumerable ItemsSource
    {
        get => (IEnumerable)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>0 means "pick from the width" (3 columns on a phone, more on a tablet).</summary>
    public int Span
    {
        get => (int)GetValue(SpanProperty);
        set => SetValue(SpanProperty, value);
    }

    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    public DataTemplate CellTemplate
    {
        get => (DataTemplate)GetValue(CellTemplateProperty);
        set => SetValue(CellTemplateProperty, value);
    }

    public double CellWidth
    {
        get => (double)GetValue(CellWidthProperty);
        private set => SetValue(CellWidthProperty, value);
    }

    public double CellHeight
    {
        get => (double)GetValue(CellHeightProperty);
        private set => SetValue(CellHeightProperty, value);
    }

    public double RowHeight
    {
        get => (double)GetValue(RowHeightProperty);
        private set => SetValue(RowHeightProperty, value);
    }

    public Thickness CellMargin
    {
        get => (Thickness)GetValue(CellMarginProperty);
        private set => SetValue(CellMarginProperty, value);
    }

    public bool FixedHeight
    {
        get => (bool)GetValue(FixedHeightProperty);
        set => SetValue(FixedHeightProperty, value);
    }

    public AnimePosterGrid()
    {
        InitializeComponent();
        ApplyLayout(force: true);
    }

    /// <summary>
    ///     The inner collection, for the few hosts that need list-level features the grid does not
    ///     model (infinite scroll, selection). Layout and sizing stay owned by the control.
    /// </summary>
    public CollectionView Inner => ItemsView;

    /// <summary>
    ///     Height a poster grid of <paramref name="count" /> items needs when it cannot scroll.
    /// </summary>
    public static double CalculateHeight(int count, int span, double rowHeight)
    {
        if (count <= 0 || rowHeight <= 0)
            return 0;
        var columns = span > 0 ? span : DefaultSpan;
        var rows = (int)Math.Ceiling(count / (double)columns);
        return rows * rowHeight;
    }

    public int EffectiveSpan(double availableWidth)
    {
        if (Span > 0)
            return Span;
        return DefaultSpan;
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0 || Math.Abs(width - _lastWidth) < 0.5)
            return;
        _lastWidth = width;
        ApplyLayout(force: true);
    }

    private void ApplyLayout(bool force)
    {
        if (!force && _spanApplied)
            return;

        var available = Width > 0 ? Width : (Window?.Width ?? 0);
        if (available <= 0)
            return;

        var span = EffectiveSpan(available);
        if (!_spanApplied || (ItemsView.ItemsLayout is GridItemsLayout grid && grid.Span != span))
        {
            // The gutter lives in the layout, not in a card margin: a margin insets the artwork on
            // all four sides and shows up as a dark frame around every poster.
            ItemsView.ItemsLayout = new GridItemsLayout(span, ItemsLayoutOrientation.Vertical)
            {
                HorizontalItemSpacing = Spacing,
                VerticalItemSpacing = Spacing
            };
            _spanApplied = true;
        }

        var slot = (available - (span - 1) * Spacing) / span;
        if (slot <= 0)
            return;

        CellMargin = new Thickness(0);
        CellHeight = DefaultCellHeight;
        RowHeight = CellHeight + Spacing;

        // The card template reads these through x:Reference, so no page has to repeat the numbers.
        ItemsView.ItemTemplate = CellTemplate ?? (DataTemplate)Root.Resources["DefaultCellTemplate"];

        if (FixedHeight)
        {
            var count = (ItemsView.ItemsSource as ICollection)?.Count
                        ?? (ItemsView.ItemsSource as IEnumerable)?.Cast<object>().Count()
                        ?? 0;
            HeightRequest = CalculateHeight(count, span, RowHeight);
        }
    }

    private void OnCellTapped(object sender, TappedEventArgs e)
    {
        if (sender is View view && view.BindingContext != null)
            ItemTapped?.Invoke(this, view.BindingContext);
    }
}
