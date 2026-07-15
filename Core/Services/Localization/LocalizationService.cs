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
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using ArdysaModsTools.Core.Interfaces;

namespace ArdysaModsTools.Core.Services.Localization
{
    public sealed class LocalizationService : ILocalizationService
    {
        public const string DefaultCode = "en";

        public static readonly IReadOnlyList<string> SupportedCodes =
            new[] { "en", "ru", "es", "de", "fr", "pt", "zh-Hans", "zh-Hant" };

        private static readonly Regex TokenPattern = new(@"\{(\w+)\}", RegexOptions.Compiled);

        private readonly string _localesDir;
        private readonly object _gate = new();

        private Dictionary<string, string> _active = new(StringComparer.Ordinal);
        private Dictionary<string, string> _fallback = new(StringComparer.Ordinal);

        public LocalizationService()
        {
            _localesDir = Path.Combine(AppContext.BaseDirectory, "Assets", "Locales");
            _fallback = LoadMap(DefaultCode);
            _active = _fallback;
            CurrentCode = DefaultCode;
            CurrentCulture = CultureInfo.GetCultureInfo(DefaultCode);
        }

        public string CurrentCode { get; private set; }

        public CultureInfo CurrentCulture { get; private set; }

        public IReadOnlyDictionary<string, string> ActiveMap
        {
            get { lock (_gate) return _active; }
        }

        public IReadOnlyDictionary<string, string> FallbackMap
        {
            get { lock (_gate) return _fallback; }
        }

        public event EventHandler? CultureChanged;

        public void SetCulture(string code)
        {
            code = ResolveSupported(code);

            var active = code == DefaultCode ? _fallback : LoadMap(code);
            var culture = ToCultureInfo(code);

            lock (_gate)
            {
                _active = active;
                CurrentCode = code;
                CurrentCulture = culture;
            }

            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            CultureChanged?.Invoke(this, EventArgs.Empty);
        }

        public string T(string key)
        {
            if (string.IsNullOrEmpty(key)) return key ?? string.Empty;

            lock (_gate)
            {
                if (_active.TryGetValue(key, out var value)) return value;
                if (_fallback.TryGetValue(key, out var fb)) return fb;
            }
            return key;
        }

        public string T(string key, object values) => Interpolate(T(key), values);

        public string TPlural(string key, int count) => TPlural(key, count, null);

        public string TPlural(string key, int count, object? values)
        {
            string suffix = SelectPluralSuffix(key, count);
            string template = T($"{key}.{suffix}");

            var bag = ToDictionary(values);
            bag["count"] = count;
            return InterpolateFromDictionary(template, bag);
        }

        public static string ResolveSupported(string? cultureName)
        {
            if (string.IsNullOrWhiteSpace(cultureName)) return DefaultCode;

            cultureName = cultureName.Trim();

            foreach (var c in SupportedCodes)
                if (string.Equals(c, cultureName, StringComparison.OrdinalIgnoreCase))
                    return c;

            if (cultureName.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                if (cultureName.IndexOf("Hant", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    cultureName.IndexOf("-TW", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    cultureName.IndexOf("-HK", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    cultureName.IndexOf("-MO", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "zh-Hant";
                return "zh-Hans";
            }

            string twoLetter = cultureName.Split('-')[0].ToLowerInvariant();
            foreach (var c in SupportedCodes)
                if (string.Equals(c, twoLetter, StringComparison.OrdinalIgnoreCase))
                    return c;

            return DefaultCode;
        }

        private static CultureInfo ToCultureInfo(string code)
        {
            try { return CultureInfo.GetCultureInfo(code); }
            catch (CultureNotFoundException) { return CultureInfo.GetCultureInfo(DefaultCode); }
        }

        private static string SelectPluralSuffix(string key, int count)
        {
            if (count == 1) return "one";
            return count == 0 ? "zero" : "other";
        }

        private Dictionary<string, string> LoadMap(string code)
        {
            var path = Path.Combine(_localesDir, code + ".json");
            try
            {
                if (!File.Exists(path)) return new Dictionary<string, string>(StringComparer.Ordinal);

                string json = File.ReadAllText(path);
                var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                return raw == null
                    ? new Dictionary<string, string>(StringComparer.Ordinal)
                    : new Dictionary<string, string>(raw, StringComparer.Ordinal);
            }
            catch (Exception ex)
            {
                FallbackLogger.Log($"[LocalizationService] Failed to load locale '{code}': {ex.Message}");
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }
        }

        private static string Interpolate(string template, object? values)
            => InterpolateFromDictionary(template, ToDictionary(values));

        private static string InterpolateFromDictionary(string template, IDictionary<string, object?> values)
        {
            if (string.IsNullOrEmpty(template) || values.Count == 0) return template;

            return TokenPattern.Replace(template, m =>
            {
                var name = m.Groups[1].Value;
                if (values.TryGetValue(name, out var v))
                    return Convert.ToString(v, CultureInfo.CurrentCulture) ?? string.Empty;
                return m.Value;
            });
        }

        private static Dictionary<string, object?> ToDictionary(object? values)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (values == null) return dict;

            if (values is IDictionary<string, object?> generic)
            {
                foreach (var kv in generic) dict[kv.Key] = kv.Value;
                return dict;
            }

            foreach (var prop in values.GetType().GetProperties(
                         System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (prop.GetIndexParameters().Length == 0)
                    dict[prop.Name] = prop.GetValue(values);
            }
            return dict;
        }
    }
}
