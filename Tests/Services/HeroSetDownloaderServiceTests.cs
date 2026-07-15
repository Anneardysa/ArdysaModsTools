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
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using NUnit.Framework;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Exceptions;
using ArdysaModsTools.Core.Services.Cdn;
using ArdysaModsTools.Tests.Helpers;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class HeroSetDownloaderServiceTests
    {
        private HeroSetDownloaderService _service = null!;

        [SetUp]
        public void Setup()
        {
            _service = new HeroSetDownloaderService();
        }

        #region Constructor Tests

        [Test]
        public void Constructor_WithDefaults_CreatesInstance()
        {
            var service = new HeroSetDownloaderService();

            Assert.That(service, Is.Not.Null);
        }

        [Test]
        public void Constructor_WithCustomFolder_CreatesInstance()
        {
            var tempFolder = Path.GetTempPath();

            var service = new HeroSetDownloaderService(tempFolder);

            Assert.That(service, Is.Not.Null);
        }

        #endregion

        #region DownloadAndExtractAsync Validation Tests

        [Test]
        public void DownloadAndExtractAsync_WithNullHeroId_ThrowsArgumentNullException()
        {
            Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await _service.DownloadAndExtractAsync(
                    null!,
                    "set1",
                    "https://example.com/set.zip",
                    msg => { });
            });
        }

        [Test]
        public void DownloadAndExtractAsync_WithEmptyHeroId_ThrowsArgumentNullException()
        {
            Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await _service.DownloadAndExtractAsync(
                    "",
                    "set1",
                    "https://example.com/set.zip",
                    msg => { });
            });
        }

        [Test]
        public void DownloadAndExtractAsync_WithEmptyUrl_ThrowsDownloadException()
        {
            var ex = Assert.ThrowsAsync<DownloadException>(async () =>
            {
                await _service.DownloadAndExtractAsync(
                    "test_hero",
                    "set1",
                    "",
                    msg => { });
            });

            Assert.That(ex!.ErrorCode, Is.EqualTo(ErrorCodes.DL_INVALID_URL));
        }

        #endregion

        #region Split Archive Retry

        [Test]
        public async Task DownloadAndExtractAsync_SplitArchive_RetriesTransientPartThenMergesAndExtracts()
        {
            byte[] zipBytes = BuildZip("inside.txt", "hello");
            int part1Calls = 0;

            var fake = new FakeHttpMessageHandler((req, _) =>
            {
                string url = req.RequestUri!.ToString();
                if (url.EndsWith(".002"))
                    return new HttpResponseMessage(HttpStatusCode.NotFound);

                if (part1Calls++ == 0)
                    return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(zipBytes) };
            });

            var tempFolder = Path.Combine(Path.GetTempPath(), "AmtSplitTest_" + Guid.NewGuid().ToString("N"));
            var service = new HeroSetDownloaderService(tempFolder, new HttpClient(fake), hashResolver: NullResolver);
            string? workFolder = null;

            try
            {
                workFolder = await service.DownloadAndExtractAsync(
                    "npc_dota_hero_test",
                    "set1",
                    "https://cdn.ardysamods.my.id/Assets/models/test_hero/set1/model.zip.001",
                    _ => { });

                Assert.That(part1Calls, Is.GreaterThanOrEqualTo(2), "part .001 should have been retried after the 503");
                Assert.That(Directory.Exists(workFolder), Is.True);
                Assert.That(File.Exists(Path.Combine(workFolder, "inside.txt")), Is.True);
                Assert.That(File.ReadAllText(Path.Combine(workFolder, "inside.txt")), Is.EqualTo("hello"));
            }
            finally
            {
                try { if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true); } catch { }
                try { if (workFolder != null && Directory.Exists(workFolder)) Directory.Delete(workFolder, true); } catch { }
            }
        }

        [Test]
        public async Task DownloadAndExtractAsync_SplitArchive_CorrectHash_Extracts()
        {
            byte[] zipBytes = BuildZip("inside.txt", "verified");
            var fake = SinglePartSplitHandler(zipBytes);

            var expected = new AssetHashEntry
            {
                Sha256 = Convert.ToHexString(SHA256.HashData(zipBytes)),
                Size = zipBytes.Length
            };

            var tempFolder = Path.Combine(Path.GetTempPath(), "AmtHashOk_" + Guid.NewGuid().ToString("N"));
            var service = new HeroSetDownloaderService(tempFolder, new HttpClient(fake), hashResolver: (_, _) => Task.FromResult<AssetHashEntry?>(expected));
            string? workFolder = null;

            try
            {
                workFolder = await service.DownloadAndExtractAsync(
                    "npc_dota_hero_test", "set1",
                    "https://cdn.ardysamods.my.id/Assets/models/test_hero/set1/model.zip.001",
                    _ => { });

                Assert.That(File.Exists(Path.Combine(workFolder, "inside.txt")), Is.True);
            }
            finally
            {
                try { if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true); } catch { }
                try { if (workFolder != null && Directory.Exists(workFolder)) Directory.Delete(workFolder, true); } catch { }
            }
        }

        [Test]
        public void DownloadAndExtractAsync_SplitArchive_WrongHash_ThrowsHashMismatchAndDeletes()
        {
            byte[] zipBytes = BuildZip("inside.txt", "tampered");
            var fake = SinglePartSplitHandler(zipBytes);

            var wrong = new AssetHashEntry { Sha256 = new string('A', 64), Size = zipBytes.Length };

            var tempFolder = Path.Combine(Path.GetTempPath(), "AmtHashBad_" + Guid.NewGuid().ToString("N"));
            var service = new HeroSetDownloaderService(tempFolder, new HttpClient(fake), hashResolver: (_, _) => Task.FromResult<AssetHashEntry?>(wrong));

            try
            {
                var ex = Assert.ThrowsAsync<DownloadException>(async () =>
                    await service.DownloadAndExtractAsync(
                        "npc_dota_hero_test", "set1",
                        "https://cdn.ardysamods.my.id/Assets/models/test_hero/set1/model.zip.001",
                        _ => { }));

                Assert.That(ex!.ErrorCode, Is.EqualTo(ErrorCodes.DL_HASH_MISMATCH));
                var cached = Path.Combine(tempFolder, "cache", "sets", "npc_dota_hero_test", "set1", "model.zip");
                Assert.That(File.Exists(cached), Is.False, "mismatched cache file should be deleted");
            }
            finally
            {
                try { if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true); } catch { }
            }
        }

        private static Task<AssetHashEntry?> NullResolver(string assetPath, System.Threading.CancellationToken ct)
            => Task.FromResult<AssetHashEntry?>(null);

        private static FakeHttpMessageHandler SinglePartSplitHandler(byte[] zipBytes) =>
            new FakeHttpMessageHandler((req, _) =>
            {
                string url = req.RequestUri!.ToString();
                if (url.EndsWith(".002"))
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(zipBytes) };
            });

        private static byte[] BuildZip(string entryName, string content)
        {
            using var ms = new MemoryStream();
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry(entryName);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(content);
            }
            return ms.ToArray();
        }

        #endregion
    }
}

