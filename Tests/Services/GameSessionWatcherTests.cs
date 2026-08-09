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
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Services.App;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class GameSessionWatcherTests
    {
        [Test]
        public void IsWaitLaunch_RecognisesTheFlagAnywhereInTheCommandLine()
        {
            Assert.Multiple(() =>
            {
                Assert.That(GameSessionWatcher.IsWaitLaunch(new[] { @"C:\amt.exe", "--wait-dota" }), Is.True);
                Assert.That(GameSessionWatcher.IsWaitLaunch(new[] { @"C:\amt.exe", "--WAIT-DOTA" }), Is.True);
            });
        }

        [Test]
        public void IsWaitLaunch_ForAnOrdinaryLaunch_IsFalse()
        {
            Assert.Multiple(() =>
            {
                Assert.That(GameSessionWatcher.IsWaitLaunch(new[] { @"C:\amt.exe" }), Is.False);
                Assert.That(GameSessionWatcher.IsWaitLaunch(new[] { @"C:\amt.exe", "--minimized" }), Is.False);
                Assert.That(GameSessionWatcher.IsWaitLaunch(null), Is.False);
            });
        }

        [Test]
        public void IsResumedLaunch_AndIsWaitLaunch_DoNotOverlap()
        {
            var resumed = new[] { @"C:\amt.exe", GameSessionWatcher.ResumedArgument };
            var waiting = new[] { @"C:\amt.exe", GameSessionWatcher.WaitArgument };

            Assert.Multiple(() =>
            {
                Assert.That(GameSessionWatcher.IsResumedLaunch(resumed), Is.True);
                Assert.That(GameSessionWatcher.IsWaitLaunch(resumed), Is.False);
                Assert.That(GameSessionWatcher.IsResumedLaunch(waiting), Is.False);
            });
        }

        [Test]
        public void WaitArgumentsFor_CarriesTheMinimizedFlagAndNothingElse()
        {
            Assert.Multiple(() =>
            {
                Assert.That(GameSessionWatcher.WaitArgumentsFor(false),
                    Is.EqualTo(GameSessionWatcher.WaitArgument));
                Assert.That(GameSessionWatcher.IsMinimizedLaunch(
                    GameSessionWatcher.WaitArgumentsFor(true).Split(' ')), Is.True);
                Assert.That(GameSessionWatcher.IsWaitLaunch(
                    GameSessionWatcher.WaitArgumentsFor(true).Split(' ')), Is.True);
            });
        }

        [Test]
        public async Task WaitUntilGone_KeepsPollingWhileTheGameIsThere()
        {
            var readings = Readings(true, true, false, false);
            int probes = 0;

            await GameSessionWatcher.WaitUntilGoneAsync(
                () => { probes++; return readings(); }, TimeSpan.FromMilliseconds(10), CancellationToken.None);

            Assert.That(probes, Is.EqualTo(4), "it must keep polling while the game is still there");
        }

        [Test]
        public async Task WaitUntilGone_RequiresTheGameToStayGone()
        {
            var readings = Readings(true, false, true, false, false);
            int probes = 0;

            await GameSessionWatcher.WaitUntilGoneAsync(
                () => { probes++; return readings(); }, TimeSpan.FromMilliseconds(10), CancellationToken.None);

            Assert.That(probes, Is.EqualTo(5),
                "a single clean reading between two running ones must not end the wait");
        }

        private static Func<bool> Readings(params bool[] sequence)
        {
            int i = 0;
            return () => i < sequence.Length && sequence[i++];
        }

        [Test]
        public void WaitUntilGone_WhenCancelled_Stops()
        {
            using var cts = new CancellationTokenSource(100);

            Assert.ThrowsAsync<TaskCanceledException>(() =>
                GameSessionWatcher.WaitUntilGoneAsync(
                    () => true, TimeSpan.FromMilliseconds(10), cts.Token));
        }
    }
}
