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

namespace ArdysaModsTools.Core.Interfaces
{
    public interface ILocalizationService
    {
        string CurrentCode { get; }

        CultureInfo CurrentCulture { get; }

        void SetCulture(string code);

        string T(string key);

        string T(string key, object values);

        string TPlural(string key, int count);

        string TPlural(string key, int count, object values);

        IReadOnlyDictionary<string, string> ActiveMap { get; }

        IReadOnlyDictionary<string, string> FallbackMap { get; }

        event EventHandler? CultureChanged;
    }
}
