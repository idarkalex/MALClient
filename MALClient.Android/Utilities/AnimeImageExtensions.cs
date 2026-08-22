using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Android.Views;
using Android.Widget;
using FFImageLoading;
using FFImageLoading.Transformations;
using FFImageLoading.Views;
using FFImageLoading.Work;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;

namespace MALClient.Android
{
    public static class AnimeImageExtensions
    {
        //private static readonly Dictionary<View, IScheduledWork> TasksDictionary = new Dictionary<View, IScheduledWork>();
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> LoadedImgs = new System.Collections.Concurrent.ConcurrentDictionary<string, byte>();
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> FailedImgs = new System.Collections.Concurrent.ConcurrentDictionary<string, byte>();

        #region AnimeInto

        public static string GetImgUrl(string originUrl)
        {
            if (string.IsNullOrEmpty(originUrl))
                return null;

            if (Settings.PullHigherQualityImages && !FailedImgs.ContainsKey(originUrl))
            {
                var pos = originUrl.IndexOf(".jpg", StringComparison.InvariantCulture);
                if (pos == -1)
                    pos = originUrl.IndexOf(".webp", StringComparison.InvariantCulture);

                if (pos != -1)
                {
                    var stem = originUrl.Substring(0, pos);
                    if (stem.EndsWith("l") || stem.EndsWith("m") || stem.EndsWith("s"))
                        return originUrl;
                    return originUrl.Insert(pos, "l");
                }
                return originUrl;
            }
            return originUrl;
        }

        public static bool AnimeIntoIfLoaded(this ImageView image, string originUrl, ITransformation transformation = null)
        {
            var url = GetImgUrl(originUrl);
            if (LoadedImgs.ContainsKey(url))
            {
                LoadImage(image, originUrl, url, true, null, transformation);
                return true;
            }
            return false;
        }

        public static void AnimeInto(this ImageView image, string originUrl, View loader = null, ITransformation transformation = null)
        {
            var url = GetImgUrl(originUrl);
            LoadImage(image, originUrl, url, LoadedImgs.ContainsKey(url), loader, transformation);
        }

        private static void LoadImage(ImageView image, string originUrl, string targetUrl,
            bool? imgLoaded, View loader, ITransformation transformation = null)
        {
            //if (TasksDictionary.TryGetValue(image, out var task))
            //{
            //    Debug.WriteLine("Cancelled");
            //    task.Cancel();
            //    TasksDictionary.Remove(image);
            //}

            try
            {
                if (string.IsNullOrEmpty(targetUrl) || string.IsNullOrEmpty(originUrl))
                    return;

                image.SetImageResource(global::Android.Resource.Color.Transparent);
                var work = ImageService.Instance.LoadUrl(targetUrl).DownSampleInDip(0, 320);
                if (transformation != null)
                    work = work.Transform(transformation);
                if (loader != null)
                    work.Finish(scheduledWork => loader.Visibility = ViewStates.Gone);
                if (imgLoaded != true && !LoadedImgs.ContainsKey(targetUrl))
                {
                    image.Visibility = ViewStates.Invisible;
                    work = work.Success(image.AnimateFadeIn);
                    LoadedImgs[targetUrl] = 0;
                }
                else
                {
                    if (image.Tag == null && !LoadedImgs.ContainsKey(targetUrl))
                    {
                        work = work.Success(image.AnimateFadeIn);
                    }
                    else
                        image.Visibility = ViewStates.Visible;
                }
                image.Tag = originUrl;
                //we can fallback to lower quality image
                if (!originUrl.Equals(targetUrl))
                {
                    work.Error(exception =>
                    {
                        if (!ResourceLocator.ConnectionInfoProvider.HasInternetConnection)
                        {
                            image.SetImageResource(global::Android.Resource.Color.Transparent);
                            return;
                        }
                        ResourceLocator.ConnectionInfoProvider.Init();
                        var img = (string)image.Tag;
                        var fallbackWork = ImageService.Instance.LoadUrl(img).FadeAnimation(false);
                        if (transformation != null)
                            fallbackWork = fallbackWork.Transform(transformation);
                        fallbackWork.Into(image);
                        FailedImgs[targetUrl] = 0;
                        LoadedImgs[img] = 0;
                    });
                }
                if (transformation == null)
                    work.FadeAnimation(false).Into(image);
                else
                    work.FadeAnimation(false).Transform(transformation).Into(image);

            }
            catch (Exception)
            {
                //BUG Throws aggregate when hostname wasn't reseolved
            }
        }

        //private static void OnWorkFinished(IScheduledWork scheduledWork)
        //{
        //    TasksDictionary.Remove(TasksDictionary.First(pair => pair.Value == scheduledWork).Key);
        //}

        #endregion

        public static bool IntoIfLoaded(this ImageView image, string originUrl, ITransformation transformation = null,
            Action<ImageView> onCompleted = null, int? maxHeight = null)
        {
            if (LoadedImgs.ContainsKey(originUrl))
            {
                LoadImage(image, originUrl, transformation, onCompleted, maxHeight, true);
                return true;
            }
            return false;
        }

        public static void Into(this ImageView image, string originUrl, ITransformation transformation = null, Action<ImageView> onCompleted = null, int? maxHeight = null)
        {
            LoadImage(image, originUrl, transformation, onCompleted, maxHeight, null);
        }

        public static void LoadImage(this ImageView image, string originUrl, ITransformation transformation,
            Action<ImageView> onCompleted, int? maxHeight, bool? imgLoaded)
        {
            if (string.IsNullOrEmpty(originUrl) || image == null)
                return;

            if (image.Tag != null && (string)image.Tag == originUrl)
            {
                image.Visibility = ViewStates.Visible;
                return;
            }

            image.Visibility = ViewStates.Invisible;
            try
            {
                var work = ImageService.Instance.LoadUrl(originUrl);
                if (maxHeight != null)
                    work = work.DownSampleInDip(0, maxHeight.Value);

                if (imgLoaded != true && !LoadedImgs.ContainsKey(originUrl))
                {
                    image.Visibility = ViewStates.Invisible;
                    work = work.Success(() =>
                    {
                        image.AnimateFadeIn();
                        onCompleted?.Invoke(image);
                    });
                    LoadedImgs[originUrl] = 0;
                }
                else
                {
                    if (image.Tag == null)
                    {
                        image.Visibility = ViewStates.Invisible;
                        work = work.Success(() =>
                        {
                            image.AnimateFadeIn();
                            onCompleted?.Invoke(image);
                        });
                    }
                    else
                    {
                        image.Visibility = ViewStates.Visible;
                        if (onCompleted != null)
                        {
                            work = work.Success(() =>
                            {
                                onCompleted.Invoke(image);
                            });
                        }
                    }
                }
                image.Tag = originUrl;
                if (transformation == null)
                    work.FadeAnimation(false).Delay(50).Into(image);
                else
                    work.FadeAnimation(false).Delay(50).Transform(transformation).Into(image);
            }
            catch (Exception)
            {
                //BUG Throws aggregate when hostname wasn't reseolved
            }
        }

        public static ImageView.ScaleType HandleScaling(this ImageView image, float threshold = .4f)
        {
            try
            {
                var bounds = image.Drawable.Bounds;
                if (bounds.Right == 0 || image.Width == 0)
                {
                    image.SetScaleType(ImageView.ScaleType.CenterCrop);
                    return ImageView.ScaleType.CenterCrop;
                }
                if (
                    Math.Abs(image.Height / (float)image.Width -
                             bounds.Bottom / (float)bounds.Right) > threshold)
                {
                    image.SetScaleType(ImageView.ScaleType.FitCenter);
                    return ImageView.ScaleType.FitCenter;

                }
                else
                {
                    image.SetScaleType(ImageView.ScaleType.CenterCrop);
                    return ImageView.ScaleType.CenterCrop;
                }
            }
            catch (Exception)
            {
                //somehow called from non ui thread
                return ImageView.ScaleType.CenterCrop;
            }
        }

        public static async void IntoWithTask(this ImageView image, Task<string> originUrlTask,
            ITransformation transformation = null)
        {
            try
            {
                var originUrl = await originUrlTask;

                Into(image, originUrl, transformation);
            }
            catch (Exception)
            {
                //BUG Throws aggregate when hostname wasn't reseolved
            }
        }

        public static void NotifyCacheWiped()
        {
            LoadedImgs.Clear();
        }
    }
}
