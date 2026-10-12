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
using System.Text.RegularExpressions;

namespace ArdysaModsTools.Core.Services
{
    public static class LogRedaction
    {
        private const string PathReplacement = "the mod package";
        private const string MechanismReplacement = "ModPackage";

        private static readonly Regex ProtectedPath = new(
            @"(?:[A-Za-z]:[\\/]{1,2}(?:[^\r\n]*?[\\/]{1,2})?|[\\/]{2,4}[^\r\n]*?[\\/]{1,2})?game[\\/]{1,2}mod(?![A-Za-z0-9_])(?:[\\/]{1,2}[^\s""'<>|:*?]*)*",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex Mechanism = new(
            @"ProtectedVpkStore(?:\.\w+)?|protected\s+(?:package|vpk|split)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string Apply(string message)
        {
            if (string.IsNullOrEmpty(message))
                return message;

            if (message.IndexOf("mod", StringComparison.OrdinalIgnoreCase) < 0 &&
                message.IndexOf("protect", StringComparison.OrdinalIgnoreCase) < 0)
                return message;

            message = ProtectedPath.Replace(message, PathReplacement);
            return Mechanism.Replace(message, MechanismReplacement);
        }
    }
}
