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
using ArdysaModsTools.UI.Forms;

namespace ArdysaModsTools.Tests.Forms
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class Dota2PerformanceFormTests
    {
        #region ParseAutoexec Tests

        [Test]
        public void ParseAutoexec_WithEmptyLines_ReturnsEmptyDictionary()
        {
            // Arrange
            var lines = Array.Empty<string>();

            // Act
            var result = Dota2PerformanceForm.ParseAutoexec(lines);

            // Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void ParseAutoexec_WithCommentOnlyLines_SkipsComments()
        {
            // Arrange
            var lines = new[]
            {
                "// This is a comment",
                "// Another comment",
                "",
                "   "
            };

            // Act
            var result = Dota2PerformanceForm.ParseAutoexec(lines);

            // Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void ParseAutoexec_WithStandardCvars_ParsesCorrectly()
        {
            // Arrange
            var lines = new[]
            {
                "fps_max 120",
                "cl_showfps 1",
                "r_drawparticles 0"
            };

            // Act
            var result = Dota2PerformanceForm.ParseAutoexec(lines);

            // Assert
            Assert.That(result, Has.Count.EqualTo(3));
            Assert.That(result["fps_max"], Is.EqualTo("120"));
            Assert.That(result["cl_showfps"], Is.EqualTo("1"));
            Assert.That(result["r_drawparticles"], Is.EqualTo("0"));
        }

        [Test]
        public void ParseAutoexec_WithInlineComments_StripsComments()
        {
            // Arrange
            var lines = new[]
            {
                "fps_max 144 // cap framerate",
                "rate 80000 // network rate"
            };

            // Act
            var result = Dota2PerformanceForm.ParseAutoexec(lines);

            // Assert
            Assert.That(result["fps_max"], Is.EqualTo("144"));
            Assert.That(result["rate"], Is.EqualTo("80000"));
        }

        [Test]
        public void ParseAutoexec_WithTabSeparators_HandlesCorrectly()
        {
            // Arrange
            var lines = new[]
            {
                "fps_max\t120",
                "cl_showfps\t\t1"
            };

            // Act
            var result = Dota2PerformanceForm.ParseAutoexec(lines);

            // Assert
            Assert.That(result["fps_max"], Is.EqualTo("120"));
            Assert.That(result["cl_showfps"], Is.EqualTo("1"));
        }

        [Test]
        public void ParseAutoexec_WithSingleTokenLines_SkipsThem()
        {
            // Arrange
            var lines = new[]
            {
                "fps_max 120",
                "singletoken",
                "cl_showfps 1"
            };

            // Act
            var result = Dota2PerformanceForm.ParseAutoexec(lines);

            // Assert
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result.ContainsKey("singletoken"), Is.False);
        }

        [Test]
        public void ParseAutoexec_WithDuplicateCvars_LastValueWins()
        {
            // Arrange
            var lines = new[]
            {
                "fps_max 60",
                "fps_max 120",
                "fps_max 240"
            };

            // Act
            var result = Dota2PerformanceForm.ParseAutoexec(lines);

            // Assert
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result["fps_max"], Is.EqualTo("240"));
        }

        [Test]
        public void ParseAutoexec_WithAliasLines_SkipsThem()
        {
            // Arrange
            var lines = new[]
            {
                "alias \"test\" \"command\"",
                "fps_max 120"
            };

            // Act
            var result = Dota2PerformanceForm.ParseAutoexec(lines);

            // Assert
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result.ContainsKey("alias"), Is.False);
        }

        #endregion

        #region GenerateAutoexecContent Tests

        [Test]
        public void GenerateAutoexecContent_WithEmptySettings_ReturnsHeaderOnly()
        {
            // Arrange
            var settings = new Dictionary<string, string>();

            // Act
            var result = Dota2PerformanceForm.GenerateAutoexecContent(settings);

            // Assert
            Assert.That(result, Does.Contain("ArdysaModsTools Performance Tweak"));
            Assert.That(result, Does.Contain("Generated"));
        }

        [Test]
        public void GenerateAutoexecContent_WithKnownCvars_GroupsUnderCategories()
        {
            // Arrange
            var settings = new Dictionary<string, string>
            {
                { "fps_max", "120" },
                { "r_drawparticles", "1" }
            };

            // Act
            var result = Dota2PerformanceForm.GenerateAutoexecContent(settings);

            // Assert
            Assert.That(result, Does.Contain("fps_max"));
            Assert.That(result, Does.Contain("120"));
            Assert.That(result, Does.Contain("r_drawparticles"));
        }

        [Test]
        public void GenerateAutoexecContent_WithUnknownCvars_GoesToOtherSection()
        {
            // Arrange
            var settings = new Dictionary<string, string>
            {
                { "my_custom_cvar", "42" }
            };

            // Act
            var result = Dota2PerformanceForm.GenerateAutoexecContent(settings);

            // Assert
            Assert.That(result, Does.Contain("OTHER"));
            Assert.That(result, Does.Contain("my_custom_cvar 42"));
        }

        [Test]
        public void GenerateAutoexecContent_ContainsTimestamp()
        {
            // Arrange
            var settings = new Dictionary<string, string>
            {
                { "fps_max", "120" }
            };

            // Act
            var result = Dota2PerformanceForm.GenerateAutoexecContent(settings);

            // Assert
            Assert.That(result, Does.Contain("Generated by ArdysaModsTools"));
        }

        [Test]
        public void GenerateAutoexecContent_WithMixedKnownAndUnknown_SeparatesSections()
        {
            // Arrange
            var settings = new Dictionary<string, string>
            {
                { "fps_max", "144" },
                { "custom_thing", "abc" },
                { "r_drawparticles", "0" }
            };

            // Act
            var result = Dota2PerformanceForm.GenerateAutoexecContent(settings);

            // Assert
            Assert.That(result, Does.Contain("fps_max 144"));
            Assert.That(result, Does.Contain("r_drawparticles 0"));
            Assert.That(result, Does.Contain("custom_thing abc"));
        }

        [Test]
        public void ParseAutoexec_RoundTrip_PreservesValues()
        {
            // Arrange — generate then parse, values should survive
            var original = new Dictionary<string, string>
            {
                { "fps_max", "144" },
                { "cl_showfps", "1" },
                { "r_drawparticles", "0" },
                { "rate", "80000" }
            };

            // Act
            var content = Dota2PerformanceForm.GenerateAutoexecContent(original);
            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var parsed = Dota2PerformanceForm.ParseAutoexec(lines);

            // Assert
            foreach (var kvp in original)
            {
                Assert.That(parsed.ContainsKey(kvp.Key), Is.True, $"Missing key: {kvp.Key}");
                Assert.That(parsed[kvp.Key], Is.EqualTo(kvp.Value), $"Value mismatch for {kvp.Key}");
            }
        }

        #endregion
    }
}
