/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;
using ArdysaModsTools.Core.Models;

namespace ArdysaModsTools.UI.Forms
{
    /// <summary>
    /// A overlay form that guides newcomers through the app's features
    /// by highlighting controls one at a time with a spotlight effect and tooltip.
    /// Captures a screenshot of the parent form to display the actual UI underneath
    /// a semi-transparent dark dimming layer.
    /// </summary>
    public class OnboardingOverlay : Form
    {
        // ─── State ───────────────────────────────────────────────────────
        private readonly Form _parentForm;
        private readonly List<OnboardingStep> _steps;
        private int _currentStepIndex;
        private Rectangle _spotlightRect;

        // ─── Background Capture ──────────────────────────────────────────
        private Bitmap? _parentSnapshot;

        // ─── Animation ───────────────────────────────────────────────────
        private readonly System.Windows.Forms.Timer _pulseTimer;
        private float _pulsePhase;
        private readonly System.Windows.Forms.Timer _fadeTimer;
        private float _overlayOpacity;
        private bool _fadingIn = true;

        // ─── Step transition animation ───────────────────────────────────
        private Rectangle _prevSpotlightRect;
        private Rectangle _targetSpotlightRect;
        private float _transitionProgress = 1f;
        private readonly System.Windows.Forms.Timer _transitionTimer;

        // ─── Completion callback ─────────────────────────────────────────
        /// <summary>
        /// Fired when the user completes or skips the onboarding guide.
        /// </summary>
        public event EventHandler? OnboardingFinished;

        // ─── Layout Constants ────────────────────────────────────────────
        private const int TooltipWidth = 290;
        private const int TooltipPadding = 14;
        private const int TooltipMargin = 14;
        private const int ButtonWidth = 80;
        private const int ButtonHeight = 28;
        private const float OverlayDimAlpha = 170; // 0-255

        // ─── Button Hit Rects (calculated during paint) ──────────────────
        private Rectangle _nextButtonRect;
        private Rectangle _skipButtonRect;
        private bool _hoveringNext;
        private bool _hoveringSkip;

        public OnboardingOverlay(Form parentForm, List<OnboardingStep> steps)
        {
            _parentForm = parentForm ?? throw new ArgumentNullException(nameof(parentForm));
            _steps = steps ?? throw new ArgumentNullException(nameof(steps));
            _currentStepIndex = 0;

            // Form setup — overlay on top of parent
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            KeyPreview = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

            // Match parent size and position
            Location = _parentForm.PointToScreen(Point.Empty);
            Size = _parentForm.ClientSize;
            TopMost = false;

            // Capture a screenshot of the parent form so the UI is visible underneath
            CaptureParentSnapshot();

            // Pulse animation timer (for spotlight border glow)
            _pulseTimer = new System.Windows.Forms.Timer { Interval = 40 };
            _pulseTimer.Tick += (s, e) =>
            {
                _pulsePhase += 0.08f;
                if (_pulsePhase > 2 * Math.PI) _pulsePhase -= (float)(2 * Math.PI);
                Invalidate();
            };

            // Fade-in timer
            _overlayOpacity = 0f;
            _fadeTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _fadeTimer.Tick += FadeTimer_Tick;

            // Step transition timer (smooth spotlight movement)
            _transitionTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _transitionTimer.Tick += TransitionTimer_Tick;

            // Calculate initial spotlight
            _targetSpotlightRect = CalculateSpotlightFor(_currentStepIndex);
            _spotlightRect = _targetSpotlightRect;
            _prevSpotlightRect = _spotlightRect;
        }

        // ═══════════════════════════════════════════════════════════════
        // PARENT FORM SCREENSHOT
        // ═══════════════════════════════════════════════════════════════

        private void CaptureParentSnapshot()
        {
            try
            {
                var size = _parentForm.ClientSize;
                if (size.Width <= 0 || size.Height <= 0) return;

                _parentSnapshot = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
                _parentForm.DrawToBitmap(_parentSnapshot, new Rectangle(Point.Empty, size));
            }
            catch
            {
                // If screenshot fails, we'll just show black background
                _parentSnapshot = null;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // LIFECYCLE
        // ═══════════════════════════════════════════════════════════════

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _fadingIn = true;
            _overlayOpacity = 0f;
            _fadeTimer.Start();
        }

        private void FadeTimer_Tick(object? sender, EventArgs e)
        {
            if (_fadingIn)
            {
                _overlayOpacity += 0.06f;
                if (_overlayOpacity >= 1f)
                {
                    _overlayOpacity = 1f;
                    _fadingIn = false;
                    _fadeTimer.Stop();
                    _pulseTimer.Start();
                }
            }
            Invalidate();
        }

        private void TransitionTimer_Tick(object? sender, EventArgs e)
        {
            _transitionProgress += 0.06f;
            if (_transitionProgress >= 1f)
            {
                _transitionProgress = 1f;
                _transitionTimer.Stop();
            }

            // Ease-out interpolation
            float t = 1f - (1f - _transitionProgress) * (1f - _transitionProgress);
            _spotlightRect = Lerp(_prevSpotlightRect, _targetSpotlightRect, t);

            Invalidate();
        }

        private static Rectangle Lerp(Rectangle a, Rectangle b, float t)
        {
            return new Rectangle(
                (int)(a.X + (b.X - a.X) * t),
                (int)(a.Y + (b.Y - a.Y) * t),
                (int)(a.Width + (b.Width - a.Width) * t),
                (int)(a.Height + (b.Height - a.Height) * t)
            );
        }

        // ═══════════════════════════════════════════════════════════════
        // SPOTLIGHT CALCULATION
        // ═══════════════════════════════════════════════════════════════

        private Rectangle CalculateSpotlightFor(int stepIndex)
        {
            if (stepIndex < 0 || stepIndex >= _steps.Count)
                return new Rectangle(Width / 2 - 80, Height / 2 - 20, 160, 40);

            var step = _steps[stepIndex];
            var control = FindControlByName(_parentForm, step.ControlName);

            if (control != null)
            {
                // Get control bounds relative to this overlay
                var screenBounds = control.RectangleToScreen(control.ClientRectangle);
                var overlayLocation = PointToScreen(Point.Empty);
                return new Rectangle(
                    screenBounds.X - overlayLocation.X - step.SpotlightPadding,
                    screenBounds.Y - overlayLocation.Y - step.SpotlightPadding,
                    screenBounds.Width + step.SpotlightPadding * 2,
                    screenBounds.Height + step.SpotlightPadding * 2
                );
            }

            // Fallback: center of form
            return new Rectangle(Width / 2 - 80, Height / 2 - 20, 160, 40);
        }

        private void AnimateToStep(int stepIndex)
        {
            _prevSpotlightRect = _spotlightRect;
            _targetSpotlightRect = CalculateSpotlightFor(stepIndex);
            _transitionProgress = 0f;
            _transitionTimer.Start();
        }

        private static Control? FindControlByName(Control parent, string name)
        {
            if (parent.Name == name) return parent;

            foreach (Control child in parent.Controls)
            {
                var found = FindControlByName(child, name);
                if (found != null) return found;
            }

            return null;
        }

        // ═══════════════════════════════════════════════════════════════
        // PAINTING
        // ═══════════════════════════════════════════════════════════════

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;

            float alpha = OverlayDimAlpha * _overlayOpacity;

            // === 0. Draw captured parent form screenshot as background ===
            if (_parentSnapshot != null)
            {
                g.DrawImage(_parentSnapshot, 0, 0);
            }

            // === 1. Draw semi-transparent dark overlay with spotlight cutout ===
            DrawOverlayWithCutout(g, (int)alpha);

            // === 2. Draw spotlight area bright (re-draw snapshot clipped to spotlight) ===
            if (_parentSnapshot != null)
            {
                DrawSpotlightContent(g);
            }

            // === 3. Draw pulsing border around spotlight ===
            DrawSpotlightBorder(g);

            // === 4. Draw connector line from spotlight to tooltip ===
            if (_currentStepIndex >= 0 && _currentStepIndex < _steps.Count)
            {
                var tooltipRect = CalculateTooltipRect();
                DrawConnectorLine(g, tooltipRect);
                DrawTooltipCard(g, _steps[_currentStepIndex], tooltipRect);
            }
        }

        private void DrawOverlayWithCutout(Graphics g, int alpha)
        {
            // Draw 4 rectangles around the spotlight to create the cutout effect
            // This avoids GraphicsPath even-odd issues
            var spot = _spotlightRect;

            using var overlayBrush = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0));

            // Top
            g.FillRectangle(overlayBrush, 0, 0, Width, spot.Top);
            // Bottom
            g.FillRectangle(overlayBrush, 0, spot.Bottom, Width, Height - spot.Bottom);
            // Left
            g.FillRectangle(overlayBrush, 0, spot.Top, spot.Left, spot.Height);
            // Right
            g.FillRectangle(overlayBrush, spot.Right, spot.Top, Width - spot.Right, spot.Height);
        }

        private void DrawSpotlightContent(Graphics g)
        {
            // Re-draw the parent snapshot clipped to the spotlight rect,
            // brightened slightly to make the highlighted control pop
            var state = g.Save();
            g.SetClip(_spotlightRect);

            // Draw the snapshot region with slight brightness boost
            using var ia = new ImageAttributes();
            float brightnessFactor = 1.1f;
            float[][] matrix =
            {
                new float[] { brightnessFactor, 0, 0, 0, 0 },
                new float[] { 0, brightnessFactor, 0, 0, 0 },
                new float[] { 0, 0, brightnessFactor, 0, 0 },
                new float[] { 0, 0, 0, 1, 0 },
                new float[] { 0.02f, 0.02f, 0.02f, 0, 1 }
            };
            ia.SetColorMatrix(new ColorMatrix(matrix));

            g.DrawImage(_parentSnapshot!,
                _spotlightRect,
                _spotlightRect.X, _spotlightRect.Y, _spotlightRect.Width, _spotlightRect.Height,
                GraphicsUnit.Pixel, ia);

            g.Restore(state);
        }

        private void DrawSpotlightBorder(Graphics g)
        {
            // Pulsing glow effect
            float pulse = (float)(Math.Sin(_pulsePhase) + 1) / 2f; // 0..1
            int glowAlpha = (int)(60 + 140 * pulse * _overlayOpacity);
            var glowColor = Color.FromArgb(glowAlpha, 0, 212, 255); // amt-accent cyan

            // Outer glow (thicker, softer)
            using var glowPen3 = new Pen(Color.FromArgb(glowAlpha / 3, 0, 212, 255), 6f);
            g.DrawRectangle(glowPen3, Rectangle.Inflate(_spotlightRect, 4, 4));

            using var glowPen2 = new Pen(Color.FromArgb(glowAlpha / 2, 0, 212, 255), 3f);
            g.DrawRectangle(glowPen2, Rectangle.Inflate(_spotlightRect, 2, 2));

            // Inner border (crisp)
            int borderAlpha = (int)(220 * _overlayOpacity);
            using var borderPen = new Pen(Color.FromArgb(borderAlpha, 0, 212, 255), 1.5f);
            g.DrawRectangle(borderPen, _spotlightRect);

            // Corner brackets (L-shaped decorations)
            int bracketLen = Math.Min(14, Math.Min(_spotlightRect.Width, _spotlightRect.Height) / 3);
            using var bracketPen = new Pen(Color.FromArgb((int)(255 * _overlayOpacity), 0, 212, 255), 2f);

            var s = _spotlightRect;
            // Top-left
            g.DrawLine(bracketPen, s.X - 1, s.Y - 1, s.X + bracketLen, s.Y - 1);
            g.DrawLine(bracketPen, s.X - 1, s.Y - 1, s.X - 1, s.Y + bracketLen);
            // Top-right
            g.DrawLine(bracketPen, s.Right + 1, s.Y - 1, s.Right - bracketLen, s.Y - 1);
            g.DrawLine(bracketPen, s.Right + 1, s.Y - 1, s.Right + 1, s.Y + bracketLen);
            // Bottom-left
            g.DrawLine(bracketPen, s.X - 1, s.Bottom + 1, s.X + bracketLen, s.Bottom + 1);
            g.DrawLine(bracketPen, s.X - 1, s.Bottom + 1, s.X - 1, s.Bottom - bracketLen);
            // Bottom-right
            g.DrawLine(bracketPen, s.Right + 1, s.Bottom + 1, s.Right - bracketLen, s.Bottom + 1);
            g.DrawLine(bracketPen, s.Right + 1, s.Bottom + 1, s.Right + 1, s.Bottom - bracketLen);
        }

        private void DrawConnectorLine(Graphics g, Rectangle tooltipRect)
        {
            // Draw a subtle dotted line from spotlight edge to tooltip
            int spotCenterX = _spotlightRect.X + _spotlightRect.Width / 2;
            int spotCenterY = _spotlightRect.Y + _spotlightRect.Height / 2;
            int tipCenterX = tooltipRect.X + tooltipRect.Width / 2;
            int tipCenterY = tooltipRect.Y + tooltipRect.Height / 2;

            // Find edge points
            Point from, to;
            if (tooltipRect.X > _spotlightRect.Right) // tooltip is to the right
            {
                from = new Point(_spotlightRect.Right, spotCenterY);
                to = new Point(tooltipRect.X, Math.Min(Math.Max(spotCenterY, tooltipRect.Y + 30), tooltipRect.Bottom - 30));
            }
            else if (tooltipRect.Right < _spotlightRect.X) // tooltip is to the left
            {
                from = new Point(_spotlightRect.X, spotCenterY);
                to = new Point(tooltipRect.Right, Math.Min(Math.Max(spotCenterY, tooltipRect.Y + 30), tooltipRect.Bottom - 30));
            }
            else if (tooltipRect.Y > _spotlightRect.Bottom) // tooltip is below
            {
                from = new Point(spotCenterX, _spotlightRect.Bottom);
                to = new Point(Math.Min(Math.Max(spotCenterX, tooltipRect.X + 30), tooltipRect.Right - 30), tooltipRect.Y);
            }
            else // tooltip is above
            {
                from = new Point(spotCenterX, _spotlightRect.Y);
                to = new Point(Math.Min(Math.Max(spotCenterX, tooltipRect.X + 30), tooltipRect.Right - 30), tooltipRect.Bottom);
            }

            int lineAlpha = (int)(60 * _overlayOpacity);
            using var linePen = new Pen(Color.FromArgb(lineAlpha, 0, 212, 255), 1f)
            {
                DashStyle = DashStyle.Dot
            };
            g.DrawLine(linePen, from, to);
        }

        private void DrawTooltipCard(Graphics g, OnboardingStep step, Rectangle tooltipRect)
        {
            int contentAlpha = (int)(255 * _overlayOpacity);

            // === Card background ===
            using var cardBrush = new SolidBrush(Color.FromArgb((int)(240 * _overlayOpacity), 8, 8, 8));
            g.FillRectangle(cardBrush, tooltipRect);

            // Card border
            using var cardBorderPen = new Pen(Color.FromArgb((int)(80 * _overlayOpacity), 51, 51, 51), 1f);
            g.DrawRectangle(cardBorderPen, tooltipRect);

            // Accent line at top of card
            using var accentPen = new Pen(Color.FromArgb((int)(220 * _overlayOpacity), 0, 212, 255), 2f);
            g.DrawLine(accentPen, tooltipRect.X, tooltipRect.Y, tooltipRect.Right, tooltipRect.Y);

            int x = tooltipRect.X + TooltipPadding;
            int y = tooltipRect.Y + TooltipPadding;

            // === Header row: step counter + progress dots (same line) ===
            string stepLabel = $"[{_currentStepIndex + 1}/{_steps.Count}]";
            using var stepFont = new Font("JetBrains Mono", 7.5f, FontStyle.Regular);
            using var dimBrush = new SolidBrush(Color.FromArgb(contentAlpha > 128 ? 80 : 0, 0, 212, 255));
            g.DrawString(stepLabel, stepFont, dimBrush, x, y + 1);

            // Progress dots (right-aligned in header)
            int dotX = tooltipRect.Right - TooltipPadding;
            int dotY = y + 6;
            for (int i = _steps.Count - 1; i >= 0; i--)
            {
                int dotSize = i == _currentStepIndex ? 5 : 3;
                int dotAlpha = i == _currentStepIndex ? contentAlpha : (contentAlpha > 128 ? 50 : 0);
                var dotColor = i == _currentStepIndex
                    ? Color.FromArgb(dotAlpha, 0, 212, 255)
                    : (i < _currentStepIndex
                        ? Color.FromArgb(dotAlpha, 80, 80, 80)
                        : Color.FromArgb(dotAlpha, 35, 35, 35));
                dotX -= dotSize + 3;
                using var dotBrush = new SolidBrush(dotColor);
                g.FillEllipse(dotBrush, dotX, dotY - dotSize / 2, dotSize, dotSize);
            }
            y += 16;

            // === Title ===
            using var titleFont = new Font("JetBrains Mono", 11.5f, FontStyle.Bold);
            using var titleBrush = new SolidBrush(Color.FromArgb(contentAlpha, 255, 255, 255));
            g.DrawString(step.Title, titleFont, titleBrush, x, y);
            y += 24;

            // === Separator line ===
            using var sepPen = new Pen(Color.FromArgb((int)(30 * _overlayOpacity), 255, 255, 255), 1f);
            g.DrawLine(sepPen, x, y, tooltipRect.Right - TooltipPadding, y);
            y += 8;

            // === Description ===
            using var descFont = new Font("JetBrains Mono", 8.5f, FontStyle.Regular);
            using var descBrush = new SolidBrush(Color.FromArgb(contentAlpha > 180 ? 160 : 0, 160, 160, 160));
            int descHeight = tooltipRect.Bottom - TooltipPadding - ButtonHeight - 8 - y;
            var descRect = new RectangleF(x, y, tooltipRect.Width - TooltipPadding * 2, descHeight);
            g.DrawString(step.Description, descFont, descBrush, descRect);

            // === Buttons ===
            y = tooltipRect.Bottom - TooltipPadding - ButtonHeight;

            // Skip button (left side)
            _skipButtonRect = new Rectangle(x, y, ButtonWidth, ButtonHeight);
            DrawButton(g, _skipButtonRect,
                _currentStepIndex == _steps.Count - 1 ? "" : "Skip",
                isSkip: true, isHovered: _hoveringSkip);

            // Next button (right side)
            int nextX = tooltipRect.Right - TooltipPadding - ButtonWidth;
            _nextButtonRect = new Rectangle(nextX, y, ButtonWidth, ButtonHeight);
            string nextLabel = _currentStepIndex == _steps.Count - 1 ? "Got it!" : "Next \u2192";
            DrawButton(g, _nextButtonRect, nextLabel, isSkip: false, isHovered: _hoveringNext);
        }

        private void DrawButton(Graphics g, Rectangle rect, string text, bool isSkip, bool isHovered)
        {
            if (string.IsNullOrEmpty(text)) return;

            int alpha = (int)(255 * _overlayOpacity);

            if (isSkip)
            {
                // Skip button: ghost style
                using var borderPen = new Pen(Color.FromArgb(alpha > 80 ? 40 : 0, 70, 70, 70), 1f);
                g.DrawRectangle(borderPen, rect);

                if (isHovered)
                {
                    using var hoverBrush = new SolidBrush(Color.FromArgb(20, 255, 255, 255));
                    g.FillRectangle(hoverBrush, rect);
                }

                using var textFont = new Font("JetBrains Mono", 8f, FontStyle.Regular);
                using var textBrush = new SolidBrush(Color.FromArgb(alpha > 128 ? 90 : 0, 90, 90, 90));
                var textSize = g.MeasureString(text, textFont);
                float tx = rect.X + (rect.Width - textSize.Width) / 2;
                float ty = rect.Y + (rect.Height - textSize.Height) / 2;
                g.DrawString(text, textFont, textBrush, tx, ty);
            }
            else
            {
                // Next/Got it button: white bg, hover → cyan
                Color bgColor = isHovered
                    ? Color.FromArgb(alpha, 0, 212, 255)
                    : Color.FromArgb(alpha, 240, 240, 240);
                Color textColor = Color.FromArgb(alpha, 5, 5, 5);

                using var bgBrush = new SolidBrush(bgColor);
                g.FillRectangle(bgBrush, rect);

                using var textFont = new Font("JetBrains Mono", 8f, FontStyle.Bold);
                using var textBrush = new SolidBrush(textColor);
                var textSize = g.MeasureString(text, textFont);
                float tx = rect.X + (rect.Width - textSize.Width) / 2;
                float ty = rect.Y + (rect.Height - textSize.Height) / 2;
                g.DrawString(text, textFont, textBrush, tx, ty);
            }
        }

        private Rectangle CalculateTooltipRect()
        {
            // Calculate total tooltip height
            int tooltipHeight = TooltipPadding + 16 + 24 + 8 + 72 + ButtonHeight + TooltipPadding + 4;

            // Try placing to the right of spotlight
            int tx = _spotlightRect.Right + TooltipMargin;
            int ty = _spotlightRect.Y;

            // If it would overflow right, place to the left
            if (tx + TooltipWidth > Width - 10)
            {
                tx = _spotlightRect.Left - TooltipWidth - TooltipMargin;
            }

            // If left also doesn't work, place below
            if (tx < 10)
            {
                tx = Math.Max(10, _spotlightRect.X);
                ty = _spotlightRect.Bottom + TooltipMargin;
            }

            // If below doesn't work, place above
            if (ty + tooltipHeight > Height - 10)
            {
                ty = _spotlightRect.Top - tooltipHeight - TooltipMargin;
            }

            // Final clamp
            tx = Math.Max(10, Math.Min(tx, Width - TooltipWidth - 10));
            ty = Math.Max(10, Math.Min(ty, Height - tooltipHeight - 10));

            return new Rectangle(tx, ty, TooltipWidth, tooltipHeight);
        }

        // ═══════════════════════════════════════════════════════════════
        // MOUSE INTERACTION
        // ═══════════════════════════════════════════════════════════════

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            bool wasHoveringNext = _hoveringNext;
            bool wasHoveringSkip = _hoveringSkip;

            _hoveringNext = _nextButtonRect.Contains(e.Location);
            _hoveringSkip = _skipButtonRect.Contains(e.Location);

            if (wasHoveringNext != _hoveringNext || wasHoveringSkip != _hoveringSkip)
            {
                Cursor = (_hoveringNext || _hoveringSkip) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);

            if (_nextButtonRect.Contains(e.Location))
            {
                GoToNextStep();
            }
            else if (_skipButtonRect.Contains(e.Location) && _currentStepIndex < _steps.Count - 1)
            {
                FinishOnboarding();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // NAVIGATION
        // ═══════════════════════════════════════════════════════════════

        private void GoToNextStep()
        {
            if (_currentStepIndex >= _steps.Count - 1)
            {
                FinishOnboarding();
                return;
            }

            _currentStepIndex++;
            _pulsePhase = 0;
            AnimateToStep(_currentStepIndex);
        }

        private void FinishOnboarding()
        {
            _pulseTimer.Stop();
            _transitionTimer.Stop();
            OnboardingFinished?.Invoke(this, EventArgs.Empty);
            Close();
        }

        // ═══════════════════════════════════════════════════════════════
        // KEY HANDLING
        // ═══════════════════════════════════════════════════════════════

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            switch (e.KeyCode)
            {
                case Keys.Right:
                case Keys.Enter:
                case Keys.Space:
                    GoToNextStep();
                    e.Handled = true;
                    break;
                case Keys.Left:
                    GoToPreviousStep();
                    e.Handled = true;
                    break;
                case Keys.Escape:
                    FinishOnboarding();
                    e.Handled = true;
                    break;
            }
        }

        private void GoToPreviousStep()
        {
            if (_currentStepIndex <= 0) return;
            _currentStepIndex--;
            _pulsePhase = 0;
            AnimateToStep(_currentStepIndex);
        }

        // ═══════════════════════════════════════════════════════════════
        // CLEANUP
        // ═══════════════════════════════════════════════════════════════

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _pulseTimer.Stop();
            _pulseTimer.Dispose();
            _fadeTimer.Stop();
            _fadeTimer.Dispose();
            _transitionTimer.Stop();
            _transitionTimer.Dispose();
            _parentSnapshot?.Dispose();
            _parentSnapshot = null;
            base.OnFormClosed(e);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_NOACTIVATE = 0x08000000;
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_NOACTIVATE;
                return cp;
            }
        }
    }
}
