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
using System.Linq;

namespace ArdysaModsTools.Core.Services.Update.Models
{
    public readonly struct AppVersion : IEquatable<AppVersion>, IComparable<AppVersion>
    {
        public string Version { get; }

        public int BuildNumber { get; }

        public AppVersion(string version, int buildNumber)
        {
            Version = NormalizeVersionString(version);
            BuildNumber = Math.Max(0, buildNumber);
        }

        public bool ShouldUpdateTo(AppVersion latest)
        {
            int versionComparison = CompareVersions(this.Version, latest.Version);

            if (versionComparison < 0)
            {
                return true;
            }

            if (versionComparison > 0)
            {
                return false;
            }

            return latest.BuildNumber > this.BuildNumber && latest.BuildNumber > 0;
        }

        public override string ToString()
        {
            return BuildNumber > 0
                ? $"{Version} (Build {BuildNumber})"
                : Version;
        }

        #region Version Comparison

        public static int CompareVersions(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) && string.IsNullOrWhiteSpace(b))
                return 0;
            if (string.IsNullOrWhiteSpace(a)) return -1;
            if (string.IsNullOrWhiteSpace(b)) return 1;

            string aNorm = StripPreRelease(a);
            string bNorm = StripPreRelease(b);

            if (System.Version.TryParse(aNorm, out var vA) &&
                System.Version.TryParse(bNorm, out var vB))
            {
                return vA.CompareTo(vB);
            }

            int[] aParts = ParseSegments(aNorm);
            int[] bParts = ParseSegments(bNorm);
            int maxLen = Math.Max(aParts.Length, bParts.Length);

            for (int i = 0; i < maxLen; i++)
            {
                int av = i < aParts.Length ? aParts[i] : 0;
                int bv = i < bParts.Length ? bParts[i] : 0;
                if (av != bv) return av.CompareTo(bv);
            }

            return 0;
        }

        private static string StripPreRelease(string version)
        {
            if (string.IsNullOrEmpty(version)) return "0";
            version = version.TrimStart('v', 'V');
            int dash = version.IndexOf('-');
            return dash >= 0 ? version.Substring(0, dash) : version;
        }

        private static int[] ParseSegments(string version)
        {
            return version.Split('.')
                .Select(p => int.TryParse(p, out int v) ? v : 0)
                .ToArray();
        }

        private static string NormalizeVersionString(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return "0.0.0";
            return version.Trim().TrimStart('v', 'V');
        }

        #endregion

        #region Factory Methods

        public static int ParseBuildFromFileVersion(string? fileVersion)
        {
            if (string.IsNullOrWhiteSpace(fileVersion))
                return 0;

            var parts = fileVersion.Trim().Split('.');
            if (parts.Length >= 4 && int.TryParse(parts[3], out int build))
                return build;

            return 0;
        }

        public static int ExtractBuildFromText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            int highestBuild = 0;

            var buildMatches = System.Text.RegularExpressions.Regex.Matches(
                text,
                @"(?:\(?\s*Build\s+(\d+)\s*\)?)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            foreach (System.Text.RegularExpressions.Match m in buildMatches)
            {
                if (int.TryParse(m.Groups[1].Value, out int b) && b > highestBuild)
                    highestBuild = b;
            }

            var rangeMatch = System.Text.RegularExpressions.Regex.Match(
                text,
                @"builds?\s*\**\s*(\d+)\s*[→\-–—>]+\s*(\d+)\s*\**",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (rangeMatch.Success)
            {
                if (int.TryParse(rangeMatch.Groups[2].Value, out int endBuild) && endBuild > highestBuild)
                    highestBuild = endBuild;
                if (int.TryParse(rangeMatch.Groups[1].Value, out int startBuild) && startBuild > highestBuild)
                    highestBuild = startBuild;
            }

            return highestBuild;
        }

        #endregion

        #region IEquatable / IComparable

        public bool Equals(AppVersion other)
        {
            return string.Equals(StripPreRelease(Version), StripPreRelease(other.Version),
                       StringComparison.OrdinalIgnoreCase)
                   && BuildNumber == other.BuildNumber;
        }

        public override bool Equals(object? obj) => obj is AppVersion other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(
            StripPreRelease(Version).ToLowerInvariant(), BuildNumber);

        public int CompareTo(AppVersion other)
        {
            int vCmp = CompareVersions(Version, other.Version);
            return vCmp != 0 ? vCmp : BuildNumber.CompareTo(other.BuildNumber);
        }

        public static bool operator ==(AppVersion left, AppVersion right) => left.Equals(right);
        public static bool operator !=(AppVersion left, AppVersion right) => !left.Equals(right);
        public static bool operator <(AppVersion left, AppVersion right) => left.CompareTo(right) < 0;
        public static bool operator >(AppVersion left, AppVersion right) => left.CompareTo(right) > 0;
        public static bool operator <=(AppVersion left, AppVersion right) => left.CompareTo(right) <= 0;
        public static bool operator >=(AppVersion left, AppVersion right) => left.CompareTo(right) >= 0;

        #endregion
    }
}
