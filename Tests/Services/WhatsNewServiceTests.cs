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
    public class WhatsNewServiceTests
    {
        [Test]
        public void Parse_ValidReleases_MapsFieldsInOrder()
        {
            const string json = """
            [
              { "tag_name": "2.1.27-beta", "name": "Build 2149", "body": "- Fixed things",
                "html_url": "https://github.com/x/y/releases/tag/2.1.27-beta",
                "draft": false, "prerelease": true, "published_at": "2026-06-14T10:00:00Z" },
              { "tag_name": "2.1.26", "name": "", "body": "Notes",
                "html_url": "https://github.com/x/y/releases/tag/2.1.26",
                "draft": false, "published_at": "2026-06-01T00:00:00Z" }
            ]
            """;

            var notes = WhatsNewService.Parse(json);

            Assert.That(notes, Is.Not.Null);
            Assert.That(notes!, Has.Count.EqualTo(2));
            Assert.That(notes[0].Tag, Is.EqualTo("2.1.27-beta"));
            Assert.That(notes[0].Name, Is.EqualTo("Build 2149"));
            Assert.That(notes[0].Body, Is.EqualTo("- Fixed things"));
            Assert.That(notes[0].Date, Is.Not.Null);
            Assert.That(notes[1].Name, Is.EqualTo("2.1.26"));
        }

        [Test]
        public void Parse_R2FeedShape_ParsesVerbatim()
        {
            const string feedJson = """
            [
              { "tag_name": "2.2.1-beta", "name": "Build 2157", "body": "- Restructured README",
                "html_url": "https://ardysamods.my.id/whatsnew", "published_at": "2026-06-17T00:00:00Z" }
            ]
            """;

            var notes = WhatsNewService.Parse(feedJson);

            Assert.That(notes, Is.Not.Null);
            Assert.That(notes!, Has.Count.EqualTo(1));
            Assert.That(notes[0].Tag, Is.EqualTo("2.2.1-beta"));
            Assert.That(notes[0].Name, Is.EqualTo("Build 2157"));
            Assert.That(notes[0].HtmlUrl, Is.EqualTo("https://ardysamods.my.id/whatsnew"));
            Assert.That(notes[0].Date, Is.Not.Null);
        }

        [Test]
        public void Parse_ExcludesDrafts()
        {
            const string json = """
            [
              { "tag_name": "v2", "name": "Real", "draft": false },
              { "tag_name": "v3", "name": "Draft one", "draft": true }
            ]
            """;

            var notes = WhatsNewService.Parse(json);

            Assert.That(notes, Is.Not.Null);
            Assert.That(notes!, Has.Count.EqualTo(1));
            Assert.That(notes[0].Tag, Is.EqualTo("v2"));
        }

        [Test]
        public void Parse_EmptyOrInvalid_ReturnsNull()
        {
            Assert.That(WhatsNewService.Parse(null), Is.Null);
            Assert.That(WhatsNewService.Parse(""), Is.Null);
            Assert.That(WhatsNewService.Parse("   "), Is.Null);
            Assert.That(WhatsNewService.Parse("[]"), Is.Null);
            Assert.That(WhatsNewService.Parse("not json"), Is.Null);
        }
    }
}
