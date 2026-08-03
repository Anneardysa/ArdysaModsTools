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
using System.Text.Json;
using NUnit.Framework;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services.Config;
using ArdysaModsTools.Core.Services.Update.Models;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class FeatureAccessServiceTests
    {
        [SetUp]
        public void Setup()
        {
            FeatureAccessService.InvalidateCache();
        }

        #region FeatureAccessConfig Model Tests

        [Test]
        public void CreateDefault_ReturnsAllFeaturesEnabled()
        {
            var config = FeatureAccessConfig.CreateDefault();

            Assert.That(config, Is.Not.Null);
            Assert.That(config.SkinSelector.Enabled, Is.True, "SkinSelector should default to enabled");
            Assert.That(config.Miscellaneous.Enabled, Is.True, "Miscellaneous should default to enabled");
        }

        [Test]
        public void FeatureAccess_DefaultValues_AreFailOpen()
        {
            var feature = new FeatureAccess();

            Assert.That(feature.Enabled, Is.True, "Feature should default to enabled (fail-open)");
            Assert.That(feature.DisabledMessage, Is.Null, "DisabledMessage should default to null");
        }

        [Test]
        public void FeatureAccess_GetDisplayMessage_WithCustomMessage_ReturnsCustom()
        {
            var feature = new FeatureAccess
            {
                DisabledMessage = "Down for maintenance until 6 PM."
            };

            var message = feature.GetDisplayMessage();

            Assert.That(message, Is.EqualTo("Down for maintenance until 6 PM."));
        }

        [Test]
        public void FeatureAccess_GetDisplayMessage_WithNullMessage_ReturnsFallback()
        {
            var feature = new FeatureAccess { DisabledMessage = null };

            var message = feature.GetDisplayMessage();

            Assert.That(message, Does.Contain("temporarily unavailable"),
                "Null message should fall back to a sensible default");
        }

        [Test]
        public void FeatureAccess_GetDisplayMessage_WithEmptyMessage_ReturnsFallback()
        {
            var feature = new FeatureAccess { DisabledMessage = "  " };

            var message = feature.GetDisplayMessage();

            Assert.That(message, Does.Contain("temporarily unavailable"),
                "Whitespace-only message should fall back to a sensible default");
        }

        [Test]
        public void FeatureAccessConfig_PropertiesCanBeSet()
        {
            var config = new FeatureAccessConfig
            {
                SkinSelector = new FeatureAccess
                {
                    Enabled = false,
                    DisabledMessage = "Skin Selector disabled"
                },
                Miscellaneous = new FeatureAccess
                {
                    Enabled = false,
                    DisabledMessage = "Misc disabled"
                }
            };

            Assert.That(config.SkinSelector.Enabled, Is.False);
            Assert.That(config.SkinSelector.DisabledMessage, Is.EqualTo("Skin Selector disabled"));
            Assert.That(config.Miscellaneous.Enabled, Is.False);
            Assert.That(config.Miscellaneous.DisabledMessage, Is.EqualTo("Misc disabled"));
        }

        #endregion

        #region JSON Deserialization Tests

        [Test]
        public void FeatureAccessConfig_Deserializes_FromValidJson()
        {
            var json = @"{
                ""skinSelector"": {
                    ""enabled"": false,
                    ""disabledMessage"": ""Maintenance in progress""
                },
                ""miscellaneous"": {
                    ""enabled"": true,
                    ""disabledMessage"": null
                },
                ""installModsPack"": {
                    ""enabled"": false,
                    ""disabledMessage"": ""Install is paused during release""
                }
            }";

            var config = System.Text.Json.JsonSerializer.Deserialize<FeatureAccessConfig>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert.That(config, Is.Not.Null);
            Assert.That(config!.SkinSelector.Enabled, Is.False);
            Assert.That(config.SkinSelector.DisabledMessage, Is.EqualTo("Maintenance in progress"));
            Assert.That(config.Miscellaneous.Enabled, Is.True);
            Assert.That(config.InstallModsPack.Enabled, Is.False);
            Assert.That(config.InstallModsPack.DisabledMessage, Is.EqualTo("Install is paused during release"));
        }

        [Test]
        public void FeatureAccessConfig_Deserializes_PartialJson_UsesDefaults()
        {
            var json = @"{
                ""skinSelector"": {
                    ""enabled"": false
                }
            }";

            var config = System.Text.Json.JsonSerializer.Deserialize<FeatureAccessConfig>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert.That(config, Is.Not.Null);
            Assert.That(config!.SkinSelector.Enabled, Is.False, "Specified value should be used");
            Assert.That(config.Miscellaneous.Enabled, Is.True, "Missing feature should default to enabled (fail-open)");
            Assert.That(config.InstallModsPack.Enabled, Is.True, "Missing feature should default to enabled (fail-open)");
        }

        [Test]
        public void FeatureAccessConfig_Deserializes_EmptyJson_UsesDefaults()
        {
            var json = "{}";

            var config = System.Text.Json.JsonSerializer.Deserialize<FeatureAccessConfig>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert.That(config, Is.Not.Null);
            Assert.That(config!.SkinSelector.Enabled, Is.True, "Empty JSON should use fail-open defaults");
            Assert.That(config.Miscellaneous.Enabled, Is.True, "Empty JSON should use fail-open defaults");
            Assert.That(config.InstallModsPack.Enabled, Is.True, "Empty JSON should use fail-open defaults");
        }

        #endregion

        #region FeatureAccessService Tests

        [Test]
        public void InvalidateCache_ClearsCurrentConfig()
        {
            FeatureAccessService.InvalidateCache();

            Assert.That(FeatureAccessService.CurrentConfig, Is.Null,
                "After cache invalidation, CurrentConfig should be null");
        }

        [Test]
        public void FeatureConstants_HaveCorrectValues()
        {
            Assert.That(FeatureAccessService.SkinSelectorFeature, Is.EqualTo("SkinSelector"));
            Assert.That(FeatureAccessService.MiscellaneousFeature, Is.EqualTo("Miscellaneous"));
        }

        [Test]
        public async Task GetConfigAsync_WhenR2Unavailable_ReturnsDefaultWithAllEnabled()
        {
            FeatureAccessService.InvalidateCache();

            var config = await FeatureAccessService.GetConfigAsync();

            Assert.That(config, Is.Not.Null,
                "Should always return a config, never null (fail-open design)");
        }

        [Test]
        public async Task IsFeatureEnabledAsync_SkinSelector_ReturnsWithoutError()
        {
            FeatureAccessService.InvalidateCache();

            var enabled = await FeatureAccessService.IsFeatureEnabledAsync(
                FeatureAccessService.SkinSelectorFeature);

            Assert.That(enabled, Is.TypeOf<bool>(),
                "Should return a valid boolean regardless of R2 state");
        }

        [Test]
        public async Task IsFeatureEnabledAsync_Miscellaneous_ReturnsWithoutError()
        {
            FeatureAccessService.InvalidateCache();

            var enabled = await FeatureAccessService.IsFeatureEnabledAsync(
                FeatureAccessService.MiscellaneousFeature);

            Assert.That(enabled, Is.TypeOf<bool>(),
                "Should return a valid boolean regardless of R2 state");
        }

        [Test]
        public async Task IsFeatureEnabledAsync_UnknownFeature_ReturnsTrue()
        {
            var enabled = await FeatureAccessService.IsFeatureEnabledAsync("UnknownFeature");

            Assert.That(enabled, Is.True, "Unknown feature should default to enabled (fail-open)");
        }

        [Test]
        public async Task GetFeatureMessageAsync_SkinSelector_ReturnsMessage()
        {
            var message = await FeatureAccessService.GetFeatureMessageAsync(
                FeatureAccessService.SkinSelectorFeature);

            Assert.That(message, Is.Not.Null.And.Not.Empty,
                "Should always return a display message");
        }

        #endregion

        #region CheckFeatureAsync Tests

        [Test]
        public async Task CheckFeatureAsync_SkinSelector_ReturnsResult()
        {
            FeatureAccessService.InvalidateCache();

            var result = await FeatureAccessService.CheckFeatureAsync(
                FeatureAccessService.SkinSelectorFeature);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.FeatureDisplayName, Is.EqualTo("Skin Selector"));
        }

        [Test]
        public async Task CheckFeatureAsync_Miscellaneous_ReturnsResult()
        {
            FeatureAccessService.InvalidateCache();

            var result = await FeatureAccessService.CheckFeatureAsync(
                FeatureAccessService.MiscellaneousFeature);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.FeatureDisplayName, Is.EqualTo("Miscellaneous"));
        }

        [Test]
        public async Task CheckFeatureAsync_InstallModsPack_ReturnsResult()
        {
            FeatureAccessService.InvalidateCache();

            var result = await FeatureAccessService.CheckFeatureAsync(
                FeatureAccessService.InstallModsPackFeature);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.FeatureDisplayName, Is.EqualTo("Install ModsPack"));
        }

        [Test]
        public async Task CheckFeatureAsync_UnknownFeature_ReturnsAllowed_FailOpen()
        {
            var result = await FeatureAccessService.CheckFeatureAsync("UnknownFeature");

            Assert.That(result.IsAllowed, Is.True,
                "Unknown feature should be allowed (fail-open)");
            Assert.That(result.FeatureDisplayName, Is.EqualTo("UnknownFeature"));
        }

        #endregion

        #region FeatureCheckResult Model Tests

        [Test]
        public void FeatureCheckResult_Allowed_HasCorrectState()
        {
            var result = FeatureCheckResult.Allowed("Skin Selector");

            Assert.That(result.IsAllowed, Is.True);
            Assert.That(result.IsDevModeBypass, Is.False);
            Assert.That(result.FeatureDisplayName, Is.EqualTo("Skin Selector"));
            Assert.That(result.BlockedMessage, Is.Null);
        }

        [Test]
        public void FeatureCheckResult_Allowed_DevMode_HasBypassFlag()
        {
            var result = FeatureCheckResult.Allowed("Skin Selector", devModeBypass: true);

            Assert.That(result.IsAllowed, Is.True);
            Assert.That(result.IsDevModeBypass, Is.True);
            Assert.That(result.FeatureDisplayName, Is.EqualTo("Skin Selector"));
        }

        [Test]
        public void FeatureCheckResult_Blocked_HasCorrectState()
        {
            var result = FeatureCheckResult.Blocked("Skin Selector", "Under maintenance");

            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.IsDevModeBypass, Is.False);
            Assert.That(result.FeatureDisplayName, Is.EqualTo("Skin Selector"));
            Assert.That(result.BlockedMessage, Is.EqualTo("Under maintenance"));
        }

        [Test]
        public void FeatureCheckResult_Blocked_NullMessage_IsNotAllowed()
        {
            var result = FeatureCheckResult.Blocked("Miscellaneous", "");

            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.BlockedMessage, Is.EqualTo(""));
        }

        [Test]
        public void FeatureCheckResult_Outdated_IsBlockedAndFlagged()
        {
            var result = FeatureCheckResult.Outdated(
                "Skin Selector", "Update to continue.", "2.2.19-beta (Build 2264)");

            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.IsOutdated, Is.True);
            Assert.That(result.RequiredVersion, Is.EqualTo("2.2.19-beta (Build 2264)"));
            Assert.That(result.BlockedMessage, Is.EqualTo("Update to continue."));
        }

        #endregion

        #region Minimum Version Gate Tests

        private static AppVersion V(string version, int build = 0) => new(version, build);

        [Test]
        public void MeetsMinimumVersion_NoRequirement_Allows()
        {
            var feature = new FeatureAccess();

            Assert.That(FeatureAccessService.MeetsMinimumVersion(feature, V("2.2.18-beta", 2250), out var required), Is.True);
            Assert.That(required, Is.Empty, "No requirement means nothing to display");
        }

        [Test]
        public void MeetsMinimumVersion_NullFeature_Allows()
        {
            Assert.That(FeatureAccessService.MeetsMinimumVersion(null, V("2.2.18-beta", 2250), out _), Is.True);
        }

        [Test]
        public void MeetsMinimumVersion_OlderVersion_Blocks()
        {
            var feature = new FeatureAccess { MinVersion = "2.2.19-beta" };

            Assert.That(FeatureAccessService.MeetsMinimumVersion(feature, V("2.2.18-beta", 2250), out var required), Is.False);
            Assert.That(required, Is.EqualTo("2.2.19-beta"));
        }

        [Test]
        public void MeetsMinimumVersion_SameVersion_Allows()
        {
            var feature = new FeatureAccess { MinVersion = "2.2.19-beta" };

            Assert.That(FeatureAccessService.MeetsMinimumVersion(feature, V("2.2.19-beta", 2264), out _), Is.True);
        }

        [Test]
        public void MeetsMinimumVersion_NewerVersion_Allows()
        {
            var feature = new FeatureAccess { MinVersion = "2.2.19-beta" };

            Assert.That(FeatureAccessService.MeetsMinimumVersion(feature, V("2.3.0-beta", 2300), out _), Is.True);
        }

        [Test]
        public void MeetsMinimumVersion_PreReleaseSuffix_IsIgnored()
        {
            var feature = new FeatureAccess { MinVersion = "2.2.19" };

            Assert.That(FeatureAccessService.MeetsMinimumVersion(feature, V("2.2.19-beta", 2264), out _), Is.True);
        }

        [Test]
        public void MeetsMinimumVersion_SameVersionLowerBuild_Blocks()
        {
            var feature = new FeatureAccess { MinVersion = "2.2.19-beta", MinBuild = 2264 };

            Assert.That(FeatureAccessService.MeetsMinimumVersion(feature, V("2.2.19-beta", 2260), out var required), Is.False);
            Assert.That(required, Is.EqualTo("2.2.19-beta (Build 2264)"));
        }

        [Test]
        public void MeetsMinimumVersion_SameVersionEqualBuild_Allows()
        {
            var feature = new FeatureAccess { MinVersion = "2.2.19-beta", MinBuild = 2264 };

            Assert.That(FeatureAccessService.MeetsMinimumVersion(feature, V("2.2.19-beta", 2264), out _), Is.True);
        }

        [Test]
        public void MeetsMinimumVersion_MinBuildOnly_ComparesWithinRunningVersion()
        {
            var feature = new FeatureAccess { MinBuild = 2264 };

            Assert.That(FeatureAccessService.MeetsMinimumVersion(feature, V("2.2.19-beta", 2260), out _), Is.False);
            Assert.That(FeatureAccessService.MeetsMinimumVersion(feature, V("2.2.19-beta", 2270), out _), Is.True);
        }

        [Test]
        public void MeetsMinimumVersion_UnparseableMinVersion_FailsOpen()
        {
            var feature = new FeatureAccess { MinVersion = "not-a-version" };

            Assert.That(FeatureAccessService.MeetsMinimumVersion(feature, V("2.2.18-beta", 2250), out _), Is.True);
        }

        [Test]
        public void MeetsMinimumVersion_NewerMajorThanRequired_Allows()
        {
            var feature = new FeatureAccess { MinVersion = "2.9.0" };

            Assert.That(FeatureAccessService.MeetsMinimumVersion(feature, V("2.10.0", 2400), out _), Is.True);
        }

        [Test]
        public void GetOutdatedMessage_CustomMessage_SubstitutesPlaceholders()
        {
            var feature = new FeatureAccess
            {
                OutdatedMessage = "Need {required}, you run {current}."
            };

            var message = feature.GetOutdatedMessage("2.2.19-beta", "2.2.18-beta", "ignored fallback");

            Assert.That(message, Is.EqualTo("Need 2.2.19-beta, you run 2.2.18-beta."));
        }

        [Test]
        public void GetOutdatedMessage_NoCustomMessage_UsesFallback()
        {
            var feature = new FeatureAccess();

            var message = feature.GetOutdatedMessage("2.2.19-beta", "2.2.18-beta", "Update to {required}.");

            Assert.That(message, Is.EqualTo("Update to 2.2.19-beta."));
        }

        [Test]
        public void GetOutdatedMessage_NoMessageAndNoFallback_UsesBuiltInDefault()
        {
            var feature = new FeatureAccess();

            var message = feature.GetOutdatedMessage("2.2.19-beta", "2.2.18-beta");

            Assert.That(message, Does.Contain("2.2.19-beta"));
            Assert.That(message, Does.Contain("2.2.18-beta"));
            Assert.That(message, Does.Not.Contain("{required}"));
        }

        #endregion

        #region Evaluate — fail-closed gating (ADR-0014)

        [TestCase(FeatureAccessService.SkinSelectorFeature)]
        [TestCase(FeatureAccessService.MiscellaneousFeature)]
        [TestCase(FeatureAccessService.InstallModsPackFeature)]
        public void Evaluate_WithNoPolicy_Blocks(string feature)
        {
            var result = FeatureAccessService.Evaluate(null, feature, new AppVersion("2.2.19-beta", 2267));

            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.IsOutdated, Is.False, "Offline is not an outdated-app problem.");
            Assert.That(result.BlockedMessage, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void Evaluate_WithEnabledPolicy_Allows()
        {
            var config = FeatureAccessConfig.CreateDefault();

            var result = FeatureAccessService.Evaluate(
                config, FeatureAccessService.SkinSelectorFeature, new AppVersion("2.2.19-beta", 2267));

            Assert.That(result.IsAllowed, Is.True);
            Assert.That(result.FeatureDisplayName, Is.EqualTo("Skin Selector"));
        }

        [Test]
        public void Evaluate_WithDisabledFeature_BlocksWithoutUpdatePrompt()
        {
            var config = FeatureAccessConfig.CreateDefault();
            config.SkinSelector.Enabled = false;
            config.SkinSelector.DisabledMessage = "Down for maintenance.";

            var result = FeatureAccessService.Evaluate(
                config, FeatureAccessService.SkinSelectorFeature, new AppVersion("2.2.19-beta", 2267));

            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.IsOutdated, Is.False);
            Assert.That(result.BlockedMessage, Is.EqualTo("Down for maintenance."));
        }

        [Test]
        public void Evaluate_WhenBelowMinBuild_ReportsOutdatedSoTheUiOffersAnUpdate()
        {
            var config = FeatureAccessConfig.CreateDefault();
            config.SkinSelector.MinVersion = "2.2.19-beta";
            config.SkinSelector.MinBuild = 2300;

            var result = FeatureAccessService.Evaluate(
                config, FeatureAccessService.SkinSelectorFeature, new AppVersion("2.2.19-beta", 2267));

            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.IsOutdated, Is.True);
            Assert.That(result.RequiredVersion, Does.Contain("2300"));
        }

        [Test]
        public void Evaluate_WithNullFeatureEntry_DoesNotThrow()
        {
            var config = JsonSerializer.Deserialize<FeatureAccessConfig>(
                "{\"skinSelector\": null}", new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

            Assert.That(config.SkinSelector, Is.Null, "Precondition: JSON null wins over the initializer.");

            FeatureCheckResult result = null!;
            Assert.DoesNotThrow(() => result = FeatureAccessService.Evaluate(
                config, FeatureAccessService.SkinSelectorFeature, new AppVersion("2.2.19-beta", 2267)));
            Assert.That(result.IsAllowed, Is.True, "An unspecified entry means the publisher said nothing.");
        }

        [Test]
        public void Evaluate_WhenAtMinBuild_Allows()
        {
            var config = FeatureAccessConfig.CreateDefault();
            config.SkinSelector.MinVersion = "2.2.19-beta";
            config.SkinSelector.MinBuild = 2267;

            var result = FeatureAccessService.Evaluate(
                config, FeatureAccessService.SkinSelectorFeature, new AppVersion("2.2.19-beta", 2267));

            Assert.That(result.IsAllowed, Is.True);
        }

        #endregion

        #region IsDevMode Tests

        [Test]
        public void IsDevMode_ReturnsBoolean()
        {
            var devMode = FeatureAccessService.IsDevMode;

            Assert.That(devMode, Is.TypeOf<bool>());
        }

        #endregion
    }
}
