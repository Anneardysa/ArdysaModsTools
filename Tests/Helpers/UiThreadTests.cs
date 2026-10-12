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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.UI.Helpers;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Helpers
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class UiThreadTests
    {
        private Form _form = null!;
        private int _uiThreadId;

        [SetUp]
        public void Setup()
        {
            _form = new Form();
            _ = _form.Handle;
            _uiThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        [TearDown]
        public void TearDown()
        {
            _form.Dispose();
        }

        private static void PumpUntil(Func<bool> done, TimeSpan timeout)
        {
            var sw = Stopwatch.StartNew();
            while (!done() && sw.Elapsed < timeout)
            {
                Application.DoEvents();
                Thread.Sleep(5);
            }
        }

        [Test]
        public void RunAsync_CalledFromBackgroundThread_ExecutesOnUiThread()
        {
            int? observedThreadId = null;

            var backgroundTask = Task.Run(() => UiThread.RunAsync(_form, () =>
            {
                observedThreadId = Thread.CurrentThread.ManagedThreadId;
                return Task.CompletedTask;
            }));

            PumpUntil(() => backgroundTask.IsCompleted, TimeSpan.FromSeconds(5));

            Assert.That(backgroundTask.IsCompletedSuccessfully, Is.True,
                "the marshaled call should complete without hanging or throwing");
            Assert.That(observedThreadId, Is.EqualTo(_uiThreadId),
                "the delegate must run on the control's UI thread, not the caller's background thread");
        }

        [Test]
        public void RunAsync_ExceptionInDelegate_PropagatesThroughReturnedTask()
        {
            var backgroundTask = Task.Run(() => UiThread.RunAsync(_form,
                () => throw new InvalidOperationException("boom")));

            PumpUntil(() => backgroundTask.IsCompleted, TimeSpan.FromSeconds(5));

            Assert.That(backgroundTask.IsFaulted, Is.True, "an exception in the delegate must not be swallowed");
            Assert.That(backgroundTask.Exception!.InnerException, Is.InstanceOf<InvalidOperationException>());
        }

        [Test]
        public void RunAsync_DisposedControl_NoOpsWithoutThrowing()
        {
            _form.Dispose();

            Assert.DoesNotThrowAsync(async () => await UiThread.RunAsync(_form, () => Task.CompletedTask));
        }

        [Test]
        public void Run_CalledFromBackgroundThread_ExecutesOnUiThread_AndBlocksCaller()
        {
            int? observedThreadId = null;

            var backgroundTask = Task.Run(() => UiThread.Run(_form, () =>
            {
                observedThreadId = Thread.CurrentThread.ManagedThreadId;
                return 42;
            }));

            PumpUntil(() => backgroundTask.IsCompleted, TimeSpan.FromSeconds(5));

            Assert.That(backgroundTask.IsCompletedSuccessfully, Is.True);
            Assert.That(backgroundTask.Result, Is.EqualTo(42));
            Assert.That(observedThreadId, Is.EqualTo(_uiThreadId));
        }

        [Test]
        public void RunAsync_CalledFromUiThread_RunsInlineWithoutMarshaling()
        {
            int observedThreadId = -1;

            var task = UiThread.RunAsync(_form, () =>
            {
                observedThreadId = Thread.CurrentThread.ManagedThreadId;
                return Task.CompletedTask;
            });

            Assert.That(task.IsCompletedSuccessfully, Is.True);
            Assert.That(observedThreadId, Is.EqualTo(_uiThreadId));
        }
    }
}
