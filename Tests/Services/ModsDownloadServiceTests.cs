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
using ArdysaModsTools.Core.Services;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class ModsDownloadServiceTests
    {
        [Test]
        public void Parse_ValidLinks_ReturnsBoth()
        {
            const string json = """
            { "mega": "https://mega.nz/file/abc", "mediafire": "https://www.mediafire.com/file/x/m.zip" }
            """;

            var config = ModsDownloadService.Parse(json);

            Assert.That(config, Is.Not.Null);
            Assert.That(config!.Mega, Is.EqualTo("https://mega.nz/file/abc"));
            Assert.That(config.Mediafire, Is.EqualTo("https://www.mediafire.com/file/x/m.zip"));
        }

        [Test]
        public void Parse_NonHttpScheme_IsDropped()
        {
            const string json = """
            { "mega": "file:///C:/Windows/System32/calc.exe", "mediafire": "https://ok.example/m.zip" }
            """;

            var config = ModsDownloadService.Parse(json);

            Assert.That(config, Is.Not.Null);
            Assert.That(config!.Mega, Is.Null);
            Assert.That(config.Mediafire, Is.EqualTo("https://ok.example/m.zip"));
        }

        [Test]
        public void Parse_NoUsableLinks_ReturnsNull()
        {
            Assert.That(ModsDownloadService.Parse("""{ "mega": "javascript:alert(1)" }"""), Is.Null);
            Assert.That(ModsDownloadService.Parse("{}"), Is.Null);
            Assert.That(ModsDownloadService.Parse(""), Is.Null);
            Assert.That(ModsDownloadService.Parse("not json"), Is.Null);
        }
    }
}
