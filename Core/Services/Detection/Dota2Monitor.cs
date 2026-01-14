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

        public event Action<bool>? OnDota2StateChanged;
        private bool _lastState = false;

        public Dota2Monitor()
        {
            _timer = new System.Timers.Timer(1500); // check every 1.5 sec
            _timer.Elapsed += CheckProcess;
        }

        public void Start() => _timer.Start();
        public void Stop() => _timer.Stop();

        private void CheckProcess(object? sender, ElapsedEventArgs e)
        {
            bool isRunning = Process.GetProcessesByName("dota2").Any();

            if (isRunning != _lastState)
            {
                _lastState = isRunning;
                OnDota2StateChanged?.Invoke(isRunning);
            }
        }
    }
}

