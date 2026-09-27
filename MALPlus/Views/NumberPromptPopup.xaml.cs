namespace MALPlus.Views;

/// <summary>
/// Modal numeric input, used for the fields the old client edited by hand (watched episodes and
/// chapters). Returns the typed value through <see cref="Result"/> or null when cancelled.
/// </summary>
[QueryProperty(nameof(InitialValue), "value")]
[QueryProperty(nameof(PromptTitle), "title")]
[QueryProperty(nameof(Hint), "hint")]
public partial class NumberPromptPopup : ContentPage
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(NumberPromptPopup), "Set value");

    public static readonly BindableProperty ResultProperty =
        BindableProperty.Create(nameof(Result), typeof(int?), typeof(NumberPromptPopup), null);

    private string _initialValue = string.Empty;
    private string _promptTitle = "Set value";
    private string _hint = string.Empty;

    private readonly TaskCompletionSource<int?> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public NumberPromptPopup()
    {
        InitializeComponent();
    }

    /// <summary>
    /// PushModalAsync completes when the push animation ends, not when the modal is dismissed, so
    /// the result has to be awaited through here instead of through the navigation task.
    /// </summary>
    public Task<int?> WaitForResultAsync()
    {
        return _completion.Task;
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public int? Result
    {
        get => (int?)GetValue(ResultProperty);
        set => SetValue(ResultProperty, value);
    }

    public string InitialValue
    {
        get => _initialValue;
        set
        {
            _initialValue = value;
            if (ValueEntry != null)
                ValueEntry.Text = value;
        }
    }

    public string PromptTitle
    {
        get => _promptTitle;
        set
        {
            _promptTitle = value;
            if (Title != null)
                Title = value;
        }
    }

    public string Hint
    {
        get => _hint;
        set
        {
            _hint = value;
            if (HintLabel != null)
            {
                HintLabel.Text = value;
                HintLabel.IsVisible = !string.IsNullOrWhiteSpace(value);
            }
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (string.IsNullOrEmpty(ValueEntry.Text))
            ValueEntry.Text = _initialValue;
        HintLabel.Text = _hint;
        HintLabel.IsVisible = !string.IsNullOrWhiteSpace(_hint);
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        Result = null;
        _completion.TrySetResult(null);
        await Close();
    }

    private async void OnConfirmClicked(object sender, EventArgs e)
    {
        var text = ValueEntry.Text?.Trim();
        if (int.TryParse(text, out var value) && value >= 0)
        {
            Result = value;
            _completion.TrySetResult(value);
            await Close();
            return;
        }

        ErrorLabel.Text = "Enter a whole number.";
        ErrorLabel.IsVisible = true;
    }

    private async Task Close()
    {
        try
        {
            ValueEntry?.Unfocus();
        }
        catch (Exception)
        {
            // the entry may already be gone
        }

        await Navigation.PopModalAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _completion.TrySetResult(null);
        return base.OnBackButtonPressed();
    }
}
