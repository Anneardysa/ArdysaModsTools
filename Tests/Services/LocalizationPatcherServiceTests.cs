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
using System.Linq;
using System.Text;
using ArdysaModsTools.Core.Services;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class LocalizationPatcherServiceTests
    {
        private static byte[] WithBom(string content) =>
            Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(content)).ToArray();

        [Test]
        public void LooksLikeLocalization_RealFileWithBom_Accepted()
        {
            var data = WithBom("\"lang\"\r\n{\r\n\t\"Language\" \"English\"\r\n");

            Assert.That(LocalizationPatcherService.LooksLikeLocalization(data), Is.True);
        }

        [Test]
        public void LooksLikeLocalization_NoBom_Accepted()
        {
            var data = Encoding.UTF8.GetBytes("\"lang\"\r\n{\r\n\t\"Language\" \"French\"\r\n");

            Assert.That(LocalizationPatcherService.LooksLikeLocalization(data), Is.True);
        }

        [Test]
        public void LooksLikeLocalization_LeadingWhitespace_Accepted()
        {
            var data = WithBom("\r\n  \"lang\"\r\n{\r\n");

            Assert.That(LocalizationPatcherService.LooksLikeLocalization(data), Is.True);
        }

        [Test]
        public void LooksLikeLocalization_CloudflareChallengePage_Rejected()
        {
            var data = Encoding.UTF8.GetBytes(
                "<!DOCTYPE html><html><head><title>Just a moment...</title></head><body>" +
                "<script src=\"/cdn-cgi/challenge-platform/h/b/orchestrate/chl_page\"></script></body></html>");

            Assert.That(LocalizationPatcherService.LooksLikeLocalization(data), Is.False);
        }

        [Test]
        public void LooksLikeLocalization_WrongRootKey_Rejected()
        {
            var data = WithBom("\"Resource\"\r\n{\r\n");

            Assert.That(LocalizationPatcherService.LooksLikeLocalization(data), Is.False);
        }

        [Test]
        public void LooksLikeLocalization_EmptyOrTruncated_Rejected()
        {
            Assert.Multiple(() =>
            {
                Assert.That(LocalizationPatcherService.LooksLikeLocalization(null!), Is.False);
                Assert.That(LocalizationPatcherService.LooksLikeLocalization(System.Array.Empty<byte>()), Is.False);
                Assert.That(LocalizationPatcherService.LooksLikeLocalization(Encoding.UTF8.GetBytes("\"la")), Is.False);
                Assert.That(LocalizationPatcherService.LooksLikeLocalization(Encoding.UTF8.GetPreamble()), Is.False);
            });
        }

        [Test]
        public void FileCount_CoversEveryLanguageOnR2()
        {
            Assert.That(LocalizationPatcherService.FileCount, Is.EqualTo(28));
        }
    }
}
