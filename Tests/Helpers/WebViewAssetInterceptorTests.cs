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
using ArdysaModsTools.UI.Helpers;

namespace ArdysaModsTools.Tests.Helpers
{
    [TestFixture]
    public class WebViewAssetInterceptorTests
    {
        #region GetContentType Tests

        [TestCase("https://cdn.ardysamods.my.id/Assets/misc/weather/rainy.webp", "image/webp")]
        [TestCase("https://cdn.ardysamods.my.id/Assets/misc/courier/baby_roshan.png", "image/png")]
        [TestCase("https://cdn.ardysamods.my.id/x/a.jpg", "image/jpeg")]
        [TestCase("https://cdn.ardysamods.my.id/x/a.jpeg", "image/jpeg")]
        [TestCase("https://cdn.ardysamods.my.id/x/a.gif", "image/gif")]
        [TestCase("https://cdn.ardysamods.my.id/x/a.svg", "image/svg+xml")]
        [TestCase("https://cdn.ardysamods.my.id/x/a.ico", "image/x-icon")]
        [TestCase("https://cdn.ardysamods.my.id/config/misc_config.json", "application/json")]
        public void GetContentType_KnownExtensions_ReturnsExpectedMime(string url, string expected)
        {
            Assert.That(WebViewAssetInterceptor.GetContentType(url), Is.EqualTo(expected));
        }

        [Test]
        public void GetContentType_UnknownExtension_ReturnsOctetStream()
        {
            Assert.That(
                WebViewAssetInterceptor.GetContentType("https://cdn.ardysamods.my.id/x/file.bin"),
                Is.EqualTo("application/octet-stream"));
        }

        [Test]
        public void GetContentType_WithQueryString_IgnoresQueryWhenResolvingExtension()
        {
            Assert.That(
                WebViewAssetInterceptor.GetContentType("https://cdn.ardysamods.my.id/x/a.png?v=2026061400"),
                Is.EqualTo("image/png"));
        }

        [Test]
        public void GetContentType_IsCaseInsensitive()
        {
            Assert.That(
                WebViewAssetInterceptor.GetContentType("https://cdn.ardysamods.my.id/x/A.WEBP"),
                Is.EqualTo("image/webp"));
        }

        #endregion

        #region BuildImageFilter Tests

        [Test]
        public void BuildImageFilter_TrimsTrailingSlashAndAppendsWildcard()
        {
            Assert.That(
                WebViewAssetInterceptor.BuildImageFilter("https://cdn.ardysamods.my.id/"),
                Is.EqualTo("https://cdn.ardysamods.my.id/*"));
        }

        [Test]
        public void BuildImageFilter_WithoutTrailingSlash_AppendsWildcard()
        {
            Assert.That(
                WebViewAssetInterceptor.BuildImageFilter("https://cdn.ardysamods.my.id"),
                Is.EqualTo("https://cdn.ardysamods.my.id/*"));
        }

        #endregion
    }
}
