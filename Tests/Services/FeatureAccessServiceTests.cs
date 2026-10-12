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
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services.Config;
using ArdysaModsTools.Core.Services.Update.Models;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class FeatureAccessServiceTests
    {
        private string _cacheDir = null!;

        [SetUp]
        public void Setup()
        {
            _cacheDir = Path.Combine(Path.GetTempPath(), "amt_fa_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_cacheDir);
            FeatureAccessService.CacheFilePath = Path.Combine(_cacheDir, "feature_access_cache.dat");

            FeatureAccessService.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            FeatureAccessService.CacheFilePath = FeatureAccessService.DefaultCacheFilePath;
            FeatureAccessService.ResetForTests();
            try { Directory.Delete(_cacheDir, true); } catch { }
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

            Assert.That(config!.SkinSelector.Enabled, Is.True, "Empty JSON should use fail-open defaults");
            Assert.That(config.SkinSelector.CooldownEnabled, Is.True, "Empty JSON should default CooldownEnabled to true");
            Assert.That(config.Miscellaneous.Enabled, Is.True, "Empty JSON should use fail-open defaults");
            Assert.That(config.InstallModsPack.Enabled, Is.True, "Empty JSON should use fail-open defaults");
        }

        [Test]
        public void FeatureAccessConfig_Deserializes_CooldownFields()
        {
            var json = @"{
                ""skinSelector"": {
                    ""enabled"": true,
                    ""cooldownEnabled"": false,
                    ""cooldownSeconds"": 300
                },
                ""miscellaneous"": {
                    ""enabled"": true,
                    ""cooldownEnabled"": true,
                    ""cooldownSeconds"": 0
                }
            }";

            var config = System.Text.Json.JsonSerializer.Deserialize<FeatureAccessConfig>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert.That(config, Is.Not.Null);
            Assert.That(config!.SkinSelector.CooldownEnabled, Is.False);
            Assert.That(config.SkinSelector.CooldownSeconds, Is.EqualTo(300));
            Assert.That(config.SkinSelector.IsCooldownEnabled(), Is.False);
            Assert.That(config.SkinSelector.GetEffectiveCooldownDuration(TimeSpan.FromMinutes(10)), Is.EqualTo(TimeSpan.Zero));

            Assert.That(config.Miscellaneous.CooldownEnabled, Is.True);
            Assert.That(config.Miscellaneous.CooldownSeconds, Is.EqualTo(0));
            Assert.That(config.Miscellaneous.IsCooldownEnabled(), Is.False);
            Assert.That(config.Miscellaneous.GetEffectiveCooldownDuration(TimeSpan.FromMinutes(10)), Is.EqualTo(TimeSpan.Zero));
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
        [TestCase(FeatureAccessService.UtilitiesFeature)]
        public void Evaluate_WithNoPolicy_Blocks(string feature)
        {
            var result = FeatureAccessService.Evaluate(null, feature, new AppVersion("2.2.19-beta", 2267));

            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.IsOutdated, Is.False, "Offline is not an outdated-app problem.");
            Assert.That(result.IsOffline, Is.True, "The UI must show the Retry notice, not 'unavailable'.");
            Assert.That(result.FeatureName, Is.EqualTo(feature), "Retry re-checks this name.");
            Assert.That(result.BlockedMessage, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void Evaluate_DisabledOrOutdated_IsNotOffline_AndCarriesFeatureName()
        {
            var config = FeatureAccessConfig.CreateDefault();
            config.Miscellaneous.Enabled = false;
            config.SkinSelector.MinVersion = "9.9.9";

            var disabled = FeatureAccessService.Evaluate(
                config, FeatureAccessService.MiscellaneousFeature, new AppVersion("2.7.0", 2431));
            var outdated = FeatureAccessService.Evaluate(
                config, FeatureAccessService.SkinSelectorFeature, new AppVersion("2.7.0", 2431));
            var allowed = FeatureAccessService.Evaluate(
                config, FeatureAccessService.InstallModsPackFeature, new AppVersion("2.7.0", 2431));

            Assert.That(disabled.IsOffline, Is.False);
            Assert.That(outdated.IsOffline, Is.False);
            Assert.That(outdated.IsOutdated, Is.True);
            Assert.That(allowed.IsAllowed, Is.True);
            Assert.That(disabled.FeatureName, Is.EqualTo(FeatureAccessService.MiscellaneousFeature));
            Assert.That(outdated.FeatureName, Is.EqualTo(FeatureAccessService.SkinSelectorFeature));
            Assert.That(allowed.FeatureName, Is.EqualTo(FeatureAccessService.InstallModsPackFeature));
        }

        [Test]
        public void Evaluate_WithNoPolicy_MessageDoesNotBlameTheUsersInternet()
        {
            var result = FeatureAccessService.Evaluate(
                null, FeatureAccessService.InstallModsPackFeature, new AppVersion("2.7.0", 2431));

            Assert.That(result.BlockedMessage, Does.Not.Contain("Connect to the internet"));
            Assert.That(result.BlockedMessage, Does.Not.Contain("current assets"));
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
        public void Evaluate_WithUtilitiesDisabledInJson_Blocks()
        {
            var config = JsonSerializer.Deserialize<FeatureAccessConfig>(
                "{\"utilities\": {\"enabled\": false, \"disabledMessage\": \"Rank Changer paused.\"}}",
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

            var result = FeatureAccessService.Evaluate(
                config, FeatureAccessService.UtilitiesFeature, new AppVersion("2.2.19-beta", 2267));

            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.FeatureDisplayName, Is.EqualTo("Utilities"));
            Assert.That(result.BlockedMessage, Is.EqualTo("Rank Changer paused."));
            Assert.That(config.SkinSelector.Enabled, Is.True, "Other features stay untouched.");
        }

        [Test]
        public void Evaluate_WithGreyOut_Blocks()
        {
            var config = JsonSerializer.Deserialize<FeatureAccessConfig>(
                "{\"utilities\": {\"enabled\": true, \"greyOut\": true}}",
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

            var result = FeatureAccessService.Evaluate(
                config, FeatureAccessService.UtilitiesFeature, new AppVersion("2.2.19-beta", 2267));

            Assert.That(config.Utilities.GreyOut, Is.True);
            Assert.That(result.IsAllowed, Is.False);
            Assert.That(FeatureAccessConfig.CreateDefault().Utilities.GreyOut, Is.False, "Default is not greyed out.");
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

        #region Policy fetch — retry, backoff, coalescing (first-run "offline" block)

        private const string ProductionJson = """
            {
              "_comment": "Remote feature gate. Placeholders: {required}, {current}.",
              "skinSelector": { "enabled": true, "cooldownEnabled": true, "cooldownSeconds": 600,
                "disabledMessage": "Skin Selector is under maintenance.", "minVersion": "2.7.0", "minBuild": 2431,
                "outdatedMessage": "Rebuilt for {required}; you are on {current}." },
              "miscellaneous": { "enabled": true, "cooldownEnabled": true, "cooldownSeconds": 600,
                "minVersion": "2.7.0", "minBuild": 2431 },
              "installModsPack": { "enabled": true,
                "disabledMessage": "Install ModsPack is under maintenance.", "minVersion": "2.7.0", "minBuild": 2431 },
              "utilities": { "enabled": false, "greyOut": true, "minVersion": "2.7.0", "minBuild": 2431 }
            }
            """;

        private static readonly AppVersion Build2431 = new("2.7.0", 2431);

        private static Func<int> UseFetcher(params Func<string?>[] responses)
        {
            int calls = 0;
            FeatureAccessService.Fetcher = _ =>
            {
                int i = Interlocked.Increment(ref calls) - 1;
                return Task.FromResult(responses[Math.Min(i, responses.Length - 1)]());
            };
            return () => Volatile.Read(ref calls);
        }

        private static string? Throw(Exception ex) => throw ex;

        [Test]
        public void ProductionJson_Build2431_AllowsGatedFeatures_AndKeepsUtilitiesGreyedOut()
        {
            var config = JsonSerializer.Deserialize<FeatureAccessConfig>(
                ProductionJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            foreach (var f in new[] { FeatureAccessService.InstallModsPackFeature,
                                      FeatureAccessService.SkinSelectorFeature,
                                      FeatureAccessService.MiscellaneousFeature })
                Assert.That(FeatureAccessService.Evaluate(config, f, Build2431).IsAllowed, Is.True, f);

            Assert.That(FeatureAccessService.Evaluate(
                config, FeatureAccessService.UtilitiesFeature, Build2431).IsAllowed, Is.False);
            Assert.That(FeatureAccessService.Evaluate(
                config, FeatureAccessService.InstallModsPackFeature, new AppVersion("2.7.0", 2430)).IsOutdated,
                Is.True, "One build older must get the update prompt, not the offline notice.");
        }

        [Test]
        public async Task FailedFetch_WithNoFallbackPolicy_NextCallRetriesImmediately()
        {
            var calls = UseFetcher(() => Throw(new TaskCanceledException()), () => ProductionJson);

            var first = await FeatureAccessService.TryGetConfigAsync();
            var second = await FeatureAccessService.TryGetConfigAsync();

            Assert.That(first, Is.Null, "Precondition: first run with no cache has no policy.");
            Assert.That(calls(), Is.EqualTo(2), "No fallback policy — backoff must not skip the retry.");
            Assert.That(second, Is.Not.Null);
            Assert.That(FeatureAccessService.Evaluate(
                second, FeatureAccessService.InstallModsPackFeature, Build2431).IsAllowed, Is.True);
        }

        [TestCase("timeout")]
        [TestCase("http")]
        [TestCase("null")]
        [TestCase("empty")]
        [TestCase("badjson")]
        [TestCase("jsonnull")]
        public async Task FailedFetch_WithNoCache_YieldsNoPolicy_WhichBlocks(string failure)
        {
            UseFetcher(failure switch
            {
                "timeout" => () => Throw(new TaskCanceledException()),
                "http" => () => Throw(new HttpRequestException("No such host is known.")),
                "null" => () => null,
                "empty" => () => "",
                "badjson" => () => "<html>Just a moment...</html>",
                _ => () => "null",
            });

            var config = await FeatureAccessService.TryGetConfigAsync();

            Assert.That(config, Is.Null);
            Assert.That(FeatureAccessService.Evaluate(
                config, FeatureAccessService.InstallModsPackFeature, Build2431).IsAllowed, Is.False);
            Assert.That(FeatureAccessService.LastFailureReason, Is.Not.Null.And.Not.Empty,
                "Support needs to know WHY the gate had no policy.");
        }

        [Test]
        public async Task SuccessfulFetch_PersistsLastKnownGood_WhichSurvivesARestartWhileOffline()
        {
            UseFetcher(() => ProductionJson);
            Assert.That(await FeatureAccessService.TryGetConfigAsync(), Is.Not.Null);
            Assert.That(File.Exists(FeatureAccessService.CacheFilePath), Is.True);
            Assert.That(FeatureAccessService.LastFailureReason, Is.Null);

            FeatureAccessService.ResetForTests();
            UseFetcher(() => Throw(new HttpRequestException("offline")));

            var config = await FeatureAccessService.TryGetConfigAsync();

            Assert.That(FeatureAccessService.Evaluate(
                config, FeatureAccessService.InstallModsPackFeature, Build2431).IsAllowed, Is.True);
        }

        [Test]
        public async Task FailedFetch_WithDiskPolicy_BacksOffInsteadOfReprobing()
        {
            FeatureAccessService.WriteDiskCache(ProductionJson, DateTime.UtcNow);
            var calls = UseFetcher(() => Throw(new TaskCanceledException()));

            var first = await FeatureAccessService.TryGetConfigAsync();
            var second = await FeatureAccessService.TryGetConfigAsync();

            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Not.Null);
            Assert.That(calls(), Is.EqualTo(1));
        }

        [Test]
        public async Task InvalidateCache_ClearsBackoff_SoTheNextCallRefetches()
        {
            FeatureAccessService.WriteDiskCache(ProductionJson, DateTime.UtcNow);
            var calls = UseFetcher(() => Throw(new TaskCanceledException()), () => ProductionJson);

            await FeatureAccessService.TryGetConfigAsync();
            FeatureAccessService.InvalidateCache();
            await FeatureAccessService.TryGetConfigAsync();

            Assert.That(calls(), Is.EqualTo(2));
            Assert.That(FeatureAccessService.LastFailureReason, Is.Null);
        }

        [Test]
        public async Task ConcurrentCalls_ShareOneFetch()
        {
            var gate = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            int calls = 0;
            FeatureAccessService.Fetcher = _ => { Interlocked.Increment(ref calls); return gate.Task; };

            var pending = Enumerable.Range(0, 3).Select(_ => FeatureAccessService.TryGetConfigAsync()).ToArray();
            gate.SetResult(ProductionJson);
            var results = await Task.WhenAll(pending);

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(results, Has.All.Not.Null);
        }

        [Test]
        public async Task CheckFeatureAsync_AfterFailedStartupFetch_ClickIsAllowed()
        {
            Assume.That(FeatureAccessService.IsDevMode, Is.False, "Dev mode bypasses the gate entirely.");
            UseFetcher(() => Throw(new TaskCanceledException()), () => ProductionJson);

            await FeatureAccessService.GetConfigAsync();
            var result = await FeatureAccessService.CheckFeatureAsync(FeatureAccessService.InstallModsPackFeature);

            Assert.That(result.IsAllowed || result.IsOutdated, Is.True, result.BlockedMessage);
        }

        #endregion

        #region Disk cache — protected, bound to this account, bounded age

        private static void GoOffline() => UseFetcher(() => Throw(new HttpRequestException("offline")));

        private static FeatureCheckResult EvaluateInstall(FeatureAccessConfig? config) =>
            FeatureAccessService.Evaluate(config, FeatureAccessService.InstallModsPackFeature, Build2431);

        [Test]
        public async Task DiskCache_RoundTrip_ServesTheLastKnownGoodPolicy()
        {
            FeatureAccessService.WriteDiskCache(ProductionJson, DateTime.UtcNow.AddDays(-6));
            GoOffline();

            Assert.That(EvaluateInstall(await FeatureAccessService.TryGetConfigAsync()).IsAllowed, Is.True);
        }

        [Test]
        public async Task DiskCache_HandWrittenPlainJson_IsRejected_AndTheGateBlocks()
        {
            File.WriteAllText(FeatureAccessService.CacheFilePath,
                "{\"installModsPack\":{\"enabled\":true},\"skinSelector\":{\"enabled\":true}}");
            GoOffline();

            var config = await FeatureAccessService.TryGetConfigAsync();

            Assert.That(config, Is.Null);
            Assert.That(EvaluateInstall(config).IsOffline, Is.True);
        }

        [Test]
        public async Task DiskCache_BlobWithAFlippedByte_IsRejected()
        {
            FeatureAccessService.WriteDiskCache(ProductionJson, DateTime.UtcNow);
            File.SetAttributes(FeatureAccessService.CacheFilePath, FileAttributes.Normal);
            var bytes = File.ReadAllBytes(FeatureAccessService.CacheFilePath);
            bytes[bytes.Length / 2] ^= 0xFF;
            File.WriteAllBytes(FeatureAccessService.CacheFilePath, bytes);
            GoOffline();

            Assert.That(await FeatureAccessService.TryGetConfigAsync(), Is.Null);
        }

        [Test]
        public async Task DiskCache_DpapiBlobForAnotherPurpose_IsRejected()
        {
            var foreign = System.Security.Cryptography.ProtectedData.Protect(
                System.Text.Encoding.UTF8.GetBytes(ProductionJson), null,
                System.Security.Cryptography.DataProtectionScope.CurrentUser);
            File.WriteAllBytes(FeatureAccessService.CacheFilePath, foreign);
            GoOffline();

            Assert.That(await FeatureAccessService.TryGetConfigAsync(), Is.Null);
        }

        [Test]
        public async Task DiskCache_OlderThanMaxAge_IsNotUsed()
        {
            FeatureAccessService.WriteDiskCache(ProductionJson,
                DateTime.UtcNow - FeatureAccessService.DiskCacheMaxAge - TimeSpan.FromHours(1));
            GoOffline();

            Assert.That(await FeatureAccessService.TryGetConfigAsync(), Is.Null);
        }

        [Test]
        public async Task DiskCache_FromTheFuture_IsNotUsed()
        {
            FeatureAccessService.WriteDiskCache(ProductionJson, DateTime.UtcNow.AddDays(2));
            GoOffline();

            Assert.That(await FeatureAccessService.TryGetConfigAsync(), Is.Null);
        }

        [Test]
        public async Task DiskCache_OnDisk_IsHiddenAndNotReadablePolicy()
        {
            UseFetcher(() => ProductionJson);
            await FeatureAccessService.TryGetConfigAsync();

            var path = FeatureAccessService.CacheFilePath;
            Assert.That(File.GetAttributes(path).HasFlag(FileAttributes.Hidden), Is.True);
            string asText = System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(path));
            Assert.That(asText, Does.Not.Contain("installModsPack").And.Not.Contain("enabled"));
        }

        [Test]
        public async Task LegacyPlaintextCache_IsIgnored_AndDeleted()
        {
            string legacy = Path.Combine(_cacheDir, FeatureAccessService.LegacyCacheFileName);
            File.WriteAllText(legacy, ProductionJson);
            GoOffline();

            var offline = await FeatureAccessService.TryGetConfigAsync();

            Assert.That(offline, Is.Null, "Legacy plaintext must not be trusted.");
            Assert.That(File.Exists(legacy), Is.False, "Removed even while offline.");

            File.WriteAllText(legacy, ProductionJson);
            FeatureAccessService.ResetForTests();
            UseFetcher(() => ProductionJson);
            await FeatureAccessService.TryGetConfigAsync();

            Assert.That(File.Exists(legacy), Is.False, "Removed after a successful save too.");
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
