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
using ArdysaModsTools.Core.Services.Hero;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class SkinManifestStoreTests
    {
        private string _tempDir = null!;

        [SetUp]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"amt_manifest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                try { Directory.Delete(_tempDir, true); } catch { }
            }
        }

        private string ManifestPath =>
            Path.Combine(_tempDir, SkinManifestStore.TreeRelativePath.Replace('/', Path.DirectorySeparatorChar));

        private static SkinManifest SampleManifest() => new()
        {
            GeneratedAtUtc = "2026-08-28T10:11:12Z",
            Build = "2380",
            Entries =
            {
                new SkinManifestEntry { HeroId = "juggernaut", SetName = "Bladeform Legacy", Layer = "set", Sha256 = "AABB" },
                new SkinManifestEntry { HeroId = "pudge", SetName = "item_hook", Layer = "item", Sha256 = "CCDD" },
            }
        };

        [Test]
        public async Task WriteThenParse_RoundTripsEntries()
        {
            await SkinManifestStore.WriteToTreeAsync(_tempDir, SampleManifest());

            var parsed = SkinManifestStore.Parse(await File.ReadAllTextAsync(ManifestPath));

            Assert.That(parsed, Is.Not.Null);
            Assert.That(parsed!.Build, Is.EqualTo("2380"));
            Assert.That(parsed.Entries, Has.Count.EqualTo(2));
            Assert.That(parsed.Entries[0].HeroId, Is.EqualTo("juggernaut"));
            Assert.That(parsed.Entries[0].SetName, Is.EqualTo("Bladeform Legacy"));
            Assert.That(parsed.Entries[1].Layer, Is.EqualTo("item"));
            Assert.That(parsed.Entries[1].Sha256, Is.EqualTo("CCDD"));
        }

        [Test]
        public void Entry_SerializesOnlyTheFieldsDetectionNeeds()
        {
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(
                new SkinManifestEntry { HeroId = "juggernaut", SetName = "Bladeform Legacy", Layer = "set", Sha256 = "AABB" }));

            var fields = doc.RootElement.EnumerateObject().Select(p => p.Name).ToArray();

            Assert.That(fields, Is.EquivalentTo(new[] { "heroId", "setName", "layer", "sha256" }));
        }

        [Test]
        public async Task WriteToTree_PreservesExistingDecoyContent()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath)!);
            await File.WriteAllTextAsync(ManifestPath, "PatchVersion=1234\nClientVersion=5678\n");

            await SkinManifestStore.WriteToTreeAsync(_tempDir, SampleManifest());

            var text = await File.ReadAllTextAsync(ManifestPath);
            Assert.That(text, Does.StartWith("PatchVersion=1234"));
            Assert.That(text, Does.Contain("ClientVersion=5678"));
            Assert.That(SkinManifestStore.Parse(text), Is.Not.Null);
        }

        [Test]
        public async Task WriteToTree_Twice_ReplacesBlockInsteadOfStacking()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath)!);
            await File.WriteAllTextAsync(ManifestPath, "PatchVersion=1234\n");

            await SkinManifestStore.WriteToTreeAsync(_tempDir, SampleManifest());

            var second = SampleManifest();
            second.Entries.RemoveAt(1);
            await SkinManifestStore.WriteToTreeAsync(_tempDir, second);

            var text = await File.ReadAllTextAsync(ManifestPath);
            Assert.That(Occurrences(text, "AMT SKIN MANIFEST v1"), Is.EqualTo(1));
            Assert.That(text, Does.StartWith("PatchVersion=1234"));
            Assert.That(SkinManifestStore.Parse(text)!.Entries, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Parse_TamperedBlock_ReturnsNull()
        {
            await SkinManifestStore.WriteToTreeAsync(_tempDir, SampleManifest());
            var text = await File.ReadAllTextAsync(ManifestPath);

            var tampered = text.Replace("juggernaut", "pudgernaut");

            Assert.That(SkinManifestStore.Parse(tampered), Is.Null);
        }

        [Test]
        public async Task Parse_SurvivesCrlfRewrite()
        {
            await SkinManifestStore.WriteToTreeAsync(_tempDir, SampleManifest());
            var text = await File.ReadAllTextAsync(ManifestPath);

            var crlf = text.Replace("\r\n", "\n").Replace("\n", "\r\n");

            Assert.That(SkinManifestStore.Parse(crlf), Is.Not.Null);
        }

        [Test]
        public void Parse_NoBlockOrEmpty_ReturnsNull()
        {
            Assert.That(SkinManifestStore.Parse(null), Is.Null);
            Assert.That(SkinManifestStore.Parse(""), Is.Null);
            Assert.That(SkinManifestStore.Parse("PatchVersion=1234\n"), Is.Null);
        }

        [Test]
        public void Parse_TruncatedBlock_ReturnsNull()
        {
            Assert.That(SkinManifestStore.Parse("# --- AMT SKIN MANIFEST v1 ---\n{\"version\":1}\n"), Is.Null);
        }

        private static int Occurrences(string haystack, string needle)
        {
            int count = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { count++; i += needle.Length; }
            return count;
        }
    }
}
