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
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace ArdysaModsTools.UI.Helpers
{
    public static class WebViewNavigation
    {
        public static async Task NavigateAndWaitReadyAsync(CoreWebView2 core, string html, TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnDomReady(object? s, CoreWebView2DOMContentLoadedEventArgs e) => tcs.TrySetResult(true);
            void OnNavCompleted(object? s, CoreWebView2NavigationCompletedEventArgs e)
            {
                if (!e.IsSuccess)
                    tcs.TrySetException(new InvalidOperationException($"WebView2 navigation failed: {e.WebErrorStatus}"));
            }

            core.DOMContentLoaded += OnDomReady;
            core.NavigationCompleted += OnNavCompleted;
            try
            {
                core.NavigateToString(html);

                if (await Task.WhenAny(tcs.Task, Task.Delay(timeout)) != tcs.Task)
                    throw new TimeoutException($"WebView2 page did not become ready within {timeout.TotalSeconds:0}s");

                await tcs.Task;
            }
            finally
            {
                core.DOMContentLoaded -= OnDomReady;
                core.NavigationCompleted -= OnNavCompleted;
            }
        }
    }
}
