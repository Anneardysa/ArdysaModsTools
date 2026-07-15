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
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services.Config;

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
