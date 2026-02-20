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
using NUnit.Framework;
using ArdysaModsTools.Core.Services.Update.Models;

namespace ArdysaModsTools.Tests.Services
{
    /// <summary>
    /// Tests for AppVersion comparison logic — the core of build-aware updates.
    /// Verifies that the update system correctly detects when:
    ///   - A higher version is available (always update)
    ///   - Same version but higher build is available (hotfix update)
    ///   - Same version and build (no update)
    ///   - Lower version or build (no downgrade)
    /// </summary>
    [TestFixture]
    public class UpdateVersionComparisonTests
    {
        #region ShouldUpdateTo — Version Comparison

        [Test]
        public void ShouldUpdateTo_HigherVersion_ReturnsTrue()
        {
            var current = new AppVersion("2.1.12", 2099);
            var latest = new AppVersion("2.1.13", 2100);
            Assert.That(current.ShouldUpdateTo(latest), Is.True);
        }

        [Test]
        public void ShouldUpdateTo_HigherMajorVersion_ReturnsTrue()
        {
            var current = new AppVersion("2.1.13", 2100);
            var latest = new AppVersion("3.0.0", 1);
            Assert.That(current.ShouldUpdateTo(latest), Is.True);
        }

        [Test]
        public void ShouldUpdateTo_LowerVersion_ReturnsFalse()
        {
            var current = new AppVersion("2.1.13", 2100);
            var latest = new AppVersion("2.1.12", 2200);
            Assert.That(current.ShouldUpdateTo(latest), Is.False,
                "Should never downgrade version, even if remote build is higher");
        }

        [Test]
        public void ShouldUpdateTo_LowerMajorVersion_ReturnsFalse()
        {
            var current = new AppVersion("3.0.0", 1);
            var latest = new AppVersion("2.9.99", 9999);
            Assert.That(current.ShouldUpdateTo(latest), Is.False);
        }

        #endregion

        #region ShouldUpdateTo — Build Number Comparison (Same Version)

        [Test]
        public void ShouldUpdateTo_SameVersion_HigherBuild_ReturnsTrue()
        {
            var current = new AppVersion("2.1.13", 2099);
            var latest = new AppVersion("2.1.13", 2100);
            Assert.That(current.ShouldUpdateTo(latest), Is.True);
        }

        [Test]
        public void ShouldUpdateTo_SameVersion_SameBuild_ReturnsFalse()
        {
            var current = new AppVersion("2.1.13", 2100);
            var latest = new AppVersion("2.1.13", 2100);
            Assert.That(current.ShouldUpdateTo(latest), Is.False);
        }

        [Test]
        public void ShouldUpdateTo_SameVersion_OlderBuild_ReturnsFalse()
        {
            var current = new AppVersion("2.1.13", 2100);
            var latest = new AppVersion("2.1.13", 2099);
            Assert.That(current.ShouldUpdateTo(latest), Is.False,
                "Should never downgrade build within the same version");
        }

        [Test]
        public void ShouldUpdateTo_SameVersion_BuildOneHigher_ReturnsTrue()
        {
            var current = new AppVersion("2.1.13", 2099);
            var latest = new AppVersion("2.1.13", 2100);
            Assert.That(current.ShouldUpdateTo(latest), Is.True);
        }

        #endregion

        #region ShouldUpdateTo — Pre-Release Handling

        [Test]
        public void ShouldUpdateTo_SamePreReleaseVersion_HigherBuild_ReturnsTrue()
        {
            var current = new AppVersion("2.1.13-beta", 2099);
            var latest = new AppVersion("2.1.13-beta", 2100);
            Assert.That(current.ShouldUpdateTo(latest), Is.True);
        }

        [Test]
        public void ShouldUpdateTo_BetaToStable_SameNumeric_HigherBuild_ReturnsTrue()
        {
            // "2.1.13-beta" and "2.1.13" have the same numeric version after stripping suffix
            var current = new AppVersion("2.1.13-beta", 2099);
            var latest = new AppVersion("2.1.13", 2100);
            Assert.That(current.ShouldUpdateTo(latest), Is.True);
        }

        [Test]
        public void ShouldUpdateTo_BetaToStable_SameNumeric_SameBuild_ReturnsFalse()
        {
            // Same numeric version, same build = no update (suffix is cosmetic)
            var current = new AppVersion("2.1.13-beta", 2100);
            var latest = new AppVersion("2.1.13", 2100);
            Assert.That(current.ShouldUpdateTo(latest), Is.False);
        }

        #endregion

        #region ShouldUpdateTo — Edge Cases with Build = 0

        [Test]
        public void ShouldUpdateTo_SameVersion_LatestBuildZero_ReturnsFalse()
        {
            // Build 0 means "unknown" — can't prove it's newer
            var current = new AppVersion("2.1.13", 2100);
            var latest = new AppVersion("2.1.13", 0);
            Assert.That(current.ShouldUpdateTo(latest), Is.False,
                "Unknown build (0) should not trigger an update");
        }

        [Test]
        public void ShouldUpdateTo_SameVersion_CurrentBuildZero_LatestHasBuild_ReturnsTrue()
        {
            // Current build unknown, but latest has a known build > 0
            var current = new AppVersion("2.1.13", 0);
            var latest = new AppVersion("2.1.13", 2100);
            Assert.That(current.ShouldUpdateTo(latest), Is.True);
        }

        [Test]
        public void ShouldUpdateTo_SameVersion_BothBuildZero_ReturnsFalse()
        {
            var current = new AppVersion("2.1.13", 0);
            var latest = new AppVersion("2.1.13", 0);
            Assert.That(current.ShouldUpdateTo(latest), Is.False);
        }

        [Test]
        public void ShouldUpdateTo_HigherVersion_IgnoresBuildCompletely()
        {
            // Version takes precedence — even with build 0 latest, higher version triggers update
            var current = new AppVersion("2.1.12", 9999);
            var latest = new AppVersion("2.1.13", 0);
            Assert.That(current.ShouldUpdateTo(latest), Is.True,
                "Higher version should always trigger update regardless of build number");
        }

        #endregion

        #region ParseBuildFromFileVersion

        [Test]
        public void ParseBuildFromFileVersion_ValidFourPart_ReturnsFourthSegment()
        {
            Assert.That(AppVersion.ParseBuildFromFileVersion("2.1.13.2100"), Is.EqualTo(2100));
        }

        [Test]
        public void ParseBuildFromFileVersion_ZeroBuild_ReturnsZero()
        {
            Assert.That(AppVersion.ParseBuildFromFileVersion("2.1.13.0"), Is.EqualTo(0));
        }

        [Test]
        public void ParseBuildFromFileVersion_ThreePart_ReturnsZero()
        {
            Assert.That(AppVersion.ParseBuildFromFileVersion("2.1.13"), Is.EqualTo(0));
        }

        [Test]
        public void ParseBuildFromFileVersion_Null_ReturnsZero()
        {
            Assert.That(AppVersion.ParseBuildFromFileVersion(null), Is.EqualTo(0));
        }

        [Test]
        public void ParseBuildFromFileVersion_Empty_ReturnsZero()
        {
            Assert.That(AppVersion.ParseBuildFromFileVersion(""), Is.EqualTo(0));
        }

        [Test]
        public void ParseBuildFromFileVersion_LargeBuild_ReturnsCorrectValue()
        {
            Assert.That(AppVersion.ParseBuildFromFileVersion("2.1.13.99999"), Is.EqualTo(99999));
        }

        #endregion

        #region ExtractBuildFromText

        [Test]
        public void ExtractBuildFromText_ValidPattern_ReturnsBuildNumber()
        {
            Assert.That(AppVersion.ExtractBuildFromText("v2.1.13-beta (Build 2100)"), Is.EqualTo(2100));
        }

        [Test]
        public void ExtractBuildFromText_CaseInsensitive_ReturnsBuildNumber()
        {
            Assert.That(AppVersion.ExtractBuildFromText("v2.1.13 (build 2100)"), Is.EqualTo(2100));
            Assert.That(AppVersion.ExtractBuildFromText("v2.1.13 (BUILD 2100)"), Is.EqualTo(2100));
        }

        [Test]
        public void ExtractBuildFromText_NoPattern_ReturnsZero()
        {
            Assert.That(AppVersion.ExtractBuildFromText("v2.1.13-beta"), Is.EqualTo(0));
        }

        [Test]
        public void ExtractBuildFromText_BuildInMiddle_ReturnsBuildNumber()
        {
            Assert.That(AppVersion.ExtractBuildFromText("Release 2.1.13 (Build 2100) - Hotfix"), Is.EqualTo(2100));
        }

        [Test]
        public void ExtractBuildFromText_Null_ReturnsZero()
        {
            Assert.That(AppVersion.ExtractBuildFromText(null), Is.EqualTo(0));
        }

        [Test]
        public void ExtractBuildFromText_Empty_ReturnsZero()
        {
            Assert.That(AppVersion.ExtractBuildFromText(""), Is.EqualTo(0));
        }

        [Test]
        public void ExtractBuildFromText_MultipleBuilds_ReturnsHighest()
        {
            // Real pattern from 2.1.12-beta notes: two build sections
            var notes = "## [2.1.12-beta] (Build 2084) - 2026-02-12\r\n### Fixed\r\n- stuff\r\n---\r\n## [2.1.12-beta] (Build 2083) - 2026-02-12\r\n### Added\r\n- more stuff";
            Assert.That(AppVersion.ExtractBuildFromText(notes), Is.EqualTo(2084));
        }

        [Test]
        public void ExtractBuildFromText_RangeFormat_ReturnsEndOfRange()
        {
            // Real pattern from 2.1.13-beta notes: "builds **2087 → 2098**"
            Assert.That(AppVersion.ExtractBuildFromText("> Covers builds **2087 → 2098**"), Is.EqualTo(2098));
        }

        [Test]
        public void ExtractBuildFromText_RangeWithDash_ReturnsEndOfRange()
        {
            Assert.That(AppVersion.ExtractBuildFromText("builds 2087-2098"), Is.EqualTo(2098));
        }

        [Test]
        public void ExtractBuildFromText_RangeWithArrow_ReturnsEndOfRange()
        {
            Assert.That(AppVersion.ExtractBuildFromText("builds 100 > 200"), Is.EqualTo(200));
        }

        [Test]
        public void ExtractBuildFromText_WithoutParentheses_ReturnsBuildNumber()
        {
            Assert.That(AppVersion.ExtractBuildFromText("Build 2100"), Is.EqualTo(2100));
        }

        [Test]
        public void ExtractBuildFromText_RealV213Notes_ReturnsCorrectBuild()
        {
            // Exact notes from user's releases.json for 2.1.13-beta
            var notes = "# Release Notes — v2.1.13\r\n\r\n> Covers builds **2087 → 2098**\r\n> Commits: `641c14eb` → `26633f73`";
            Assert.That(AppVersion.ExtractBuildFromText(notes), Is.EqualTo(2098));
        }

        [Test]
        public void ExtractBuildFromText_RealV212Notes_ReturnsHighestBuild()
        {
            // Exact notes from user's releases.json for 2.1.12-beta
            var notes = "## [2.1.12-beta] (Build 2084) - 2026-02-12\r\n\r\n### 🐛 Fixed\r\n\r\n- stuff\r\n\r\n---\r\n\r\n## [2.1.12-beta] (Build 2083) - 2026-02-12\r\n\r\n### 🚀 Added\r\n\r\n- more stuff";
            Assert.That(AppVersion.ExtractBuildFromText(notes), Is.EqualTo(2084));
        }

        #endregion

        #region ToString

        [Test]
        public void ToString_WithBuild_IncludesBuildNumber()
        {
            var version = new AppVersion("2.1.13-beta", 2100);
            Assert.That(version.ToString(), Is.EqualTo("2.1.13-beta (Build 2100)"));
        }

        [Test]
        public void ToString_WithoutBuild_VersionOnly()
        {
            var version = new AppVersion("2.1.13-beta", 0);
            Assert.That(version.ToString(), Is.EqualTo("2.1.13-beta"));
        }

        [Test]
        public void ToString_StripsLeadingV()
        {
            var version = new AppVersion("v2.1.13", 2100);
            Assert.That(version.ToString(), Is.EqualTo("2.1.13 (Build 2100)"));
        }

        #endregion

        #region CompareVersions

        [TestCase("2.1.12", "2.1.13", -1)]
        [TestCase("2.1.13", "2.1.12", 1)]
        [TestCase("2.1.13", "2.1.13", 0)]
        [TestCase("2.2.0", "2.1.99", 1)]
        [TestCase("3.0.0", "2.9.99", 1)]
        [TestCase("1.0.0", "2.0.0", -1)]
        [TestCase("2.1.13-beta", "2.1.13", 0)] // Pre-release suffix stripped
        [TestCase("v2.1.13", "2.1.13", 0)]      // Leading v stripped
        public void CompareVersions_ProducesCorrectSign(string a, string b, int expectedSign)
        {
            int result = AppVersion.CompareVersions(a, b);
            Assert.That(Math.Sign(result), Is.EqualTo(expectedSign),
                $"CompareVersions(\"{a}\", \"{b}\") expected sign {expectedSign} but got {result}");
        }

        [Test]
        public void CompareVersions_NullInputs_HandledGracefully()
        {
            Assert.That(AppVersion.CompareVersions(null!, null!), Is.EqualTo(0));
            Assert.That(AppVersion.CompareVersions("2.1.13", null!), Is.GreaterThan(0));
            Assert.That(AppVersion.CompareVersions(null!, "2.1.13"), Is.LessThan(0));
        }

        [Test]
        public void CompareVersions_EmptyInputs_HandledGracefully()
        {
            Assert.That(AppVersion.CompareVersions("", ""), Is.EqualTo(0));
            Assert.That(AppVersion.CompareVersions("2.1.13", ""), Is.GreaterThan(0));
            Assert.That(AppVersion.CompareVersions("", "2.1.13"), Is.LessThan(0));
        }

        #endregion

        #region Equality and Comparison Operators

        [Test]
        public void Equality_SameVersionAndBuild_AreEqual()
        {
            var a = new AppVersion("2.1.13", 2100);
            var b = new AppVersion("2.1.13", 2100);
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a == b, Is.True);
        }

        [Test]
        public void Inequality_DifferentBuild_AreNotEqual()
        {
            var a = new AppVersion("2.1.13", 2099);
            var b = new AppVersion("2.1.13", 2100);
            Assert.That(a, Is.Not.EqualTo(b));
            Assert.That(a != b, Is.True);
        }

        [Test]
        public void Comparison_HigherBuild_IsGreater()
        {
            var a = new AppVersion("2.1.13", 2099);
            var b = new AppVersion("2.1.13", 2100);
            Assert.That(a < b, Is.True);
            Assert.That(b > a, Is.True);
        }

        [Test]
        public void Comparison_HigherVersion_IsGreater()
        {
            var a = new AppVersion("2.1.12", 9999);
            var b = new AppVersion("2.1.13", 1);
            Assert.That(a < b, Is.True, "Version should take precedence over build");
        }

        #endregion

        #region Real-World Scenarios

        [Test]
        public void Scenario_HotfixSameVersion_TriggersUpdate()
        {
            // Dev publishes 2.1.13-beta Build 2099, finds bug, rebuilds as Build 2100
            var installed = new AppVersion("2.1.13-beta", 2099);
            var hotfix = new AppVersion("2.1.13-beta", 2100);
            Assert.That(installed.ShouldUpdateTo(hotfix), Is.True);
        }

        [Test]
        public void Scenario_SwapZipOnR2_TriggersUpdate()
        {
            // Same release tag, but you replaced the zip with a new build
            var installed = new AppVersion("2.1.13-beta", 2099);
            var newUpload = new AppVersion("2.1.13-beta", 2100);
            Assert.That(installed.ShouldUpdateTo(newUpload), Is.True);
        }

        [Test]
        public void Scenario_NormalVersionBump_TriggersUpdate()
        {
            var installed = new AppVersion("2.1.13-beta", 2100);
            var nextVersion = new AppVersion("2.1.14", 2101);
            Assert.That(installed.ShouldUpdateTo(nextVersion), Is.True);
        }

        [Test]
        public void Scenario_UserOnLatest_NoUpdate()
        {
            var installed = new AppVersion("2.1.13-beta", 2100);
            var server = new AppVersion("2.1.13-beta", 2100);
            Assert.That(installed.ShouldUpdateTo(server), Is.False);
        }

        [Test]
        public void Scenario_OldManifestNoBuild_NoFalsePositive()
        {
            // Old manifest doesn't have build field → build = 0
            var installed = new AppVersion("2.1.13", 2100);
            var oldManifest = new AppVersion("2.1.13", 0);
            Assert.That(installed.ShouldUpdateTo(oldManifest), Is.False,
                "Should not trigger update when remote has no build info");
        }

        [Test]
        public void Scenario_FreshInstall_NoBuild_ServerHasBuild_TriggersUpdate()
        {
            // User has old install without build tracking, server now has build
            var installed = new AppVersion("2.1.13", 0);
            var server = new AppVersion("2.1.13", 2100);
            Assert.That(installed.ShouldUpdateTo(server), Is.True);
        }

        #endregion
    }
}
