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
    /// <summary>
    /// Represents an application version with both semantic version and build number.
    /// 
    /// The version string (e.g. "2.1.13-beta") is the user-facing identifier,
    /// while the build number (e.g. 2100) is the internal counter that increments
    /// with every build — even within the same version.
    /// 
    /// Comparison priority:
    ///   1. Semantic version (major.minor.patch) — higher version always wins
    ///   2. Build number — used as tiebreaker when versions are equal
    ///   
    /// This enables hotfix-style updates where you re-publish the same version
    /// with a new build number (e.g. swapping the .zip on R2/GitHub).
    /// </summary>
    public readonly struct AppVersion : IEquatable<AppVersion>, IComparable<AppVersion>
    {
        /// <summary>
        /// The semantic version string (e.g. "2.1.13-beta", "2.1.13").
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// The incremental build number (4th segment of AssemblyVersion/FileVersion).
        /// Example: For FileVersion "2.1.13.2100", BuildNumber = 2100.
        /// </summary>
        public int BuildNumber { get; }

        /// <summary>
        /// Creates a new AppVersion from a version string and build number.
        /// </summary>
        /// <param name="version">Semantic version string (e.g. "2.1.13-beta").</param>
        /// <param name="buildNumber">Build number (0 if unknown).</param>
        public AppVersion(string version, int buildNumber)
        {
            Version = NormalizeVersionString(version);
            BuildNumber = Math.Max(0, buildNumber);
        }

        /// <summary>
        /// Determines whether this version requires an update to the <paramref name="latest"/> version.
        /// Returns true if <paramref name="latest"/> is strictly newer than this instance.
        /// 
        /// Logic:
        ///   1. Compare normalized semantic versions (stripping pre-release suffix)
        ///   2. If versions are equal, compare build numbers (higher build = newer)
        ///   3. If both build numbers are 0, they are considered equal (no update)
        /// </summary>
        /// <param name="latest">The candidate version to compare against.</param>
        /// <returns>True if <paramref name="latest"/> is newer and an update should be offered.</returns>
        public bool ShouldUpdateTo(AppVersion latest)
        {
            int versionComparison = CompareVersions(this.Version, latest.Version);

            if (versionComparison < 0)
            {
                // Latest has a higher semantic version — always update
                return true;
            }

            if (versionComparison > 0)
            {
                // Current has a higher version — never downgrade
                return false;
            }

            // Versions are equal — use build number as tiebreaker.
            // Only trigger update if the latest build is strictly higher.
            // If either side has build 0, it means build info is unknown:
            //   - latest.Build=0: can't prove it's newer → no update
            //   - current.Build=0: can't prove it's older → still require latest > 0
            return latest.BuildNumber > this.BuildNumber && latest.BuildNumber > 0;
        }

        /// <summary>
        /// Display format: "2.1.13-beta (Build 2100)" or "2.1.13-beta" if build is 0.
        /// </summary>
        public override string ToString()
        {
            return BuildNumber > 0
                ? $"{Version} (Build {BuildNumber})"
                : Version;
        }

        #region Version Comparison

        /// <summary>
        /// Compares two version strings numerically, ignoring pre-release suffixes.
        /// Returns negative if a &lt; b, zero if equal, positive if a &gt; b.
        /// 
        /// Examples:
        ///   "2.1.12" vs "2.1.13"  → negative (a is older)
        ///   "2.1.13-beta" vs "2.1.13"  → 0 (equal after stripping suffix)
        ///   "2.2.0" vs "2.1.99"  → positive (a is newer)
        /// </summary>
        public static int CompareVersions(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) && string.IsNullOrWhiteSpace(b))
                return 0;
            if (string.IsNullOrWhiteSpace(a)) return -1;
            if (string.IsNullOrWhiteSpace(b)) return 1;

            // Try System.Version parsing first (handles 2, 3, and 4 part versions)
            string aNorm = StripPreRelease(a);
            string bNorm = StripPreRelease(b);

            if (System.Version.TryParse(aNorm, out var vA) &&
                System.Version.TryParse(bNorm, out var vB))
            {
                return vA.CompareTo(vB);
            }

            // Fallback: manual numeric comparison of each segment
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

        /// <summary>
        /// Strips pre-release suffix (e.g. "-beta", "-rc1") from a version string.
        /// </summary>
        private static string StripPreRelease(string version)
        {
            if (string.IsNullOrEmpty(version)) return "0";
            version = version.TrimStart('v', 'V');
            int dash = version.IndexOf('-');
            return dash >= 0 ? version.Substring(0, dash) : version;
        }

        /// <summary>
        /// Parses "2.1.13" → [2, 1, 13]. Non-numeric segments become 0.
        /// </summary>
        private static int[] ParseSegments(string version)
        {
            return version.Split('.')
                .Select(p => int.TryParse(p, out int v) ? v : 0)
                .ToArray();
        }

        /// <summary>
        /// Normalizes the version string: trims, removes leading 'v'.
        /// </summary>
        private static string NormalizeVersionString(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return "0.0.0";
            return version.Trim().TrimStart('v', 'V');
        }

        #endregion

        #region Factory Methods

        /// <summary>
        /// Parse build number from a FileVersion string of the form "X.X.X.BUILD".
        /// Example: "2.1.13.2100" → build = 2100
        /// </summary>
        /// <param name="fileVersion">The 4-part file version string.</param>
        /// <returns>Build number (4th segment), or 0 if parsing fails.</returns>
        public static int ParseBuildFromFileVersion(string? fileVersion)
        {
            if (string.IsNullOrWhiteSpace(fileVersion))
                return 0;

            var parts = fileVersion.Trim().Split('.');
            if (parts.Length >= 4 && int.TryParse(parts[3], out int build))
                return build;

            return 0;
        }

        /// <summary>
        /// Extract the highest build number from text that may contain build references.
        /// 
        /// Supports multiple patterns found in real release notes:
        ///   - "(Build 2100)"           → 2100
        ///   - "(Build 2083) ... (Build 2084)" → 2084 (takes highest)
        ///   - "builds **2087 → 2098**" → 2098 (takes end of range)
        ///   - "Build 2100"             → 2100 (without parentheses)
        ///   
        /// Always returns the highest build number found across all matches.
        /// </summary>
        /// <param name="text">Text that may contain build number patterns (release notes, title, etc.).</param>
        /// <returns>Highest build number found, or 0 if none.</returns>
        public static int ExtractBuildFromText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            int highestBuild = 0;

            // Pattern 1: "(Build XXXX)" or "Build XXXX" — may appear multiple times
            var buildMatches = System.Text.RegularExpressions.Regex.Matches(
                text,
                @"(?:\(?\s*Build\s+(\d+)\s*\)?)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            foreach (System.Text.RegularExpressions.Match m in buildMatches)
            {
                if (int.TryParse(m.Groups[1].Value, out int b) && b > highestBuild)
                    highestBuild = b;
            }

            // Pattern 2: "builds **XXXX → YYYY**" or "builds XXXX → YYYY" (range format)
            var rangeMatch = System.Text.RegularExpressions.Regex.Match(
                text,
                @"builds?\s*\**\s*(\d+)\s*[→\-–—>]+\s*(\d+)\s*\**",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (rangeMatch.Success)
            {
                // Take the end of the range (highest build)
                if (int.TryParse(rangeMatch.Groups[2].Value, out int endBuild) && endBuild > highestBuild)
                    highestBuild = endBuild;
                // Also check start in case it's somehow higher (shouldn't be, but defensive)
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
