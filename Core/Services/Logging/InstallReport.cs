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
using System.Linq;

namespace ArdysaModsTools.Core.Services
{
    public static class InstallReport
    {
        public const string Default = "default";
        public const string Success = "success";
        public const string Warning = "warning";
        public const string Error = "error";

        private static readonly object _lock = new();
        private static readonly List<(string Text, string Category)> _lines = new();

        public static void Begin()
        {
            lock (_lock) _lines.Clear();
        }

        public static void Step(string text) => Add(text, Default);
        public static void Ok(string text) => Add(text, Success);
        public static void Warn(string text) => Add(text, Warning);
        public static void Fail(string text) => Add(text, Error);

        public static IReadOnlyList<(string Text, string Category)> Snapshot()
        {
            lock (_lock) return _lines.ToList();
        }

        private static void Add(string text, string category)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            lock (_lock) _lines.Add((text, category));
        }
    }
}
