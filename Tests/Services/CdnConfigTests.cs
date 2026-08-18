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
using ArdysaModsTools.Core.Constants;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class CdnConfigTests
    {
        [SetUp]
        public void SetUp()
        {
            CdnConfig.IsR2Enabled = true;
            CdnConfig.CdnServerPreference = "auto";
        }

        [Test]
        public void GetCdnBaseUrls_WhenEuUsPreference_PrioritizesCdn2B2()
        {
            CdnConfig.CdnServerPreference = "eu_us";

            var urls = CdnConfig.GetCdnBaseUrls();

            Assert.That(urls, Is.Not.Null);
            Assert.That(urls.Length, Is.EqualTo(2));
            Assert.That(urls[0], Is.EqualTo("https://cdn2.ardysamods.my.id"));
            Assert.That(urls[1], Is.EqualTo("https://cdn.ardysamods.my.id"));
        }

        [Test]
        public void GetCdnBaseUrls_WhenAsiaOrAutoPreference_PrioritizesR2()
        {
            CdnConfig.CdnServerPreference = "asia";
            var asiaUrls = CdnConfig.GetCdnBaseUrls();

            CdnConfig.CdnServerPreference = "auto";
            var autoUrls = CdnConfig.GetCdnBaseUrls();

            Assert.That(asiaUrls[0], Is.EqualTo("https://cdn.ardysamods.my.id"));
            Assert.That(autoUrls[0], Is.EqualTo("https://cdn.ardysamods.my.id"));
        }

        [Test]
        public void GetCdnBaseUrls_WhenR2Enabled_ReturnsPrimaryAndCdn2Fallback()
        {
            var urls = CdnConfig.GetCdnBaseUrls();

            Assert.That(urls, Is.Not.Null);
            Assert.That(urls.Length, Is.EqualTo(2));
            Assert.That(urls[0], Is.EqualTo("https://cdn.ardysamods.my.id"));
            Assert.That(urls[1], Is.EqualTo("https://cdn2.ardysamods.my.id"));
        }

        [Test]
        public void GetCdnBaseUrls_WhenR2Disabled_ReturnsCdn2FallbackOnly()
        {
            CdnConfig.IsR2Enabled = false;

            var urls = CdnConfig.GetCdnBaseUrls();

            Assert.That(urls, Is.Not.Null);
            Assert.That(urls.Length, Is.EqualTo(1));
            Assert.That(urls[0], Is.EqualTo("https://cdn2.ardysamods.my.id"));
        }

        [Test]
        public void ExtractAssetPath_ValidR2AndCdn2Urls_ReturnsRelativePath()
        {
            string primaryUrl = "https://cdn.ardysamods.my.id/Assets/models/hero.zip";
            string cdn2Url = "https://cdn2.ardysamods.my.id/Assets/models/hero.zip";
            string queryUrl = "https://cdn.ardysamods.my.id/Assets/models/hero.zip?v=1.0";

            Assert.That(CdnConfig.ExtractAssetPath(primaryUrl), Is.EqualTo("Assets/models/hero.zip"));
            Assert.That(CdnConfig.ExtractAssetPath(cdn2Url), Is.EqualTo("Assets/models/hero.zip"));
            Assert.That(CdnConfig.ExtractAssetPath(queryUrl), Is.EqualTo("Assets/models/hero.zip"));
        }

        [Test]
        public void ExtractAssetPath_PathContainingAssetsFolder_KeepsFullPath()
        {
            string deltaFileUrl = "https://cdn.ardysamods.my.id/releases/2.3.1-beta/files/Assets/Locales/en.json";

            Assert.That(CdnConfig.ExtractAssetPath(deltaFileUrl),
                Is.EqualTo("releases/2.3.1-beta/files/Assets/Locales/en.json"));
            Assert.That(CdnConfig.ConvertToCdn(deltaFileUrl, CdnConfig.Cdn2BaseUrl),
                Is.EqualTo("https://cdn2.ardysamods.my.id/releases/2.3.1-beta/files/Assets/Locales/en.json"));
        }

        [Test]
        public void ExtractAssetPath_UnknownHost_FallsBackToAssetsMarker()
        {
            string unknownHostUrl = "https://pub-abc123.r2.dev/Assets/models/hero.zip?v=2";

            Assert.That(CdnConfig.ExtractAssetPath(unknownHostUrl), Is.EqualTo("Assets/models/hero.zip"));
        }

        [Test]
        public void ExtractBaseUrl_ValidUrls_ReturnsMatchingBaseUrl()
        {
            string primaryUrl = "https://cdn.ardysamods.my.id/Assets/models/hero.zip";
            string cdn2Url = "https://cdn2.ardysamods.my.id/Assets/models/hero.zip";

            Assert.That(CdnConfig.ExtractBaseUrl(primaryUrl), Is.EqualTo("https://cdn.ardysamods.my.id"));
            Assert.That(CdnConfig.ExtractBaseUrl(cdn2Url), Is.EqualTo("https://cdn2.ardysamods.my.id"));
        }

        [Test]
        public void ConvertToCdn_ConvertsBetweenPrimaryAndCdn2()
        {
            string primaryUrl = "https://cdn.ardysamods.my.id/Assets/models/hero.zip";
            string converted = CdnConfig.ConvertToCdn(primaryUrl, "https://cdn2.ardysamods.my.id");

            Assert.That(converted, Is.EqualTo("https://cdn2.ardysamods.my.id/Assets/models/hero.zip"));
        }

        [Test]
        public void BuildUrl_UsesPrimaryR2Url()
        {
            string url = CdnConfig.BuildUrl("Assets/models/hero.zip");

            Assert.That(url, Is.EqualTo("https://cdn.ardysamods.my.id/Assets/models/hero.zip"));
        }

        [Test]
        public void ExtractAssetPath_LegacyJsDelivrAndGitHubUrls_ExtractsRelativePath()
        {
            string jsDelivrUrl = "https://cdn.jsdelivr.net/gh/Anneardysa/ModsPack@main/config/banner.json";
            string rawGitHubUrl = "https://raw.githubusercontent.com/Anneardysa/ModsPack/main/config/banner.json";

            Assert.That(CdnConfig.ExtractAssetPath(jsDelivrUrl), Is.EqualTo("config/banner.json"));
            Assert.That(CdnConfig.ExtractAssetPath(rawGitHubUrl), Is.EqualTo("config/banner.json"));
        }

        [Test]
        public void ConvertToCdn_LegacyUrls_ConvertsToPrimaryOrCdn2()
        {
            string legacyUrl = "https://cdn.jsdelivr.net/gh/Anneardysa/ModsPack@main/config/banner.json";
            string primaryConverted = CdnConfig.ConvertToCdn(legacyUrl, CdnConfig.R2BaseUrl);
            string cdn2Converted = CdnConfig.ConvertToCdn(legacyUrl, CdnConfig.Cdn2BaseUrl);

            Assert.That(primaryConverted, Is.EqualTo("https://cdn.ardysamods.my.id/config/banner.json"));
            Assert.That(cdn2Converted, Is.EqualTo("https://cdn2.ardysamods.my.id/config/banner.json"));
        }

        [Test]
        public void IsModsPackUrl_RecognizesPrimaryAndCdn2Domains()
        {
            Assert.That(CdnConfig.IsModsPackUrl("https://cdn.ardysamods.my.id/Assets/test.zip"), Is.True);
            Assert.That(CdnConfig.IsModsPackUrl("https://cdn2.ardysamods.my.id/Assets/test.zip"), Is.True);
            Assert.That(CdnConfig.IsModsPackUrl("https://otherdomain.com/test.zip"), Is.False);
        }
    }
}
