using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Misc;
using ArdysaModsTools.Tests.Helpers;
using Moq;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services.Misc
{
    [TestFixture]
    public class MapCleanupServiceTests
    {
        private string _root = string.Empty;
        private string _work = string.Empty;
        private Mock<IVpkExtractor> _extractor = null!;
        private Mock<IVpkRecompiler> _recompiler = null!;

        private Dictionary<string, byte[]> _installedTree = new();

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "amt_mapcleanup_tests_" + Guid.NewGuid().ToString("N"));
            _work = Path.Combine(_root, "_work");
            Directory.CreateDirectory(Path.Combine(_root, "game", "dota"));
            Directory.CreateDirectory(_work);
            _installedTree = new Dictionary<string, byte[]>();

            _extractor = new Mock<IVpkExtractor>();
            _extractor.Setup(e => e.ExtractAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<SpeedMetrics>?>(), false))
                .Returns((string _, string _, string dir, Action<string> _, CancellationToken _, IProgress<SpeedMetrics>? _, bool _) =>
                {
                    foreach (var (rel, bytes) in _installedTree)
                    {
                        var p = Path.Combine(dir, rel.Replace('/', Path.DirectorySeparatorChar));
                        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
                        File.WriteAllBytes(p, bytes);
                    }
                    return Task.FromResult(true);
                });

            _recompiler = new Mock<IVpkRecompiler>();
            _recompiler.Setup(r => r.RecompileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<SpeedMetrics>?>()))
                .Returns((string _, string dir, string _, string _, Action<string> _, CancellationToken _, IProgress<SpeedMetrics>? _) =>
                {
                    _installedTree = Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
                        .ToDictionary(f => Path.GetRelativePath(dir, f).Replace('\\', '/'), File.ReadAllBytes);
                    var vpk = Path.Combine(_work, Guid.NewGuid().ToString("N") + ".vpk");
                    File.WriteAllBytes(vpk, TestVpk.Build(_installedTree.Select(kv => Entry(kv.Key, kv.Value)).ToArray()));
                    return Task.FromResult<string?>(vpk);
                });
        }

        [TearDown]
        public void TearDown()
        {
            ProtectedVpkStore.NormalizeAttributesRecursively(_root);
            try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch {  }
        }

        private static TestVpk.Entry Entry(string rel, byte[] data)
        {
            var dir = Path.GetDirectoryName(rel)?.Replace('\\', '/');
            var file = Path.GetFileName(rel);
            var dot = file.LastIndexOf('.');
            return new TestVpk.Entry { Dir = string.IsNullOrEmpty(dir) ? " " : dir, Name = file[..dot], Ext = file[(dot + 1)..], Data = data };
        }

        private MapCleanupService CreateService() => new(_extractor.Object, _recompiler.Object, new TestLogger());

        private void WriteGameInfo(bool mountsMod) => File.WriteAllText(
            Path.Combine(_root, "game", "dota", "gameinfo_branchspecific.gi"),
            "SearchPaths\r\n{\r\n\t\t\tGame\t\t\t\t_ArdysaMods\r\n" + (mountsMod ? "\t\t\tGame\t\t\t\tmod\r\n" : "") + "\t\t\tGame\t\t\t\tdota\r\n}\r\n");

        [Test]
        public void Paths_AreDisjoint_AndNeverTouchCosmeticsOrTreeLeaves()
        {
            var foliage = MapCleanupService.Paths(MapCleanupGroup.Foliage);
            var props = MapCleanupService.Paths(MapCleanupGroup.Props);

            Assert.That(foliage, Is.Not.Empty);
            Assert.That(props, Is.Not.Empty);
            Assert.That(foliage.Intersect(props), Is.Empty);
            foreach (var p in foliage.Concat(props))
            {
                Assert.That(p, Does.Not.StartWith("models/items/"), "hero cosmetics must never be blanked");
                Assert.That(p, Does.Not.StartWith("materials/models/props_tree/"), "tree leaves need a remap, not a blank");
                Assert.DoesNotThrow(() => MapCleanupService.BlankFor(p), $"no blank for {p}");
            }
        }

        [Test]
        public void ApplyTo_WritesEnabledGroup_RemovesDisabledBlanks_KeepsForeignFiles()
        {
            var dir = Path.Combine(_work, "tree");
            var foliage = MapCleanupService.Paths(MapCleanupGroup.Foliage);
            var props = MapCleanupService.Paths(MapCleanupGroup.Props);

            MapCleanupService.ApplyTo(dir, new HashSet<MapCleanupGroup> { MapCleanupGroup.Props });
            var foreign = Path.Combine(dir, foliage[0]);
            Directory.CreateDirectory(Path.GetDirectoryName(foreign)!);
            File.WriteAllBytes(foreign, new byte[] { 1, 2, 3 });

            int written = MapCleanupService.ApplyTo(dir, new HashSet<MapCleanupGroup> { MapCleanupGroup.Foliage });

            Assert.That(written, Is.EqualTo(foliage.Count - 1), "the foreign file is not overwritten");
            Assert.That(File.ReadAllBytes(foreign), Is.EqualTo(new byte[] { 1, 2, 3 }));
            Assert.That(File.ReadAllBytes(Path.Combine(dir, foliage[1])), Is.EqualTo(MapCleanupService.BlankFor(foliage[1])));
            Assert.That(props.Any(p => File.Exists(Path.Combine(dir, p))), Is.False, "turned-off group's blanks are removed");
        }

        [Test]
        public void ReadEnabled_MissingOrCorruptState_IsOff()
        {
            Assert.That(MapCleanupService.ReadEnabled(_root), Is.Empty);
            Directory.CreateDirectory(Path.Combine(_root, "game", "_ArdysaMods", "_temp"));
            File.WriteAllText(Path.Combine(_root, "game", "_ArdysaMods", "_temp", "map_cleanup.json"), "{not json");
            Assert.That(MapCleanupService.ReadEnabled(_root), Is.Empty);
        }

        [Test]
        public async Task InstallThenRemove_RepacksProtectedPackage_AndRemembersState()
        {
            var service = CreateService();
            WriteGameInfo(mountsMod: true);
            _installedTree["scripts/items/items_game.txt"] = "\"items_game\"\n{\n}\n"u8.ToArray();
            File.WriteAllBytes(EnsureDir(ProtectedVpkStore.VpkPath(_root)), TestVpk.Minimal());

            Assert.That(await service.SetEnabledAsync(_root, new HashSet<MapCleanupGroup> { MapCleanupGroup.Foliage }), Is.True);

            Assert.That(await service.GetStatusAsync(_root), Is.EqualTo(new MapCleanupStatus(true, false, true)));
            Assert.That(MapCleanupService.ReadEnabled(_root), Is.EquivalentTo(new[] { MapCleanupGroup.Foliage }));
            Assert.That(_installedTree.ContainsKey("scripts/items/items_game.txt"), Is.True, "existing package content survives");

            Assert.That(await service.SetEnabledAsync(_root, new HashSet<MapCleanupGroup>()), Is.True);

            Assert.That(await service.GetStatusAsync(_root), Is.EqualTo(new MapCleanupStatus(false, false, true)));
            Assert.That(MapCleanupService.ReadEnabled(_root), Is.Empty);
            Assert.That(_installedTree.Keys, Is.EquivalentTo(new[] { "scripts/items/items_game.txt" }));
        }

        [Test]
        public async Task SetEnabled_AlreadyMatching_DoesNotRepack()
        {
            Assert.That(await CreateService().SetEnabledAsync(_root, new HashSet<MapCleanupGroup>()), Is.False);
            _extractor.VerifyNoOtherCalls();
            _recompiler.VerifyNoOtherCalls();
        }

        [Test]
        public void Install_WhenRepackFails_LeavesStateOff()
        {
            _recompiler.Setup(r => r.RecompileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<SpeedMetrics>?>()))
                .ReturnsAsync((string?)null);

            Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().SetEnabledAsync(_root, new HashSet<MapCleanupGroup> { MapCleanupGroup.Props }));
            Assert.That(MapCleanupService.ReadEnabled(_root), Is.Empty);
            Assert.That(File.Exists(ProtectedVpkStore.VpkPath(_root)), Is.False);
        }

        [Test]
        public void ProtectedSplit_CarriesEnabledGroups()
        {
            WriteGameInfo(mountsMod: true);
            MapCleanupService.WriteEnabled(_root, new[] { MapCleanupGroup.Props });
            var extractDir = Path.Combine(_work, "extract");
            var protectedDir = Path.Combine(_work, "protected");
            Directory.CreateDirectory(extractDir);

            ProtectedVpkStore.Split(_root, extractDir, protectedDir);

            Assert.That(MapCleanupService.Paths(MapCleanupGroup.Props).All(p => File.Exists(Path.Combine(protectedDir, p))), Is.True);
            Assert.That(MapCleanupService.Paths(MapCleanupGroup.Foliage).Any(p => File.Exists(Path.Combine(protectedDir, p))), Is.False);
        }

        [Test]
        public async Task GetStatus_GameInfoWithoutModMount_IsNotMounted()
        {
            WriteGameInfo(mountsMod: false);
            Assert.That(await CreateService().GetStatusAsync(_root), Is.EqualTo(new MapCleanupStatus(false, false, false)));
        }

        [Test]
        public void Install_NotADotaInstall_Throws()
        {
            Assert.ThrowsAsync<DirectoryNotFoundException>(() => CreateService().SetEnabledAsync(Path.Combine(_root, "nope"), new HashSet<MapCleanupGroup> { MapCleanupGroup.Foliage }));
            Assert.ThrowsAsync<DirectoryNotFoundException>(() => CreateService().SetEnabledAsync(null, new HashSet<MapCleanupGroup>()));
        }

        private static string EnsureDir(string file)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            return file;
        }
    }
}
