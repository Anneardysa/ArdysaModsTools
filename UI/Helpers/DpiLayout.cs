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
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace ArdysaModsTools.UI.Helpers
{
    public static class DpiLayout
    {
        public static double UiScale { get; set; } = 1.0;

        private static readonly List<(Form form, WebView2 web, Size baseSize)> _live = new();

        private const int ScreenMargin = 24;

        private static double ClampScale(double s) => Math.Min(2.0, Math.Max(1.0, s));

        public static double CurrentUiScale => ClampScale(UiScale);

        public static void ReapplyAll()
        {
            _live.RemoveAll(e => e.form.IsDisposed || e.web.IsDisposed);
            foreach (var (form, web, baseSize) in _live)
            {
                ApplyScale(form, web, baseSize, form.DeviceDpi);
                if (!form.IsHandleCreated || form.IsDisposed)
                    continue;
                CenterForm(form);
                ClampToWorkingArea(form);
            }
        }

        private static void CenterForm(Form form)
        {
            if (form == null || form.IsDisposed || !form.IsHandleCreated)
                return;

            var owner = form.Owner;
            if (owner != null && !owner.IsDisposed && owner.IsHandleCreated)
            {
                form.Location = new Point(
                    owner.Left + (owner.Width - form.Width) / 2,
                    owner.Top + (owner.Height - form.Height) / 2);
            }
            else
            {
                var work = Screen.FromControl(form).WorkingArea;
                form.Location = new Point(
                    work.X + (work.Width - form.Width) / 2,
                    work.Y + (work.Height - form.Height) / 2);
            }
        }

        private static void ApplyScale(Form form, WebView2 web, Size baseSize, int dpi)
        {
            if (form.IsDisposed || web.IsDisposed)
                return;
            double scale = EffectiveScale(form, baseSize);
            try { web.ZoomFactor = (96.0 / Math.Max(dpi, 96)) * scale; }
            catch {  }
            try { form.Size = new Size((int)Math.Round(baseSize.Width * scale), (int)Math.Round(baseSize.Height * scale)); }
            catch {  }
        }

        private static double EffectiveScale(Form form, Size baseSize)
        {
            double requested = ClampScale(UiScale);
            if (requested <= 1.0 || baseSize.Width <= 0 || baseSize.Height <= 0)
                return requested;

            var screen = (form.IsHandleCreated && !form.IsDisposed)
                ? Screen.FromControl(form)
                : Screen.PrimaryScreen;
            if (screen == null)
                return requested;

            var work = screen.WorkingArea;
            int availW = work.Width - ScreenMargin * 2;
            int availH = work.Height - ScreenMargin * 2;
            if (availW <= 0 || availH <= 0)
                return requested;

            double fit = Math.Min((double)availW / baseSize.Width, (double)availH / baseSize.Height);

            return Math.Max(1.0, Math.Min(requested, fit));
        }

        private static void ApplyScaleAndRecenter(Form form, WebView2 web, Size baseSize, int dpi)
        {
            ApplyScale(form, web, baseSize, dpi);
            if (ClampScale(UiScale) == 1.0 || form.IsDisposed || !form.IsHandleCreated || !form.Visible)
                return;
            form.BeginInvoke(new Action(() =>
            {
                if (form.IsDisposed) return;
                CenterForm(form);
                ClampToWorkingArea(form);
            }));
        }

        public static void PinTo100(Form form, WebView2 webView)
        {
            if (form == null || webView == null)
                return;

            var baseSize = form.Size;

            ApplyScaleAndRecenter(form, webView, baseSize, form.DeviceDpi);
            form.DpiChanged += (s, e) => ApplyScaleAndRecenter(form, webView, baseSize, e.DeviceDpiNew);

            _live.Add((form, webView, baseSize));
            form.Disposed += (s, e) => _live.RemoveAll(en => en.form == form);
        }

        public static void AttachClamp(Form form, int margin = 24)
        {
            if (form == null)
                return;

            form.Shown += (s, e) =>
            {
                if (form.IsHandleCreated && !form.IsDisposed)
                    form.BeginInvoke(new Action(() => ClampToWorkingArea(form, margin)));
            };
            form.DpiChanged += (s, e) => ClampToWorkingArea(form, margin);
        }

        public static void ClampToWorkingArea(Form form, int margin = 24)
        {
            if (form == null || form.IsDisposed || !form.IsHandleCreated)
                return;

            var work = Screen.FromControl(form).WorkingArea;
            int maxW = Math.Max(200, work.Width - margin * 2);
            int maxH = Math.Max(200, work.Height - margin * 2);

            int w = Math.Min(form.Width, maxW);
            int h = Math.Min(form.Height, maxH);
            if (w == form.Width && h == form.Height)
            {
                NudgeOnScreen(form, work);
                return;
            }

            form.Size = new Size(w, h);

            form.Location = new Point(
                work.X + (work.Width - w) / 2,
                work.Y + (work.Height - h) / 2);
            NudgeOnScreen(form, work);
        }

        private static void NudgeOnScreen(Form form, Rectangle work)
        {
            int x = Math.Min(Math.Max(form.Left, work.Left), work.Right - form.Width);
            int y = Math.Min(Math.Max(form.Top, work.Top), work.Bottom - form.Height);
            if (x != form.Left || y != form.Top)
                form.Location = new Point(x, y);
        }
    }
}
