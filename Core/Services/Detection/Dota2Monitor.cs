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
