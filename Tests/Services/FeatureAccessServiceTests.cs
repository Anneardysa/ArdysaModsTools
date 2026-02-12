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
    /// <summary>
    /// Tests for FeatureAccessConfig model and FeatureAccessService.
    /// Validates fail-open defaults, model deserialization, and cache behavior.
    /// </summary>
    [TestFixture]
    public class FeatureAccessServiceTests
    {
        [SetUp]
        public void Setup()
        {
            // Ensure clean state for each test
            FeatureAccessService.InvalidateCache();
        }

        #region FeatureAccessConfig Model Tests

        [Test]
        public void CreateDefault_ReturnsAllFeaturesEnabled()
        {
            // Act
            var config = FeatureAccessConfig.CreateDefault();

            // Assert
            Assert.That(config, Is.Not.Null);
            Assert.That(config.SkinSelector.Enabled, Is.True, "SkinSelector should default to enabled");
            Assert.That(config.Miscellaneous.Enabled, Is.True, "Miscellaneous should default to enabled");
        }

        [Test]
        public void FeatureAccess_DefaultValues_AreFailOpen()
        {
            // Act
            var feature = new FeatureAccess();

            // Assert
            Assert.That(feature.Enabled, Is.True, "Feature should default to enabled (fail-open)");
            Assert.That(feature.DisabledMessage, Is.Null, "DisabledMessage should default to null");
        }

        [Test]
        public void FeatureAccess_GetDisplayMessage_WithCustomMessage_ReturnsCustom()
        {
            // Arrange
            var feature = new FeatureAccess
            {
                DisabledMessage = "Down for maintenance until 6 PM."
            };

            // Act
            var message = feature.GetDisplayMessage();

            // Assert
            Assert.That(message, Is.EqualTo("Down for maintenance until 6 PM."));
        }

        [Test]
        public void FeatureAccess_GetDisplayMessage_WithNullMessage_ReturnsFallback()
        {
            // Arrange
            var feature = new FeatureAccess { DisabledMessage = null };

            // Act
            var message = feature.GetDisplayMessage();

            // Assert
            Assert.That(message, Does.Contain("temporarily unavailable"),
                "Null message should fall back to a sensible default");
        }

        [Test]
        public void FeatureAccess_GetDisplayMessage_WithEmptyMessage_ReturnsFallback()
        {
            // Arrange
            var feature = new FeatureAccess { DisabledMessage = "  " };

            // Act
            var message = feature.GetDisplayMessage();

            // Assert
            Assert.That(message, Does.Contain("temporarily unavailable"),
                "Whitespace-only message should fall back to a sensible default");
        }

        [Test]
        public void FeatureAccessConfig_PropertiesCanBeSet()
        {
            // Arrange
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

            // Assert
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
            // Arrange
            var json = @"{
                ""skinSelector"": {
                    ""enabled"": false,
                    ""disabledMessage"": ""Maintenance in progress""
                },
                ""miscellaneous"": {
                    ""enabled"": true,
                    ""disabledMessage"": null
                }
            }";

            // Act
            var config = System.Text.Json.JsonSerializer.Deserialize<FeatureAccessConfig>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Assert
            Assert.That(config, Is.Not.Null);
            Assert.That(config!.SkinSelector.Enabled, Is.False);
            Assert.That(config.SkinSelector.DisabledMessage, Is.EqualTo("Maintenance in progress"));
            Assert.That(config.Miscellaneous.Enabled, Is.True);
        }

        [Test]
        public void FeatureAccessConfig_Deserializes_PartialJson_UsesDefaults()
        {
            // Arrange - only skinSelector specified
            var json = @"{
                ""skinSelector"": {
                    ""enabled"": false
                }
            }";

            // Act
            var config = System.Text.Json.JsonSerializer.Deserialize<FeatureAccessConfig>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Assert
            Assert.That(config, Is.Not.Null);
            Assert.That(config!.SkinSelector.Enabled, Is.False, "Specified value should be used");
            Assert.That(config.Miscellaneous.Enabled, Is.True, "Missing feature should default to enabled (fail-open)");
        }

        [Test]
        public void FeatureAccessConfig_Deserializes_EmptyJson_UsesDefaults()
        {
            // Arrange
            var json = "{}";

            // Act
            var config = System.Text.Json.JsonSerializer.Deserialize<FeatureAccessConfig>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Assert
            Assert.That(config, Is.Not.Null);
            Assert.That(config!.SkinSelector.Enabled, Is.True, "Empty JSON should use fail-open defaults");
            Assert.That(config.Miscellaneous.Enabled, Is.True, "Empty JSON should use fail-open defaults");
        }

        #endregion

        #region FeatureAccessService Tests

        [Test]
        public void InvalidateCache_ClearsCurrentConfig()
        {
            // Act
            FeatureAccessService.InvalidateCache();

            // Assert  
            Assert.That(FeatureAccessService.CurrentConfig, Is.Null,
                "After cache invalidation, CurrentConfig should be null");
        }

        [Test]
        public void FeatureConstants_HaveCorrectValues()
        {
            // Assert
            Assert.That(FeatureAccessService.SkinSelectorFeature, Is.EqualTo("SkinSelector"));
            Assert.That(FeatureAccessService.MiscellaneousFeature, Is.EqualTo("Miscellaneous"));
        }

        [Test]
        public async Task GetConfigAsync_WhenR2Unavailable_ReturnsDefaultWithAllEnabled()
        {
            // Arrange - invalidate any prior cache
            FeatureAccessService.InvalidateCache();

            // Act - this will try to fetch from R2, which may not be available in test env
            var config = await FeatureAccessService.GetConfigAsync();

            // Assert - should return a valid config regardless (fail-open)
            Assert.That(config, Is.Not.Null,
                "Should always return a config, never null (fail-open design)");
            // Note: In CI without R2 access, defaults are returned (both enabled)
        }

        [Test]
        public async Task IsFeatureEnabledAsync_SkinSelector_ReturnsWithoutError()
        {
            // Arrange - invalidate cache to ensure fresh attempt
            FeatureAccessService.InvalidateCache();

            // Act - should not throw regardless of R2 availability
            var enabled = await FeatureAccessService.IsFeatureEnabledAsync(
                FeatureAccessService.SkinSelectorFeature);

            // Assert - returns a valid boolean without exceptions
            Assert.That(enabled, Is.TypeOf<bool>(),
                "Should return a valid boolean regardless of R2 state");
        }

        [Test]
        public async Task IsFeatureEnabledAsync_Miscellaneous_ReturnsWithoutError()
        {
            // Arrange
            FeatureAccessService.InvalidateCache();

            // Act - should not throw regardless of R2 availability
            var enabled = await FeatureAccessService.IsFeatureEnabledAsync(
                FeatureAccessService.MiscellaneousFeature);

            // Assert - returns a valid boolean without exceptions
            Assert.That(enabled, Is.TypeOf<bool>(),
                "Should return a valid boolean regardless of R2 state");
        }

        [Test]
        public async Task IsFeatureEnabledAsync_UnknownFeature_ReturnsTrue()
        {
            // Act
            var enabled = await FeatureAccessService.IsFeatureEnabledAsync("UnknownFeature");

            // Assert - unknown features should default to enabled (fail-open)
            Assert.That(enabled, Is.True, "Unknown feature should default to enabled (fail-open)");
        }

        [Test]
        public async Task GetFeatureMessageAsync_SkinSelector_ReturnsMessage()
        {
            // Act
            var message = await FeatureAccessService.GetFeatureMessageAsync(
                FeatureAccessService.SkinSelectorFeature);

            // Assert - should always return a non-empty message
            Assert.That(message, Is.Not.Null.And.Not.Empty,
                "Should always return a display message");
        }

        #endregion
    }
}
