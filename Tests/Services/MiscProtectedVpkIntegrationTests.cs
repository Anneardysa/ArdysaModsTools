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
using ArdysaModsTools.Tests.Helpers;

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
                File.WriteAllBytes(dummyVpk, TestVpk.Minimal());
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
                File.WriteAllBytes(dummyVpk, TestVpk.Minimal());
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
    public async Task MiscGenerationService_AddToCurrent_WithNoProtectedAssets_DropsStaleAssetsButKeepsPackageText()
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

        List<string>? protectedTree = null;

        var mockRecompiler = new Mock<IVpkRecompiler>();
        mockRecompiler
            .Setup(r => r.RecompileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<SpeedMetrics>?>()))
            .Returns<string, string, string, string, Action<string>, CancellationToken, IProgress<SpeedMetrics>?>((tool, inputDir, build, temp, l, ct, sp) =>
            {
                if (Path.GetFileName(inputDir) == "protected")
                {
                    protectedTree = new List<string>();
                    foreach (var f in Directory.GetFiles(inputDir, "*", SearchOption.AllDirectories))
                        protectedTree.Add(Path.GetRelativePath(inputDir, f).Replace(Path.DirectorySeparatorChar, '/'));
                }

                string dummyVpk = Path.Combine(temp, Path.GetFileName(inputDir) + ".vpk");
                File.WriteAllBytes(dummyVpk, TestVpk.Minimal());
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
            Assert.That(File.Exists(ProtectedVpkStore.VpkPath(_targetPath)), Is.True);
            Assert.That(protectedTree, Is.Not.Null, "the protected package was rebuilt");
            Assert.That(protectedTree, Does.Not.Contain("models/roshan/roshan.vmdl_c"),
                "the disabled mod's asset must leave the protected package");
            Assert.That(protectedTree, Contains.Item("scripts/items/items_game.txt"),
                "the item data must stay in the protected package");
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
                File.WriteAllBytes(dummyVpk, TestVpk.Minimal());
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

    private const string ItemDataPath = "scripts/items/items_game.txt";
    private const string LocalizationPath = "resource/localization/dota_english.txt";

    private static List<string> RelativeFiles(string dir) =>
        Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(dir, f).Replace(Path.DirectorySeparatorChar, '/'))
            .ToList();

    private string MakePristineBase()
    {
        string baseDir = Path.Combine(_root, "original_extract");
        Directory.CreateDirectory(Path.Combine(baseDir, "scripts", "items"));
        Directory.CreateDirectory(Path.Combine(baseDir, "resource", "localization"));
        Directory.CreateDirectory(Path.Combine(baseDir, "panorama", "layout"));
        File.WriteAllText(Path.Combine(baseDir, "scripts", "items", "items_game.txt"), "\"items_game\" { \"items\" { } }");
        File.WriteAllText(Path.Combine(baseDir, "resource", "localization", "dota_english.txt"), "\"lang\" { \"Tokens\" { } }");
        File.WriteAllText(Path.Combine(baseDir, "panorama", "layout", "hud.vxml_c"), "hud");
        return baseDir;
    }

    private static Mock<IOriginalVpkProvider> ProviderFor(string baseDir)
    {
        var provider = new Mock<IOriginalVpkProvider>();
        provider
            .Setup(p => p.GetExtractedOriginalAsync(It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<SpeedMetrics>?>(), It.IsAny<IProgress<int>?>()))
            .ReturnsAsync(baseDir);
        return provider;
    }

    private static Mock<IGameItemsGameExtractor> RefreshingExtractor()
    {
        var mock = new Mock<IGameItemsGameExtractor>();
        mock.Setup(e => e.RefreshFromGameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return mock;
    }

    private static Mock<IVpkRecompiler> RecordingRecompiler(
        Dictionary<string, List<string>> trees,
        Func<string, byte[]>? bytesFor = null)
    {
        var mock = new Mock<IVpkRecompiler>();
        mock.Setup(r => r.RecompileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<SpeedMetrics>?>()))
            .Returns<string, string, string, string, Action<string>, CancellationToken, IProgress<SpeedMetrics>?>((tool, inputDir, build, temp, l, ct, sp) =>
            {
                string key = Path.GetFileName(inputDir);
                trees[key] = RelativeFiles(inputDir);
                string dummyVpk = Path.Combine(temp, key + ".vpk");
                File.WriteAllBytes(dummyVpk, bytesFor?.Invoke(key) ?? TestVpk.Minimal());
                return Task.FromResult<string?>(dummyVpk);
            });
        return mock;
    }

    private static Mock<IVpkReplacer> SucceedingReplacer()
    {
        var mock = new Mock<IVpkReplacer>();
        mock.Setup(r => r.ReplaceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return mock;
    }

    [Test]
    public async Task MiscClean_MainPackageCarriesNoItemDataOrLocalization_AndTheProtectedOneCarriesBoth()
    {
        string baseDir = MakePristineBase();
        var trees = new Dictionary<string, List<string>>();

        byte[] protectedBytes = TestVpk.Build(
            TestVpk.ItemsGame("\"items_game\"\n{\n}\n"),
            TestVpk.Blob("resource/localization", "dota_english", "txt", 64));
        byte[]? protectedOnDiskWhenMainSwapped = null;

        var mockReplacer = new Mock<IVpkReplacer>();
        mockReplacer
            .Setup(r => r.ReplaceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                string prot = ProtectedVpkStore.VpkPath(_targetPath);
                protectedOnDiskWhenMainSwapped = File.Exists(prot) ? File.ReadAllBytes(prot) : null;
                return Task.FromResult(true);
            });

        var service = new MiscCleanGenerationService(
            ProviderFor(baseDir).Object,
            new FakeAssetModifier(),
            RecordingRecompiler(trees, key => key == "protected" ? protectedBytes : TestVpk.Minimal()).Object,
            mockReplacer.Object,
            RefreshingExtractor().Object);

        var result = await service.GenerateCleanAsync(_targetPath, new Dictionary<string, string> { { "Roshan", "Golden" } }, _ => { });

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);

            Assert.That(trees["base"], Does.Not.Contain(ItemDataPath),
                "the copyable _ArdysaMods package must not carry the item data");
            Assert.That(trees["base"].Where(f => f.StartsWith("resource/localization")), Is.Empty,
                "the copyable _ArdysaMods package must not carry the mod localization");
            Assert.That(trees["base"], Contains.Item("panorama/layout/hud.vxml_c"),
                "everything that is NOT package text stays in the main package");

            Assert.That(trees["protected"], Contains.Item(ItemDataPath));
            Assert.That(trees["protected"], Contains.Item(LocalizationPath));

            Assert.That(protectedOnDiskWhenMainSwapped, Is.Not.Null,
                "the protected package must be deployed BEFORE the main package is replaced");
            var deployedProtected = protectedOnDiskWhenMainSwapped!;
            var protectedBody = protectedBytes[12..];
            Assert.That(BitConverter.ToUInt32(deployedProtected, 4), Is.EqualTo(2u),
                "the protected package is finalized to v2 before deploy");
            Assert.That(deployedProtected.Length, Is.EqualTo(28 + protectedBody.Length + 48 + 64),
                "v2 header + this run's payload + the signature section");
            Assert.That(deployedProtected[28..(28 + protectedBody.Length)], Is.EqualTo(protectedBody),
                "the protected package on disk carries THIS run's payload");
        });
    }

    [Test]
    public async Task MiscClean_RunTwice_StillPacksLocalization_BecauseTheCachedBaseIsNeverPatched()
    {
        string baseDir = MakePristineBase();
        var provider = ProviderFor(baseDir);
        var selections = new Dictionary<string, string> { { "Roshan", "Golden" } };

        var firstTrees = new Dictionary<string, List<string>>();
        var first = new MiscCleanGenerationService(provider.Object, new FakeAssetModifier(),
            RecordingRecompiler(firstTrees).Object, SucceedingReplacer().Object, RefreshingExtractor().Object);
        var firstResult = await first.GenerateCleanAsync(_targetPath, selections, _ => { });

        var secondTrees = new Dictionary<string, List<string>>();
        var second = new MiscCleanGenerationService(provider.Object, new FakeAssetModifier(),
            RecordingRecompiler(secondTrees).Object, SucceedingReplacer().Object, RefreshingExtractor().Object);
        var secondResult = await second.GenerateCleanAsync(_targetPath, selections, _ => { });

        Assert.Multiple(() =>
        {
            Assert.That(firstResult.Success, Is.True);
            Assert.That(secondResult.Success, Is.True);
            Assert.That(RelativeFiles(baseDir), Contains.Item(LocalizationPath),
                "the pristine cache must survive the run untouched");
            Assert.That(RelativeFiles(baseDir), Contains.Item(ItemDataPath));
            Assert.That(secondTrees["protected"], Contains.Item(LocalizationPath),
                "the second run must still find localization to pack");
            Assert.That(secondTrees["protected"], Contains.Item(ItemDataPath));
        });
    }

    [Test]
    public async Task MiscClean_GameInfoWithoutModMount_KeepsPackageTextInTheMainPackage()
    {
        File.WriteAllText(Path.Combine(_targetPath, "game", "dota", "gameinfo_branchspecific.gi"),
            "\t\t\tGame\t\t\t\t_ArdysaMods\r\n\t\t\tGame\t\t\t\tdota\r\n");

        string baseDir = MakePristineBase();
        var trees = new Dictionary<string, List<string>>();

        var service = new MiscCleanGenerationService(
            ProviderFor(baseDir).Object,
            new FakeAssetModifier(),
            RecordingRecompiler(trees).Object,
            SucceedingReplacer().Object,
            RefreshingExtractor().Object);

        var result = await service.GenerateCleanAsync(_targetPath, new Dictionary<string, string> { { "Roshan", "Golden" } }, _ => { });

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(trees.ContainsKey("protected"), Is.False, "no protected package is built on an unmounted install");
            Assert.That(trees["base"], Contains.Item(ItemDataPath), "item data stays where the game can still read it");
            Assert.That(trees["base"], Contains.Item(LocalizationPath));
            Assert.That(File.Exists(ProtectedVpkStore.VpkPath(_targetPath)), Is.False);
        });
    }

    [Test]
    public async Task MiscClean_GameInfoWithoutModMount_KeepsProtectedSetAssetsInTheMainPackage()
    {
        File.WriteAllText(Path.Combine(_targetPath, "game", "dota", "gameinfo_branchspecific.gi"),
            "\t\t\tGame\t\t\t\t_ArdysaMods\r\n\t\t\tGame\t\t\t\tdota\r\n");

        const string assetPath = "models/roshan/roshan.vmdl_c";
        var modifier = new FakeAssetModifier();
        modifier.ProtectedPathsToReturn.Add(assetPath);
        modifier.OnApply = (_, extract) =>
        {
            string full = Path.Combine(extract, assetPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, "model_data");
        };

        string baseDir = MakePristineBase();
        var trees = new Dictionary<string, List<string>>();

        var service = new MiscCleanGenerationService(
            ProviderFor(baseDir).Object,
            modifier,
            RecordingRecompiler(trees).Object,
            SucceedingReplacer().Object,
            RefreshingExtractor().Object);

        var result = await service.GenerateCleanAsync(_targetPath, new Dictionary<string, string> { { "Roshan", "Golden" } }, _ => { });

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(trees.ContainsKey("protected"), Is.False, "no protected package is built on an unmounted install");
            Assert.That(trees["base"], Contains.Item(assetPath), "the set asset stays where the game can still load it");
            Assert.That(File.Exists(ProtectedVpkStore.VpkPath(_targetPath)), Is.False);
        });
    }

    [Test]
    public async Task MiscClean_WhenTheProtectedDeployFails_TheMainPackageIsNeverSwapped()
    {
        string baseDir = MakePristineBase();

        string modsDir = Path.Combine(_targetPath, "game", "_ArdysaMods");
        Directory.CreateDirectory(modsDir);
        string mainVpk = Path.Combine(modsDir, "pak01_dir.vpk");
        byte[] installedMain = TestVpk.Build(TestVpk.Blob("models/heroes", "axe", "vmdl_c", 32));
        File.WriteAllBytes(mainVpk, installedMain);

        ProtectedVpkStore.Ensure(_targetPath);
        string protVpk = ProtectedVpkStore.VpkPath(_targetPath);
        byte[] installedProtected = TestVpk.Minimal();
        File.WriteAllBytes(protVpk, installedProtected);

        var trees = new Dictionary<string, List<string>>();
        var mockReplacer = SucceedingReplacer();

        var service = new MiscCleanGenerationService(
            ProviderFor(baseDir).Object,
            new FakeAssetModifier(),
            RecordingRecompiler(trees, key => key == "protected" ? new byte[] { 1, 2, 3, 4 } : TestVpk.Minimal()).Object,
            mockReplacer.Object,
            RefreshingExtractor().Object);

        var result = await service.GenerateCleanAsync(_targetPath, new Dictionary<string, string> { { "Roshan", "Golden" } }, _ => { });

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False, "a package that cannot be installed is not a successful run");
            mockReplacer.Verify(r => r.ReplaceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()),
                Times.Never, "the main package must not be swapped once the protected deploy failed");
            Assert.That(File.ReadAllBytes(mainVpk), Is.EqualTo(installedMain), "the installed main package is untouched");
            Assert.That(File.ReadAllBytes(protVpk), Is.EqualTo(installedProtected), "the installed protected package is untouched");
            Assert.That(File.Exists(protVpk + ".bak"), Is.False, "no half-finished rename is left behind");
        });
    }

    [Test]
    public async Task MiscGeneration_AddToCurrent_BorrowsPatchesAndReturnsThePackageText()
    {
        string modsDir = Path.Combine(_targetPath, "game", "_ArdysaMods");
        Directory.CreateDirectory(modsDir);
        string mainVpk = Path.Combine(modsDir, "pak01_dir.vpk");
        File.WriteAllBytes(mainVpk, TestVpk.Minimal());

        ProtectedVpkStore.Ensure(_targetPath);
        string protVpk = ProtectedVpkStore.VpkPath(_targetPath);
        File.WriteAllBytes(protVpk, TestVpk.Minimal());

        bool mainExtractedWithoutRequiringItemData = false;
        var mockExtractor = new Mock<IVpkExtractor>();
        mockExtractor
            .Setup(e => e.ExtractAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<SpeedMetrics>?>(), It.IsAny<bool>()))
            .Returns<string, string, string, Action<string>, CancellationToken, IProgress<SpeedMetrics>?, bool>((tool, vpk, destDir, l, ct, sp, requireItemsGame) =>
            {
                if (vpk == mainVpk)
                {
                    mainExtractedWithoutRequiringItemData = !requireItemsGame;
                    Directory.CreateDirectory(Path.Combine(destDir, "models", "heroes", "axe"));
                    File.WriteAllText(Path.Combine(destDir, "models", "heroes", "axe", "axe.vmdl_c"), "axe_model");
                }
                else if (vpk == protVpk)
                {
                    Directory.CreateDirectory(Path.Combine(destDir, "scripts", "items"));
                    File.WriteAllText(Path.Combine(destDir, "scripts", "items", "items_game.txt"), "\"items_game\" { \"items\" { } }");
                    Directory.CreateDirectory(Path.Combine(destDir, "resource", "localization"));
                    File.WriteAllText(Path.Combine(destDir, "resource", "localization", "dota_english.txt"), "\"lang\" { }");
                }
                return Task.FromResult(true);
            });

        var fakeModifier = new FakeAssetModifier();
        fakeModifier.OnApply = (vpk, extract) =>
        {
            File.WriteAllText(Path.Combine(extract, "scripts", "items", "items_game.txt"),
                "\"items_game\" { \"items\" { \"9001\" { } } }");
        };

        var trees = new Dictionary<string, List<string>>();
        string? patchedInProtected = null;
        var mockRecompiler = new Mock<IVpkRecompiler>();
        mockRecompiler
            .Setup(r => r.RecompileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<SpeedMetrics>?>()))
            .Returns<string, string, string, string, Action<string>, CancellationToken, IProgress<SpeedMetrics>?>((tool, inputDir, build, temp, l, ct, sp) =>
            {
                string key = Path.GetFileName(inputDir);
                trees[key] = RelativeFiles(inputDir);
                if (key == "protected")
                {
                    string itemData = Path.Combine(inputDir, "scripts", "items", "items_game.txt");
                    patchedInProtected = File.Exists(itemData) ? File.ReadAllText(itemData) : null;
                }
                string dummyVpk = Path.Combine(temp, key + ".vpk");
                File.WriteAllBytes(dummyVpk, TestVpk.Minimal());
                return Task.FromResult<string?>(dummyVpk);
            });

        var service = new MiscGenerationService(mockExtractor.Object, fakeModifier, mockRecompiler.Object, SucceedingReplacer().Object);
        var result = await service.PerformGenerationAsync(_targetPath, new Dictionary<string, string> { { "Roshan", "Golden" } }, _ => { });

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(mainExtractedWithoutRequiringItemData, Is.True,
                "the installed main package no longer carries item data, so extraction must not require it");

            string mainKey = trees.Keys.Single(k => k != "protected");
            Assert.That(trees[mainKey], Does.Not.Contain(ItemDataPath), "the patched item data must not go back into the main package");
            Assert.That(trees[mainKey].Where(f => f.StartsWith("resource/localization")), Is.Empty);
            Assert.That(trees[mainKey], Contains.Item("models/heroes/axe/axe.vmdl_c"), "the assets themselves stay in the main package");

            Assert.That(trees["protected"], Contains.Item(ItemDataPath));
            Assert.That(trees["protected"], Contains.Item(LocalizationPath));
            Assert.That(patchedInProtected, Does.Contain("9001"),
                "the patch applied to the borrowed copy must be what lands back in the protected package");
        });
    }

    [Test]
    public async Task MiscGeneration_AddToCurrent_WhenTheProtectedPackageCannotBeRead_FailsWithAnActionableMessage()
    {
        string modsDir = Path.Combine(_targetPath, "game", "_ArdysaMods");
        Directory.CreateDirectory(modsDir);
        string mainVpk = Path.Combine(modsDir, "pak01_dir.vpk");
        File.WriteAllBytes(mainVpk, TestVpk.Minimal());

        ProtectedVpkStore.Ensure(_targetPath);
        File.WriteAllBytes(ProtectedVpkStore.VpkPath(_targetPath), TestVpk.Minimal());

        var mockExtractor = new Mock<IVpkExtractor>();
        mockExtractor
            .Setup(e => e.ExtractAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<SpeedMetrics>?>(), It.IsAny<bool>()))
            .Returns<string, string, string, Action<string>, CancellationToken, IProgress<SpeedMetrics>?, bool>((tool, vpk, destDir, l, ct, sp, req) =>
            {
                if (vpk == mainVpk)
                {
                    Directory.CreateDirectory(Path.Combine(destDir, "models"));
                    File.WriteAllText(Path.Combine(destDir, "models", "axe.vmdl_c"), "axe");
                }
                return Task.FromResult(true);
            });

        var mockRecompiler = new Mock<IVpkRecompiler>();
        var service = new MiscGenerationService(mockExtractor.Object, new FakeAssetModifier(),
            mockRecompiler.Object, SucceedingReplacer().Object);
        var result = await service.PerformGenerationAsync(_targetPath, new Dictionary<string, string> { { "Roshan", "Golden" } }, _ => { });

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("item definitions"), "the user is told what is actually wrong");
            mockRecompiler.Verify(r => r.RecompileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<SpeedMetrics>?>()),
                Times.Never, "nothing is rebuilt from a tree with no item definitions");
        });
    }
}
