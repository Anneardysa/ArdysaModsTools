/*
 * Copyright (C) 2026 Ardysa
 * Licensed under GPL v3
 *
 * AnimationHelper — Pure WPF Storyboard-based animation engine.
 * No third-party dependencies. All animations use DoubleAnimation with easing.
 *
 * Usage:
 *   await AnimationHelper.FadeInAsync(element, 300);
 *   await AnimationHelper.SlideDownAsync(panel, 200, 80);
 */

using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ArdysaModsTools.Installer.Helpers
{
    public static class AnimationHelper
    {
        private static readonly QuadraticEase EaseOut = new() { EasingMode = EasingMode.EaseOut };
        private static readonly QuadraticEase EaseInOut = new() { EasingMode = EasingMode.EaseInOut };
        private static readonly CubicEase BounceEase = new() { EasingMode = EasingMode.EaseOut };


        public static Task FadeInAsync(UIElement element, int durationMs = 300)
        {
            element.Visibility = Visibility.Visible;
            return AnimatePropertyAsync(element, UIElement.OpacityProperty, 0, 1, durationMs, EaseOut);
        }

        public static async Task FadeOutAsync(UIElement element, int durationMs = 200)
        {
            await AnimatePropertyAsync(element, UIElement.OpacityProperty, element.Opacity, 0, durationMs, EaseOut);
            element.Visibility = Visibility.Collapsed;
        }

        public static async Task CrossFadeAsync(UIElement fadeOut, UIElement fadeIn, int durationMs = 300)
        {
            var outTask = FadeOutAsync(fadeOut, durationMs / 2);
            await outTask;

            await FadeInAsync(fadeIn, durationMs / 2);
        }


        public static async Task ScaleBounceAsync(FrameworkElement element, int durationMs = 400)
        {
            EnsureScaleTransform(element);
            var transform = (ScaleTransform)element.RenderTransform;

            var phase1 = durationMs * 6 / 10;
            var t1 = AnimatePropertyAsync(transform, ScaleTransform.ScaleXProperty, 0.8, 1.08, phase1, EaseOut);
            var t2 = AnimatePropertyAsync(transform, ScaleTransform.ScaleYProperty, 0.8, 1.08, phase1, EaseOut);
            await Task.WhenAll(t1, t2);

            var phase2 = durationMs * 4 / 10;
            t1 = AnimatePropertyAsync(transform, ScaleTransform.ScaleXProperty, 1.08, 1.0, phase2, EaseInOut);
            t2 = AnimatePropertyAsync(transform, ScaleTransform.ScaleYProperty, 1.08, 1.0, phase2, EaseInOut);
            await Task.WhenAll(t1, t2);
        }

        public static async Task EntranceAsync(FrameworkElement element, int durationMs = 350)
        {
            EnsureScaleTransform(element);
            var transform = (ScaleTransform)element.RenderTransform;

            element.Opacity = 0;

            var fade = AnimatePropertyAsync(element, UIElement.OpacityProperty, 0, 1, durationMs, EaseOut);
            var scaleX = AnimatePropertyAsync(transform, ScaleTransform.ScaleXProperty, 0.96, 1.0, durationMs, EaseOut);
            var scaleY = AnimatePropertyAsync(transform, ScaleTransform.ScaleYProperty, 0.96, 1.0, durationMs, EaseOut);

            await Task.WhenAll(fade, scaleX, scaleY);
        }


        public static async Task SlideDownAsync(FrameworkElement element, int durationMs = 250, double targetHeight = double.NaN)
        {
            element.Visibility = Visibility.Visible;
            element.Opacity = 0;

            if (double.IsNaN(targetHeight))
            {
                element.Measure(new System.Windows.Size(element.ActualWidth > 0 ? element.ActualWidth : 400, double.PositiveInfinity));
                targetHeight = element.DesiredSize.Height;
            }

            element.Height = 0;
            element.Opacity = 1;

            await AnimatePropertyAsync(element, FrameworkElement.HeightProperty, 0, targetHeight, durationMs, EaseOut);
            element.Height = double.NaN;
        }

        public static async Task SlideUpAsync(FrameworkElement element, int durationMs = 200)
        {
            var currentHeight = element.ActualHeight;
            if (currentHeight <= 0)
            {
                element.Visibility = Visibility.Collapsed;
                return;
            }

            await AnimatePropertyAsync(element, FrameworkElement.HeightProperty, currentHeight, 0, durationMs, EaseOut);
            element.Visibility = Visibility.Collapsed;
            element.Height = double.NaN;
        }


        public static async Task ShakeAsync(FrameworkElement element, int durationMs = 400)
        {
            EnsureTranslateTransform(element);
            var transform = (element.RenderTransform as TransformGroup)?.Children.OfType<TranslateTransform>().First()
                            ?? GetOrCreateTranslateTransform(element);

            var keyFrameAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(durationMs),
            };

            var step = durationMs / 6.0;
            keyFrameAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            keyFrameAnim.KeyFrames.Add(new LinearDoubleKeyFrame(-8, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(step))));
            keyFrameAnim.KeyFrames.Add(new LinearDoubleKeyFrame(8, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(step * 2))));
            keyFrameAnim.KeyFrames.Add(new LinearDoubleKeyFrame(-4, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(step * 3))));
            keyFrameAnim.KeyFrames.Add(new LinearDoubleKeyFrame(4, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(step * 4))));
            keyFrameAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs))));

            var tcs = new TaskCompletionSource<bool>();
            keyFrameAnim.Completed += (_, _) => tcs.TrySetResult(true);
            transform.BeginAnimation(TranslateTransform.XProperty, keyFrameAnim);
            await tcs.Task;
        }


        public static void AnimateProgressBar(System.Windows.Controls.ProgressBar bar, double toValue, int durationMs = 500)
        {
            var animation = new DoubleAnimation
            {
                To = toValue,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = EaseInOut,
            };
            bar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, animation);
        }


        private static Task AnimatePropertyAsync(
            IAnimatable target,
            DependencyProperty property,
            double from,
            double to,
            int durationMs,
            IEasingFunction? easing = null)
        {
            var tcs = new TaskCompletionSource<bool>();

            var animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = easing,
                FillBehavior = FillBehavior.HoldEnd,
            };

            animation.Completed += (_, _) =>
            {
                if (target is DependencyObject depObj)
                {
                    depObj.SetValue(property, to);
                    ((IAnimatable)depObj).BeginAnimation(property, null);
                }
                tcs.TrySetResult(true);
            };
            target.BeginAnimation(property, animation);

            return tcs.Task;
        }


        private static void EnsureScaleTransform(FrameworkElement element)
        {
            if (element.RenderTransform is ScaleTransform) return;

            element.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            element.RenderTransform = new ScaleTransform(1, 1);
        }

        private static void EnsureTranslateTransform(FrameworkElement element)
        {
            if (element.RenderTransform is TransformGroup tg &&
                tg.Children.OfType<TranslateTransform>().Any())
                return;

            GetOrCreateTranslateTransform(element);
        }

        private static TranslateTransform GetOrCreateTranslateTransform(FrameworkElement element)
        {
            var translate = new TranslateTransform(0, 0);

            if (element.RenderTransform is TransformGroup existingGroup)
            {
                existingGroup.Children.Add(translate);
            }
            else
            {
                var group = new TransformGroup();
                if (element.RenderTransform != null && element.RenderTransform != Transform.Identity)
                {
                    group.Children.Add(element.RenderTransform);
                }
                group.Children.Add(translate);
                element.RenderTransform = group;
            }

            return translate;
        }
    }
}
