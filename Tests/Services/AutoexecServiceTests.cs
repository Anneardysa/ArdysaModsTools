using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Services.FileTransactions;
using ArdysaModsTools.Core.Services.Misc;
using ArdysaModsTools.Tests.Helpers;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services.Misc
{
    [TestFixture]
    public class AutoexecServiceTests
    {
        private string _tempRoot = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "amt_autoexec_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempRoot);
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, true); } catch {  }
        }

        private AutoexecService CreateService(out TestLogger logger)
        {
            logger = new TestLogger();
            return new AutoexecService(logger);
        }

        private string CreateCfgDir()
        {
            var cfgDir = Path.Combine(_tempRoot, "dota", "cfg");
            Directory.CreateDirectory(cfgDir);
            return cfgDir;
        }

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
            Assert.That(result["fps_max"], Is.EqualTo("144"));
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
                "fps_max",
                "123 invalid",
                "!@# bad",
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

        #region LoadCurrentSettingsAsync Tests

        [Test]
        public async Task LoadCurrentSettingsAsync_WhenAutoexecExists_ReturnsParsedSettings()
        {
            var cfgDir = CreateCfgDir();
            await File.WriteAllTextAsync(Path.Combine(cfgDir, "autoexec.cfg"), "fps_max 144\r\nr_ssao 0\r\n");
            var service = CreateService(out _);

            var result = await service.LoadCurrentSettingsAsync(_tempRoot);

            Assert.That(result["fps_max"], Is.EqualTo("144"));
            Assert.That(result["r_ssao"], Is.EqualTo("0"));
        }

        [Test]
        public async Task LoadCurrentSettingsAsync_WhenCfgDirExistsButNoAutoexec_ReturnsEmpty()
        {
            CreateCfgDir();
            var service = CreateService(out _);

            var result = await service.LoadCurrentSettingsAsync(_tempRoot);

            Assert.That(result, Is.Empty);
        }

        [Test]
        public async Task LoadCurrentSettingsAsync_WhenGamePathIsInstallRoot_ReadsGameDotaCfg()
        {
            var cfgDir = Path.Combine(_tempRoot, "game", "dota", "cfg");
            Directory.CreateDirectory(cfgDir);
            await File.WriteAllTextAsync(Path.Combine(cfgDir, "autoexec.cfg"), "fps_max 240\r\n");
            var service = CreateService(out _);

            var result = await service.LoadCurrentSettingsAsync(_tempRoot);

            Assert.That(result["fps_max"], Is.EqualTo("240"));
        }

        #endregion

        #region ApplySettingsAsync Tests

        [Test]
        public async Task ApplySettingsAsync_WritesAutoexecWithGeneratedContent()
        {
            var cfgDir = CreateCfgDir();
            var service = CreateService(out var logger);
            var settings = new Dictionary<string, string> { { "fps_max", "144" } };

            await service.ApplySettingsAsync(_tempRoot, settings);

            var written = await File.ReadAllTextAsync(Path.Combine(cfgDir, "autoexec.cfg"));
            Assert.That(written, Does.Contain("DOTA 2 AUTOEXEC.CFG"));
            Assert.That(written, Does.Contain("fps_max 144"));
            Assert.That(logger.ErrorCount, Is.EqualTo(0));
        }

        [Test]
        public void ApplySettingsAsync_WhenInstallInvalid_ThrowsDirectoryNotFound()
        {
            var service = CreateService(out _);
            var settings = new Dictionary<string, string> { { "fps_max", "144" } };

            Assert.That(
                async () => await service.ApplySettingsAsync(_tempRoot, settings),
                Throws.InstanceOf<DirectoryNotFoundException>());
        }

        [Test]
        public async Task ApplySettingsAsync_WhenCfgFolderMissingButInstallValid_CreatesCfgAndWrites()
        {
            Directory.CreateDirectory(Path.Combine(_tempRoot, "dota"));
            var service = CreateService(out var logger);
            var settings = new Dictionary<string, string> { { "fps_max", "144" } };

            await service.ApplySettingsAsync(_tempRoot, settings);

            var autoexec = Path.Combine(_tempRoot, "dota", "cfg", "autoexec.cfg");
            Assert.That(File.Exists(autoexec), Is.True);
            var written = await File.ReadAllTextAsync(autoexec);
            Assert.That(written, Does.Contain("fps_max 144"));
            Assert.That(logger.ErrorCount, Is.EqualTo(0));
        }

        [Test]
        public async Task ApplySettingsAsync_WhenGamePathIsInstallRoot_WritesUnderGameDotaCfg()
        {
            Directory.CreateDirectory(Path.Combine(_tempRoot, "game", "dota"));
            var service = CreateService(out var logger);
            var settings = new Dictionary<string, string> { { "fps_max", "240" } };

            await service.ApplySettingsAsync(_tempRoot, settings);

            var autoexec = Path.Combine(_tempRoot, "game", "dota", "cfg", "autoexec.cfg");
            Assert.That(File.Exists(autoexec), Is.True);
            var written = await File.ReadAllTextAsync(autoexec);
            Assert.That(written, Does.Contain("fps_max 240"));
            Assert.That(logger.ErrorCount, Is.EqualTo(0));
        }

        [Test]
        public void ApplySettingsAsync_WhenWriteFails_SurfacesErrorAndLeavesFolderIntact()
        {
            var cfgDir = CreateCfgDir();
            var blockingDir = Path.Combine(cfgDir, "autoexec.cfg");
            Directory.CreateDirectory(blockingDir);
            var service = CreateService(out var logger);
            var settings = new Dictionary<string, string> { { "fps_max", "144" } };

            Assert.That(
                async () => await service.ApplySettingsAsync(_tempRoot, settings),
                Throws.Exception);

            Assert.That(Directory.Exists(blockingDir), Is.True);
            Assert.That(logger.HasLogContaining("Transaction failed"), Is.True);
        }

        #endregion

        #region ExportCfgAsync Tests

        [Test]
        public async Task ExportCfgAsync_WritesGeneratedContentToTargetPath()
        {
            var service = CreateService(out _);
            var exportPath = Path.Combine(_tempRoot, "exported_autoexec.cfg");
            var settings = new Dictionary<string, string> { { "fps_max", "240" } };

            await service.ExportCfgAsync(exportPath, settings);

            var written = await File.ReadAllTextAsync(exportPath);
            Assert.That(written, Does.Contain("fps_max 240"));
            Assert.That(written, Does.Contain("End of ArdysaModsTools autoexec.cfg"));
        }

        #endregion

        #region DeleteCfgAsync Tests

        [Test]
        public async Task DeleteCfgAsync_WhenFileExists_DeletesAndReturnsTrue()
        {
            var cfgDir = CreateCfgDir();
            var autoexecPath = Path.Combine(cfgDir, "autoexec.cfg");
            await File.WriteAllTextAsync(autoexecPath, "fps_max 144");
            var service = CreateService(out _);

            var deleted = await service.DeleteCfgAsync(_tempRoot);

            Assert.That(deleted, Is.True);
            Assert.That(File.Exists(autoexecPath), Is.False);
        }

        [Test]
        public async Task DeleteCfgAsync_WhenNoFile_ReturnsFalse()
        {
            CreateCfgDir();
            var service = CreateService(out _);

            var deleted = await service.DeleteCfgAsync(_tempRoot);

            Assert.That(deleted, Is.False);
        }

        #endregion
    }
}
