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
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Misc;
using Moq;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services;

[TestFixture]
public class MiscProtectedVpkIntegrationTests
{
    private string _root = null!;
    private string _targetPath = null!;
    private string _vpkStub = null!;
    private string _hlExtractStub = null!;

    [SetUp]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "AmtMiscProtTest_" + Guid.NewGuid().ToString("N"));
        _targetPath = Path.Combine(_root, "dota");
        Directory.CreateDirectory(_targetPath);

        string gameInfoDir = Path.Combine(_targetPath, "game", "dota");
        Directory.CreateDirectory(gameInfoDir);
        string gameInfoPath = Path.Combine(gameInfoDir, "gameinfo_branchspecific.gi");
        File.WriteAllText(gameInfoPath, "\t\t\tGame\t\t\t\t_ArdysaMods\r\n\t\t\tGame\t\t\t\tmod\r\n\t\t\tGame\t\t\t\tdota\r\n");

        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _vpkStub = Path.Combine(baseDir, "vpk.exe");
        _hlExtractStub = Path.Combine(baseDir, "HLExtract.exe");
        if (!File.Exists(_vpkStub)) File.WriteAllText(_vpkStub, "stub");
        if (!File.Exists(_hlExtractStub)) File.WriteAllText(_hlExtractStub, "stub");
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            var protectedVpk = ProtectedVpkStore.VpkPath(_targetPath);
            if (File.Exists(protectedVpk)) File.SetAttributes(protectedVpk, FileAttributes.Normal);
            var dir = ProtectedVpkStore.Dir(_targetPath);
            if (Directory.Exists(dir)) File.SetAttributes(dir, FileAttributes.Normal);
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
        catch { }
    }

    private sealed class FakeAssetModifier : IAssetModifier
    {
        public List<string> ProtectedPathsToReturn { get; } = new();
        public Dictionary<string, List<string>> InstalledFilesToReturn { get; } = new();
        public List<string> WarningsToReturn { get; } = new();
        public HashSet<string> ModifiedItemIdsToReturn { get; } = new();
        public HashSet<string> UnpatchedItemIdsToReturn { get; } = new();
        public Action<string, string>? OnApply { get; set; }

        public Task<bool> ApplyModificationsAsync(string vpkPath, string extractDir,
            Dictionary<string, string> selections, Action<string> log,
            CancellationToken ct = default,
            IProgress<SpeedMetrics>? speedProgress = null)
        {
            OnApply?.Invoke(vpkPath, extractDir);
            return Task.FromResult(true);
        }

        public IReadOnlyCollection<string> GetProtectedPaths() => ProtectedPathsToReturn;
        public Dictionary<string, List<string>> GetInstalledFiles() => InstalledFilesToReturn;
        public List<string> GetWarnings() => WarningsToReturn;
        public IReadOnlyCollection<string> GetModifiedItemIds() => ModifiedItemIdsToReturn;
        public IReadOnlyCollection<string> GetUnpatchedItemIds() => UnpatchedItemIdsToReturn;
        public void SetPreviousLog(MiscExtractionLog? log) { }
    }

    [Test]
    public async Task MiscCleanGenerationService_WithProtectedAssets_BuildsAndDeploysBothPackages()
    {
        string baseExtractDir = Path.Combine(_root, "original_extract");
        Directory.CreateDirectory(Path.Combine(baseExtractDir, "scripts", "items"));
        File.WriteAllText(Path.Combine(baseExtractDir, "scripts", "items", "items_game.txt"), "\"items_game\" { \"items\" { } }");

        var mockOriginalProvider = new Mock<IOriginalVpkProvider>();
        mockOriginalProvider
            .Setup(p => p.GetExtractedOriginalAsync(It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<SpeedMetrics>?>(), It.IsAny<IProgress<int>?>()))
            .ReturnsAsync(baseExtractDir);

        var mockItemsGameExtractor = new Mock<IGameItemsGameExtractor>();
        mockItemsGameExtractor
            .Setup(e => e.RefreshFromGameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var fakeModifier = new FakeAssetModifier();
        fakeModifier.ProtectedPathsToReturn.Add("models/roshan/roshan.vmdl_c");
        fakeModifier.OnApply = (vpk, extract) =>
        {
            string modelPath = Path.Combine(extract, "models", "roshan", "roshan.vmdl_c");
            Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
            File.WriteAllText(modelPath, "model_data");
        };

        var mockRecompiler = new Mock<IVpkRecompiler>();
        mockRecompiler
            .Setup(r => r.RecompileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<SpeedMetrics>?>()))
            .Returns<string, string, string, string, Action<string>, CancellationToken, IProgress<SpeedMetrics>?>((tool, inputDir, build, temp, l, ct, sp) =>
            {
                string dummyVpk = Path.Combine(temp, Path.GetFileName(inputDir) + ".vpk");
                File.WriteAllBytes(dummyVpk, new byte[] { 1, 2, 3, 4 });
                return Task.FromResult<string?>(dummyVpk);
            });

        var mockReplacer = new Mock<IVpkReplacer>();
        mockReplacer
            .Setup(r => r.ReplaceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new MiscCleanGenerationService(
            mockOriginalProvider.Object,
            fakeModifier,
            mockRecompiler.Object,
            mockReplacer.Object,
            mockItemsGameExtractor.Object);

        var selections = new Dictionary<string, string> { { "Roshan", "Golden" } };
        var result = await service.GenerateCleanAsync(_targetPath, selections, _ => { });

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            mockRecompiler.Verify(r => r.RecompileAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<SpeedMetrics>?>()), Times.Exactly(2));

            Assert.That(File.Exists(ProtectedVpkStore.VpkPath(_targetPath)), Is.True);
        });
    }

    [Test]
    public async Task MiscGenerationService_AddToCurrent_WithExistingProtectedHero_PreservesAndMergesProtectedMisc()
    {
        string modsDir = Path.Combine(_targetPath, "game", "_ArdysaMods");
        Directory.CreateDirectory(modsDir);
        string mainVpk = Path.Combine(modsDir, "pak01_dir.vpk");
        File.WriteAllBytes(mainVpk, new byte[] { 1, 2, 3 });

        string protDir = ProtectedVpkStore.Dir(_targetPath);
        Directory.CreateDirectory(protDir);
        string protVpk = ProtectedVpkStore.VpkPath(_targetPath);
        File.WriteAllBytes(protVpk, new byte[] { 4, 5, 6 });

        var mockExtractor = new Mock<IVpkExtractor>();
        mockExtractor
            .Setup(e => e.ExtractAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<SpeedMetrics>?>(), It.IsAny<bool>()))
            .Returns<string, string, string, Action<string>, CancellationToken, IProgress<SpeedMetrics>?, bool>((tool, vpk, destDir, l, ct, sp, req) =>
            {
                if (vpk == mainVpk)
                {
                    Directory.CreateDirectory(Path.Combine(destDir, "scripts", "items"));
                    File.WriteAllText(Path.Combine(destDir, "scripts", "items", "items_game.txt"), "\"items_game\" { \"items\" { } }");
                }
                else if (vpk == protVpk)
                {
                    Directory.CreateDirectory(Path.Combine(destDir, "models", "heroes", "axe"));
                    File.WriteAllText(Path.Combine(destDir, "models", "heroes", "axe", "axe.vmdl_c"), "axe_model");
                }
                return Task.FromResult(true);
            });

        var fakeModifier = new FakeAssetModifier();
        fakeModifier.ProtectedPathsToReturn.Add("models/roshan/roshan.vmdl_c");
        fakeModifier.OnApply = (vpk, extract) =>
        {
            string modelPath = Path.Combine(extract, "models", "roshan", "roshan.vmdl_c");
            Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
            File.WriteAllText(modelPath, "roshan_model");
        };

        var recompiledInputs = new List<string>();
        bool heroModelFoundInProtected = false;
        bool miscModelFoundInProtected = false;

        var mockRecompiler = new Mock<IVpkRecompiler>();
        mockRecompiler
            .Setup(r => r.RecompileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<SpeedMetrics>?>()))
            .Returns<string, string, string, string, Action<string>, CancellationToken, IProgress<SpeedMetrics>?>((tool, inputDir, build, temp, l, ct, sp) =>
            {
                recompiledInputs.Add(inputDir);
                if (inputDir.EndsWith("protected"))
                {
                    heroModelFoundInProtected = File.Exists(Path.Combine(inputDir, "models", "heroes", "axe", "axe.vmdl_c"));
                    miscModelFoundInProtected = File.Exists(Path.Combine(inputDir, "models", "roshan", "roshan.vmdl_c"));
                }
                string dummyVpk = Path.Combine(temp, Path.GetFileName(inputDir) + ".vpk");
                File.WriteAllBytes(dummyVpk, new byte[] { 1, 2, 3, 4 });
                return Task.FromResult<string?>(dummyVpk);
            });

        var mockReplacer = new Mock<IVpkReplacer>();
        mockReplacer
            .Setup(r => r.ReplaceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new MiscGenerationService(
            mockExtractor.Object,
            fakeModifier,
            mockRecompiler.Object,
            mockReplacer.Object);

        var selections = new Dictionary<string, string> { { "Roshan", "Golden" } };
        var result = await service.PerformGenerationAsync(_targetPath, selections, _ => { });

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(recompiledInputs, Has.Count.EqualTo(2));

            Assert.That(heroModelFoundInProtected, Is.True, "Hero model preserved");
            Assert.That(miscModelFoundInProtected, Is.True, "Misc model added to protected package");

            Assert.That(File.Exists(ProtectedVpkStore.VpkPath(_targetPath)), Is.True);
        });
    }

    [Test]
    public async Task MiscGenerationService_AddToCurrent_WithNoProtectedAssets_RemovesProtectedPackage()
    {
        string modsDir = Path.Combine(_targetPath, "game", "_ArdysaMods");
        Directory.CreateDirectory(modsDir);
        string mainVpk = Path.Combine(modsDir, "pak01_dir.vpk");
        File.WriteAllBytes(mainVpk, new byte[] { 1, 2, 3 });

        string protDir = ProtectedVpkStore.Dir(_targetPath);
        Directory.CreateDirectory(protDir);
        string protVpk = ProtectedVpkStore.VpkPath(_targetPath);
        File.WriteAllBytes(protVpk, new byte[] { 4, 5, 6 });

        var mockExtractor = new Mock<IVpkExtractor>();
        mockExtractor
            .Setup(e => e.ExtractAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<SpeedMetrics>?>(), It.IsAny<bool>()))
            .Returns<string, string, string, Action<string>, CancellationToken, IProgress<SpeedMetrics>?, bool>((tool, vpk, destDir, l, ct, sp, req) =>
            {
                if (vpk == mainVpk)
                {
                    Directory.CreateDirectory(Path.Combine(destDir, "scripts", "items"));
                    File.WriteAllText(Path.Combine(destDir, "scripts", "items", "items_game.txt"), "\"items_game\" { \"items\" { } }");
                }
                return Task.FromResult(true);
            });

        var previousLog = new MiscExtractionLog
        {
            Mode = "AddToCurrent"
        };
        previousLog.AddFiles("Roshan", new List<string> { "models/roshan/roshan.vmdl_c" });
        previousLog.Save(_targetPath);

        mockExtractor
            .Setup(e => e.ExtractAsync(It.IsAny<string>(), protVpk, It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<SpeedMetrics>?>(), It.IsAny<bool>()))
            .Callback<string, string, string, Action<string>, CancellationToken, IProgress<SpeedMetrics>?, bool>((tool, vpk, destDir, l, ct, sp, req) =>
            {
                string oldFile = Path.Combine(destDir, "models", "roshan", "roshan.vmdl_c");
                Directory.CreateDirectory(Path.GetDirectoryName(oldFile)!);
                File.WriteAllText(oldFile, "old_misc_data");
            })
            .ReturnsAsync(true);

        var fakeModifier = new FakeAssetModifier();

        var mockRecompiler = new Mock<IVpkRecompiler>();
        mockRecompiler
            .Setup(r => r.RecompileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<SpeedMetrics>?>()))
            .Returns<string, string, string, string, Action<string>, CancellationToken, IProgress<SpeedMetrics>?>((tool, inputDir, build, temp, l, ct, sp) =>
            {
                string dummyVpk = Path.Combine(temp, Path.GetFileName(inputDir) + ".vpk");
                File.WriteAllBytes(dummyVpk, new byte[] { 1, 2, 3, 4 });
                return Task.FromResult<string?>(dummyVpk);
            });

        var mockReplacer = new Mock<IVpkReplacer>();
        mockReplacer
            .Setup(r => r.ReplaceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new MiscGenerationService(
            mockExtractor.Object,
            fakeModifier,
            mockRecompiler.Object,
            mockReplacer.Object);

        var selections = new Dictionary<string, string> { { "Roshan", "Default Roshan" } };
        var result = await service.PerformGenerationAsync(_targetPath, selections, _ => { });

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(File.Exists(ProtectedVpkStore.VpkPath(_targetPath)), Is.False);
        });
    }

    [Test]
    public async Task MiscGenerationService_AddToCurrent_ExtractsDirectVpk_AndPatchesGameFiles()
    {
        string modsDir = Path.Combine(_targetPath, "game", "_ArdysaMods");
        Directory.CreateDirectory(modsDir);
        string mainVpk = Path.Combine(modsDir, "pak01_dir.vpk");
        File.WriteAllBytes(mainVpk, new byte[] { 0x55, 0xaa, 0x12, 0x34, 0x02, 0x00, 0x00, 0x00, 1, 2, 3, 4 });

        string binDir = Path.Combine(_targetPath, "game", "bin", "win64");
        Directory.CreateDirectory(binDir);
        string sigPath = Path.Combine(binDir, "dota.signatures");
        File.WriteAllLines(sigPath, new[] { "GAME_SIGNATURES_HEADER", "DIGEST:12345", "SOME_OLD_LINE" });

        var mockExtractor = new Mock<IVpkExtractor>();
        string? extractedVpkSource = null;
        mockExtractor
            .Setup(e => e.ExtractAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<SpeedMetrics>?>(), It.IsAny<bool>()))
            .Returns<string, string, string, Action<string>, CancellationToken, IProgress<SpeedMetrics>?, bool>((tool, vpk, destDir, l, ct, sp, req) =>
            {
                extractedVpkSource = vpk;
                Directory.CreateDirectory(Path.Combine(destDir, "scripts", "items"));
                File.WriteAllText(Path.Combine(destDir, "scripts", "items", "items_game.txt"), "\"items_game\" { \"items\" { } }");
                return Task.FromResult(true);
            });

        var fakeModifier = new FakeAssetModifier();
        var mockRecompiler = new Mock<IVpkRecompiler>();
        mockRecompiler
            .Setup(r => r.RecompileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<SpeedMetrics>?>()))
            .Returns<string, string, string, string, Action<string>, CancellationToken, IProgress<SpeedMetrics>?>((tool, inputDir, build, temp, l, ct, sp) =>
            {
                string dummyVpk = Path.Combine(temp, Path.GetFileName(inputDir) + ".vpk");
                File.WriteAllBytes(dummyVpk, new byte[] { 1, 2, 3, 4 });
                return Task.FromResult<string?>(dummyVpk);
            });

        var mockReplacer = new Mock<IVpkReplacer>();
        mockReplacer
            .Setup(r => r.ReplaceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new MiscGenerationService(
            mockExtractor.Object,
            fakeModifier,
            mockRecompiler.Object,
            mockReplacer.Object);

        var selections = new Dictionary<string, string> { { "Weather", "Ash" } };
        var result = await service.PerformGenerationAsync(_targetPath, selections, _ => { });

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(extractedVpkSource, Is.EqualTo(mainVpk), "Should extract direct pak01_dir.vpk");
            
            string[] sigLines = File.ReadAllLines(sigPath);
            Assert.That(sigLines, Does.Contain(ModConstants.ModPatchLine));
        });
    }
}
