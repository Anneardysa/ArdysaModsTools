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
using ArdysaModsTools.Core.Models;

namespace ArdysaModsTools.Tests.Models
{
    [TestFixture]
    public class MiscOptionTests
    {
        #region SanitizeChoice — CDN filename convention

        // These expectations are verified against the live CDN: the filenames follow the JS
        // getThumbUrl() convention (apostrophes/commas/&/curly-quotes removed, hyphens kept).
        [TestCase("Default Courier", "default_courier")]
        [TestCase("10th Anniversary Filmtail", "10th_anniversary_filmtail")]
        [TestCase("Collector's Baby Roshan 2017", "collectors_baby_roshan_2017")]
        [TestCase("Corsair, Son of the Storm", "corsair_son_of_the_storm")]
        [TestCase("Fezzle-Feez the Magic Carpet Smeevil", "fezzle-feez_the_magic_carpet_smeevil")]
        [TestCase("Anti-Mage", "anti-mage")]
        [TestCase("Hwytty & Shyzzyrd", "hwytty__shyzzyrd")]
        [TestCase("LGD's Golden Skipper", "lgds_golden_skipper")]
        [TestCase("Na'Vi's Weaselcrow", "navis_weaselcrow")]
        [TestCase("The Gaze of Scree'Auk", "the_gaze_of_screeauk")]
        public void SanitizeChoice_MatchesCdnConvention(string input, string expected)
        {
            Assert.That(MiscOption.SanitizeChoice(input), Is.EqualTo(expected));
        }

        [Test]
        public void SanitizeChoice_StripsCurlyApostrophe()
        {
            // Curly apostrophe (U+2019) must be removed like the JS ASCII-only \w class does.
            Assert.That(MiscOption.SanitizeChoice("Aghanim’s Baby Roshan"),
                Is.EqualTo("aghanims_baby_roshan"));
        }

        [TestCase("", "")]
        [TestCase(null, "")]
        public void SanitizeChoice_EmptyOrNull_ReturnsEmpty(string? input, string expected)
        {
            Assert.That(MiscOption.SanitizeChoice(input!), Is.EqualTo(expected));
        }

        #endregion

        #region GetThumbnailUrl

        [Test]
        public void GetThumbnailUrl_UsesSanitizedChoiceInPattern()
        {
            var opt = new MiscOption
            {
                ThumbnailUrlPattern = "https://cdn.ardysamods.my.id/Assets/misc/courier/{choice}.png"
            };

            Assert.That(opt.GetThumbnailUrl("Collector's Baby Roshan 2017"),
                Is.EqualTo("https://cdn.ardysamods.my.id/Assets/misc/courier/collectors_baby_roshan_2017.png"));
        }

        [Test]
        public void GetThumbnailUrl_PrefersExplicitChoiceThumbnail()
        {
            var opt = new MiscOption
            {
                ThumbnailUrlPattern = "https://cdn/{choice}.png",
                ChoiceThumbnails = { ["Some Choice"] = "https://cdn/explicit.png" }
            };

            Assert.That(opt.GetThumbnailUrl("Some Choice"), Is.EqualTo("https://cdn/explicit.png"));
        }

        [Test]
        public void GetThumbnailUrl_NoPattern_ReturnsNull()
        {
            var opt = new MiscOption();
            Assert.That(opt.GetThumbnailUrl("Anything"), Is.Null);
        }

        [TestCase("Default Courier")]
        [TestCase("Default Weather")]
        [TestCase("Disable Shader")]
        [TestCase("disable special")]
        public void GetThumbnailUrl_DefaultOrDisableChoice_ReturnsNull(string choice)
        {
            var opt = new MiscOption
            {
                ThumbnailUrlPattern = "https://cdn.ardysamods.my.id/Assets/misc/courier/{choice}.png"
            };
            Assert.That(opt.GetThumbnailUrl(choice), Is.Null);
        }

        #endregion

        #region IsDefaultChoice

        [TestCase("Default Courier", true)]
        [TestCase("Disable Shader", true)]
        [TestCase("  default weather", true)]
        [TestCase("DISABLE SPECIAL", true)]
        [TestCase("Collector's Baby Roshan 2017", false)]
        [TestCase("Crownfall", false)]
        [TestCase("", false)]
        public void IsDefaultChoice_DetectsNoOpChoices(string choice, bool expected)
        {
            Assert.That(MiscOption.IsDefaultChoice(choice), Is.EqualTo(expected));
        }

        #endregion
    }
}
