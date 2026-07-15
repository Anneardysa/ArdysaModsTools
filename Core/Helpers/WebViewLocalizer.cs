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
using System.IO;
using System.Text.Json;
using ArdysaModsTools.Core.Interfaces;

namespace ArdysaModsTools.Core.Helpers
{
    public static class WebViewLocalizer
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private static string? _helperCache;

        public static string BuildBootstrapScript(ILocalizationService loc)
            => ReadHelper() + "\n;" + BuildApplyScript(loc);

        public static string BuildApplyScript(ILocalizationService loc)
        {
            string active = JsonSerializer.Serialize(loc.ActiveMap, JsonOptions);
            string fallback = JsonSerializer.Serialize(loc.FallbackMap, JsonOptions);
            return $"window.setLocale({active},{fallback});";
        }

        private static string ReadHelper()
        {
            if (_helperCache != null) return _helperCache;
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Html", "i18n.js");
                _helperCache = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            }
            catch
            {
                _helperCache = string.Empty;
            }
            return _helperCache;
        }
    }
}
