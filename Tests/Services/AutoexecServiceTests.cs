using System.Collections.Generic;
using ArdysaModsTools.Core.Services.Misc;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services.Misc
{
    [TestFixture]
    public class AutoexecServiceTests
    {
        #region ParseAutoexec Tests

        [Test]
        public void ParseAutoexec_GivenValidLines_ParsesCorrectly()
        {
            var lines = new[]
            {
                "fps_max 144",
                "r_fullscreen_gamma 2.2"
            };

            var result = AutoexecService.ParseAutoexec(lines);

            Assert.That(result, Contains.Key("fps_max"));
            Assert.That(result["fps_max"], Is.EqualTo("144"));
            Assert.That(result, Contains.Key("r_fullscreen_gamma"));
            Assert.That(result["r_fullscreen_gamma"], Is.EqualTo("2.2"));
        }

        [Test]
        public void ParseAutoexec_IgnoresComments()
        {
            var lines = new[]
            {
                "// This is a comment",
                "fps_max 144 // max fps",
                "  // another comment",
                "r_fullscreen_gamma 2.2"
            };

            var result = AutoexecService.ParseAutoexec(lines);

            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result["fps_max"], Is.EqualTo("144"));
            Assert.That(result["r_fullscreen_gamma"], Is.EqualTo("2.2"));
        }

        [Test]
        public void ParseAutoexec_IgnoresAliases()
        {
            var lines = new[]
            {
                "alias myalias \"echo hello\"",
                "fps_max 144"
            };

            var result = AutoexecService.ParseAutoexec(lines);

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result, Contains.Key("fps_max"));
            Assert.That(result.ContainsKey("alias"), Is.False);
            Assert.That(result.ContainsKey("myalias"), Is.False);
        }

        [Test]
        public void ParseAutoexec_HandlesExtraWhitespace()
        {
            var lines = new[]
            {
                "   fps_max    144   ",
                "",
                "	r_fullscreen_gamma		2.2"
            };

            var result = AutoexecService.ParseAutoexec(lines);

            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result["fps_max"], Is.EqualTo("144"));
            Assert.That(result["r_fullscreen_gamma"], Is.EqualTo("2.2"));
        }

        [Test]
        public void ParseAutoexec_IsCaseInsensitive()
        {
            var lines = new[]
            {
                "FPS_MAX 144",
                "R_FullScreen_Gamma 2.2"
            };

            var result = AutoexecService.ParseAutoexec(lines);

            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result["fps_max"], Is.EqualTo("144")); // We should be able to look it up in lowercase
            Assert.That(result["r_fullscreen_gamma"], Is.EqualTo("2.2"));
        }

        [Test]
        public void ParseAutoexec_OverwritesDuplicatesWithLastValue()
        {
            var lines = new[]
            {
                "fps_max 60",
                "fps_max 144"
            };

            var result = AutoexecService.ParseAutoexec(lines);

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result["fps_max"], Is.EqualTo("144"));
        }

        [Test]
        public void ParseAutoexec_HandlesInvalidLinesGracefully()
        {
            var lines = new[]
            {
                "fps_max", // missing value
                "123 invalid", // starts with number
                "!@# bad", // starts with symbol
                "fps_max 144"
            };

            var result = AutoexecService.ParseAutoexec(lines);

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result["fps_max"], Is.EqualTo("144"));
        }

        #endregion

        #region GenerateAutoexecContent Tests

        [Test]
        public void GenerateAutoexecContent_ContainsHeaderAndFooter()
        {
            var settings = new Dictionary<string, string>();

            var result = AutoexecService.GenerateAutoexecContent(settings);

            Assert.That(result, Does.Contain("DOTA 2 AUTOEXEC.CFG"));
            Assert.That(result, Does.Contain("End of ArdysaModsTools autoexec.cfg"));
        }

        [Test]
        public void GenerateAutoexecContent_FormatsKnownCvarsIntoCategories()
        {
            var settings = new Dictionary<string, string>
            {
                { "fps_max", "144" },
                { "cl_updaterate", "30" }
            };

            var result = AutoexecService.GenerateAutoexecContent(settings);

            Assert.That(result, Does.Contain("── DISPLAY & FPS ──"));
            Assert.That(result, Does.Contain("fps_max 144"));
            Assert.That(result, Does.Contain("── NETWORK ──"));
            Assert.That(result, Does.Contain("cl_updaterate 30"));
        }

        [Test]
        public void GenerateAutoexecContent_PutsUnknownCvarsInOtherCategory()
        {
            var settings = new Dictionary<string, string>
            {
                { "unknown_cvar", "value" }
            };

            var result = AutoexecService.GenerateAutoexecContent(settings);

            Assert.That(result, Does.Contain("── OTHER ──"));
            Assert.That(result, Does.Contain("unknown_cvar value"));
        }

        [Test]
        public void GenerateAutoexecContent_CaseInsensitiveMatching()
        {
            var settings = new Dictionary<string, string>
            {
                { "FPS_MAX", "144" }
            };

            var result = AutoexecService.GenerateAutoexecContent(settings);

            Assert.That(result, Does.Contain("fps_max 144"));
        }

        [Test]
        public void RoundTrip_ParseAndGenerate_PreservesData()
        {
            var original = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                { "fps_max", "144" },
                { "r_dota_fxaa", "1" },
                { "custom_cvar", "test" }
            };

            var content = AutoexecService.GenerateAutoexecContent(original);
            var lines = content.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
            var parsed = AutoexecService.ParseAutoexec(lines);

            Assert.That(parsed["fps_max"], Is.EqualTo("144"));
            Assert.That(parsed["r_dota_fxaa"], Is.EqualTo("1"));
            Assert.That(parsed["custom_cvar"], Is.EqualTo("test"));
        }

        #endregion
    }
}
