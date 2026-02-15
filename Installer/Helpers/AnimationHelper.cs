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
    /// <summary>
    /// Provides reusable animation methods for WPF UI elements.
    /// All methods are async and complete when the animation finishes.
    /// </summary>
    public static class AnimationHelper
    {
        // Default easing for all animations — smooth deceleration
        private static readonly QuadraticEase EaseOut = new() { EasingMode = EasingMode.EaseOut };
        private static readonly QuadraticEase EaseInOut = new() { EasingMode = EasingMode.EaseInOut };
        private static readonly CubicEase BounceEase = new() { EasingMode = EasingMode.EaseOut };

        // ================================================================
        // FADE ANIMATIONS
        // ================================================================

        /// <summary>
        /// Fade an element from 0 → 1 opacity.
        /// </summary>
        public static Task FadeInAsync(UIElement element, int durationMs = 300)
        {
            element.Visibility = Visibility.Visible;
            return AnimatePropertyAsync(element, UIElement.OpacityProperty, 0, 1, durationMs, EaseOut);
        }

        /// <summary>
        /// Fade an element from 1 → 0 opacity, then collapse it.
        /// </summary>
        public static async Task FadeOutAsync(UIElement element, int durationMs = 200)
        {
            await AnimatePropertyAsync(element, UIElement.OpacityProperty, element.Opacity, 0, durationMs, EaseOut);
            element.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Cross-fade: fade out the old panel, fade in the new one.
        /// Used for smooth state transitions (Install → Progress → Complete).
        /// </summary>
        public static async Task CrossFadeAsync(UIElement fadeOut, UIElement fadeIn, int durationMs = 300)
        {
            // Start fade-out
            var outTask = FadeOutAsync(fadeOut, durationMs / 2);
            await outTask;

            // Then fade-in
            await FadeInAsync(fadeIn, durationMs / 2);
        }

        // ================================================================
        // SCALE ANIMATIONS
        // ================================================================

        /// <summary>
        /// Scale-bounce: element scales from 0.8 → 1.05 → 1.0
        /// Used for the completion checkmark entrance.
        /// </summary>
        public static async Task ScaleBounceAsync(FrameworkElement element, int durationMs = 400)
        {
            EnsureScaleTransform(element);
            var transform = (ScaleTransform)element.RenderTransform;

            // Phase 1: 0.8 → 1.08 (overshoot)
            var phase1 = durationMs * 6 / 10;
            var t1 = AnimatePropertyAsync(transform, ScaleTransform.ScaleXProperty, 0.8, 1.08, phase1, EaseOut);
            var t2 = AnimatePropertyAsync(transform, ScaleTransform.ScaleYProperty, 0.8, 1.08, phase1, EaseOut);
            await Task.WhenAll(t1, t2);

            // Phase 2: 1.08 → 1.0 (settle back)
            var phase2 = durationMs * 4 / 10;
            t1 = AnimatePropertyAsync(transform, ScaleTransform.ScaleXProperty, 1.08, 1.0, phase2, EaseInOut);
            t2 = AnimatePropertyAsync(transform, ScaleTransform.ScaleYProperty, 1.08, 1.0, phase2, EaseInOut);
            await Task.WhenAll(t1, t2);
        }

        /// <summary>
        /// Subtle scale-up for window entrance: 0.95 → 1.0 with fade.
        /// </summary>
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

        // ================================================================
        // SLIDE ANIMATIONS
        // ================================================================

        /// <summary>
        /// Slide panel down (expand) from 0 height → targetHeight.
        /// </summary>
        public static async Task SlideDownAsync(FrameworkElement element, int durationMs = 250, double targetHeight = double.NaN)
        {
            element.Visibility = Visibility.Visible;
            element.Opacity = 0;

            if (double.IsNaN(targetHeight))
            {
                // Measure desired height
                element.Measure(new System.Windows.Size(element.ActualWidth > 0 ? element.ActualWidth : 400, double.PositiveInfinity));
                targetHeight = element.DesiredSize.Height;
            }

            element.Height = 0;
            element.Opacity = 1;

            await AnimatePropertyAsync(element, FrameworkElement.HeightProperty, 0, targetHeight, durationMs, EaseOut);
            element.Height = double.NaN; // Restore auto-sizing
        }

        /// <summary>
        /// Slide panel up (collapse) from current height → 0.
        /// </summary>
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

        // ================================================================
        // SHAKE ANIMATION (Error feedback)
        // ================================================================

        /// <summary>
        /// Horizontal shake for error states.
        /// Oscillates TranslateTransform.X: 0 → -8 → 8 → -4 → 4 → 0
        /// </summary>
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

        // ================================================================
        // SMOOTH PROGRESS BAR
        // ================================================================

        /// <summary>
        /// Smoothly animate a ProgressBar value change.
        /// </summary>
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

        // ================================================================
        // CORE ANIMATION ENGINE
        // ================================================================

        /// <summary>
        /// Animate a DependencyProperty on any Animatable/UIElement.
        /// Returns a Task that completes when the animation finishes.
        /// </summary>
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
                // Release the animation clock so the property can be set freely again.
                // Without this, HoldEnd keeps the clock alive and fights with subsequent
                // property setters or animations targeting the same property.
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

        // ================================================================
        // TRANSFORM HELPERS
        // ================================================================

        private static void EnsureScaleTransform(FrameworkElement element)
        {
            if (element.RenderTransform is ScaleTransform) return;

            // Center the transform origin
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
