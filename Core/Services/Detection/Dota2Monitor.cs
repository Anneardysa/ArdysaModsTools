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
using System.Linq;
using System.Timers;

namespace ArdysaModsTools.Core.Services
{
    public class Dota2Monitor
    {
        private readonly System.Timers.Timer _timer;
        private readonly Func<bool> _isRunningCheck;

        public event Action<bool>? OnDota2StateChanged;
        private bool _lastState = false;
        private bool _pendingState = false;
        private int _pendingCount = 0;

        private const int SettleChecks = 2;

        public Dota2Monitor(Func<bool>? isRunningCheck = null)
        {
            _isRunningCheck = isRunningCheck ?? (() => Process.GetProcessesByName("dota2").Any());
            _timer = new System.Timers.Timer(1500);
            _timer.Elapsed += (s, e) => Evaluate();
        }

        public void Start() => _timer.Start();
        public void Stop() => _timer.Stop();

        internal void Evaluate()
        {
            bool isRunning = _isRunningCheck();

            if (isRunning == _lastState)
            {
                _pendingCount = 0;
                return;
            }

            if (isRunning == _pendingState)
                _pendingCount++;
            else
            {
                _pendingState = isRunning;
                _pendingCount = 1;
            }

            if (_pendingCount >= SettleChecks)
            {
                _lastState = isRunning;
                _pendingCount = 0;
                OnDota2StateChanged?.Invoke(isRunning);
            }
        }
    }
}

