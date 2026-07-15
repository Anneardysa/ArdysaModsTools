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
using System.IO;
using System.Text.RegularExpressions;

namespace ArdysaModsTools.Core.Models
{
    public sealed class GenerationReport
    {
        private readonly Action<string> _log;
        private readonly List<string> _lines = new();
        private readonly List<string> _warnings = new();

        public GenerationReport(Action<string> log)
        {
            _log = log ?? (_ => { });
        }

        public IReadOnlyList<string> Warnings => _warnings;

        public IReadOnlyList<string> Lines => _lines;

        private static readonly Regex UrlPattern = new(
            @"https?://[^\s""'<>]+", RegexOptions.Compiled);
        private static readonly Regex WindowsPathPattern = new(
            @"[A-Za-z]:[\\/][^\s""'<>|]+", RegexOptions.Compiled);

        internal static string Sanitize(string msg)
        {
            if (string.IsNullOrEmpty(msg))
                return msg;

            msg = UrlPattern.Replace(msg, m => "…/" + LastSegment(m.Value, '/'));
            msg = WindowsPathPattern.Replace(msg, m => @"…\" + LastSegment(m.Value, '\\', '/'));
            return msg;
        }

        private static string LastSegment(string value, params char[] separators)
        {
            var trimmed = value.TrimEnd('/', '\\');
            var idx = trimmed.LastIndexOfAny(separators);
            return idx >= 0 && idx < trimmed.Length - 1 ? trimmed[(idx + 1)..] : trimmed;
        }

        public void Log(string msg)
        {
            msg = Sanitize(msg);
            _lines.Add(msg);
            _log(msg);
        }

        public void Debug(string msg)
        {
            msg = Sanitize(msg);
            _lines.Add($"DEBUG {msg}");
            System.Diagnostics.Debug.WriteLine($"[DEBUG] {msg}");
        }

        public void Skip(string item, string reason)
        {
            reason = Sanitize(reason);
            _warnings.Add($"{item}: {reason}");
            _lines.Add($"SKIPPED {item}: {reason}");
            _log($"Skipped {item}: {reason}");
        }

        public void Warn(string msg)
        {
            msg = Sanitize(msg);
            _warnings.Add(msg);
            _lines.Add($"WARNING {msg}");
            _log($"Warning: {msg}");
        }

        private const int KeepReports = 10;

        public void Save(string targetPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(targetPath)) return;
                var dir = Path.Combine(targetPath, "game", "_ArdysaMods", "_temp");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, $"generation_report_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                File.WriteAllLines(path, _lines);

                var old = Directory.GetFiles(dir, "generation_report_*.txt");
                if (old.Length > KeepReports)
                {
                    Array.Sort(old, StringComparer.Ordinal);
                    for (int i = 0; i < old.Length - KeepReports; i++)
                        File.Delete(old[i]);
                }
            }
            catch
            {
            }
        }
    }
}
