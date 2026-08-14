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
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Cdn;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class ModInstallerCdnFallbackTests
    {
        private RichTextBox _testConsole = null!;
        private Logger _logger = null!;
        private ModInstallerService _service = null!;

        [SetUp]
        public void SetUp()
        {
            _testConsole = new RichTextBox();
            _logger = new Logger(_testConsole);
            _service = new ModInstallerService(_logger);
            CdnConfig.CdnServerPreference = "auto";
        }

        [TearDown]
        public void TearDown()
        {
            CdnConfig.CdnServerPreference = "auto";
            _testConsole?.Dispose();
        }

        [Test]
        public void CdnConfig_ConvertToCdn_ConvertsModsPackReleasesUrlCorrectly()
        {
            string r2Url = "https://cdn.ardysamods.my.id/modspack-releases/mods-v4.0/mods-v4.0.zip";
            string b2Url = CdnConfig.ConvertToCdn(r2Url, CdnConfig.Cdn2BaseUrl);

            Assert.That(b2Url, Is.EqualTo("https://cdn2.ardysamods.my.id/modspack-releases/mods-v4.0/mods-v4.0.zip"));
        }

        [Test]
        public void CdnConfig_ConvertToCdn_ConvertsModsPackManifestUrlCorrectly()
        {
            string r2Manifest = CdnConfig.ModsPackReleasesManifestUrl;
            string b2Manifest = CdnConfig.ConvertToCdn(r2Manifest, CdnConfig.Cdn2BaseUrl);

            Assert.That(b2Manifest, Is.EqualTo("https://cdn2.ardysamods.my.id/modspack-releases/modspack-releases.json"));
        }

        [Test]
        public void CdnConfig_ConvertToCdn_ConvertsAppReleasesManifestUrlCorrectly()
        {
            string r2Manifest = CdnConfig.ReleaseManifestUrl;
            string b2Manifest = CdnConfig.ConvertToCdn(r2Manifest, CdnConfig.Cdn2BaseUrl);

            Assert.That(b2Manifest, Is.EqualTo("https://cdn2.ardysamods.my.id/releases/releases.json"));
        }

        [Test]
        public void CdnConfig_IsModsPackUrl_IdentifiesCdnAndWorkerUrls()
        {
            Assert.That(CdnConfig.IsModsPackUrl("https://cdn.ardysamods.my.id/modspack-releases/mods-v4.0/mods-v4.0.zip"), Is.True);
            Assert.That(CdnConfig.IsModsPackUrl("https://cdn2.ardysamods.my.id/modspack-releases/mods-v4.0/mods-v4.0.zip"), Is.True);
            Assert.That(CdnConfig.IsModsPackUrl("https://cdn.ardysamods.my.id/releases/releases.json"), Is.True);
            Assert.That(CdnConfig.IsModsPackUrl("https://cdn2.ardysamods.my.id/releases/releases.json"), Is.True);
            Assert.That(CdnConfig.IsModsPackUrl("https://example.com/other.zip"), Is.False);
        }

        [Test]
        public void ManifestSchema_ParsesCorrectly()
        {
            string json = @"{
                ""latest"": ""mods-v4.0"",
                ""releases"": {
                    ""mods-v4.0"": {
                        ""version"": ""mods-v4.0"",
                        ""date"": ""2026-08-10"",
                        ""assets"": [
                            {
                                ""name"": ""mods-v4.0.zip"",
                                ""url"": ""https://cdn.ardysamods.my.id/modspack-releases/mods-v4.0/mods-v4.0.zip"",
                                ""size"": 470396296
                            }
                        ],
                        ""notes"": null
                    }
                }
            }";

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.That(root.TryGetProperty("latest", out var latestProp), Is.True);
            Assert.That(latestProp.GetString(), Is.EqualTo("mods-v4.0"));

            Assert.That(root.TryGetProperty("releases", out var releases), Is.True);
            Assert.That(releases.TryGetProperty("mods-v4.0", out var release), Is.True);
            Assert.That(release.TryGetProperty("assets", out var assets), Is.True);

            var asset = assets[0];
            Assert.That(asset.GetProperty("name").GetString(), Is.EqualTo("mods-v4.0.zip"));
            Assert.That(asset.GetProperty("url").GetString(), Is.EqualTo("https://cdn.ardysamods.my.id/modspack-releases/mods-v4.0/mods-v4.0.zip"));
        }

        [Test]
        public void TryGetModsPackAssetUrlAsync_Cancellation_ThrowsOperationCanceledException()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await _service.TryGetModsPackAssetUrlAsync(cts.Token);
            });
        }
    }
}
