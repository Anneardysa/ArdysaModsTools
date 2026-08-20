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
using System.Collections.Generic;
using ArdysaModsTools.Core.Services;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class Dota2MonitorTests
    {
        private static Dota2Monitor Create(Queue<bool> readings, out List<bool> fired)
        {
            var events = new List<bool>();
            var monitor = new Dota2Monitor(() => readings.Dequeue());
            monitor.OnDota2StateChanged += v => events.Add(v);
            fired = events;
            return monitor;
        }

        [Test]
        public void Evaluate_SingleFlicker_DoesNotFireEvent()
        {
            var readings = new Queue<bool>(new[] { true, true, false, true });
            var monitor = Create(readings, out var fired);
            monitor.Evaluate();
            monitor.Evaluate();
            fired.Clear();

            monitor.Evaluate();
            monitor.Evaluate();

            Assert.That(fired, Is.Empty);
        }

        [Test]
        public void Evaluate_TwoConsecutiveNotRunning_FiresFalseOnce()
        {
            var readings = new Queue<bool>(new[] { true, true, false, false });
            var monitor = Create(readings, out var fired);
            monitor.Evaluate();
            monitor.Evaluate();
            fired.Clear();

            monitor.Evaluate();
            monitor.Evaluate();

            Assert.That(fired, Is.EqualTo(new[] { false }));
        }

        [Test]
        public void Evaluate_TwoConsecutiveRunning_FiresTrueOnce()
        {
            var readings = new Queue<bool>(new[] { true, true });
            var monitor = Create(readings, out var fired);
            monitor.Evaluate();
            monitor.Evaluate();

            Assert.That(fired, Is.EqualTo(new[] { true }));
        }

        [Test]
        public void Evaluate_InterruptedTransition_OnlyFiresOnceItSettles()
        {
            var readings = new Queue<bool>(new[] { true, true, false, true, false, false });
            var monitor = Create(readings, out var fired);
            monitor.Evaluate();
            monitor.Evaluate();
            fired.Clear();

            monitor.Evaluate();
            monitor.Evaluate();
            monitor.Evaluate();
            monitor.Evaluate();

            Assert.That(fired, Is.EqualTo(new[] { false }));
        }
    }
}
