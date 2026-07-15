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
using ArdysaModsTools.Core.Interfaces;

namespace ArdysaModsTools.Core.Services.Localization
{
    public static class Loc
    {
        private static ILocalizationService? _service;

        public static void Initialize(ILocalizationService service) => _service = service;

        public static ILocalizationService? Service => _service;

        public static string T(string key) => _service?.T(key) ?? key;

        public static string T(string key, object values) => _service?.T(key, values) ?? key;

        public static string TPlural(string key, int count) => _service?.TPlural(key, count) ?? key;

        public static string TPlural(string key, int count, object values)
            => _service?.TPlural(key, count, values) ?? key;
    }
}
