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
using System.Windows.Forms;

namespace ArdysaModsTools.UI.Helpers
{
    public static class UiThread
    {
        private static bool IsUsable(Control? control) => control is { IsDisposed: false };

        public static Task RunAsync(Control? control, Func<Task> action)
        {
            if (!IsUsable(control))
                return Task.CompletedTask;

            if (!control!.IsHandleCreated)
                return Task.CompletedTask;

            if (!control.InvokeRequired)
                return action();

            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            control.BeginInvoke(new Action(async () =>
            {
                try
                {
                    await action();
                    tcs.TrySetResult();
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }));
            return tcs.Task;
        }

        public static Task<T> RunAsync<T>(Control? control, Func<Task<T>> action, T fallback = default!)
        {
            if (!IsUsable(control))
                return Task.FromResult(fallback);

            if (!control!.IsHandleCreated)
                return Task.FromResult(fallback);

            if (!control.InvokeRequired)
                return action();

            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            control.BeginInvoke(new Action(async () =>
            {
                try
                {
                    tcs.TrySetResult(await action());
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }));
            return tcs.Task;
        }

        public static T Run<T>(Control? control, Func<T> func, T fallback = default!)
        {
            if (!IsUsable(control))
                return fallback;

            if (!control!.IsHandleCreated)
                return fallback;

            if (!control.InvokeRequired)
                return func();

            return (T)control.Invoke(func);
        }

        public static void Post(Control? control, Action action)
        {
            if (!IsUsable(control))
                return;

            if (!control!.IsHandleCreated)
                return;

            if (!control.InvokeRequired)
            {
                action();
                return;
            }

            control.BeginInvoke(action);
        }
    }
}
