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
using System.Collections.Generic;
using ArdysaModsTools.Helpers;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Helpers
{
    [TestFixture]
    public class UIHelpersUrlValidationTests
    {
        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void OpenUrl_NullOrEmptyUrl_ReturnsFalseAndLogsError(string? invalidUrl)
        {
            var logs = new List<string>();
            bool result = UIHelpers.OpenUrl(invalidUrl, msg => logs.Add(msg));

            Assert.That(result, Is.False);
            Assert.That(logs, Has.Count.EqualTo(1));
            Assert.That(logs[0], Does.Contain("empty or null"));
        }

        [Test]
        [TestCase("file:///C:/Windows/System32/calc.exe")]
        [TestCase("file://C:/Windows/System32/cmd.exe")]
        [TestCase("powershell.exe")]
        [TestCase("cmd.exe /c echo test")]
        [TestCase("javascript:alert(1)")]
        [TestCase("data:text/html,<script>alert(1)</script>")]
        [TestCase("ms-settings:privacy")]
        [TestCase("relative/path/file.exe")]
        public void OpenUrl_UntrustedOrDangerousScheme_BlocksExecutionAndReturnsFalse(string dangerousUrl)
        {
            var logs = new List<string>();
            bool result = UIHelpers.OpenUrl(dangerousUrl, msg => logs.Add(msg));

            Assert.That(result, Is.False, $"URL '{dangerousUrl}' should be blocked for security.");
            Assert.That(logs, Has.Count.AtLeast(1));
            Assert.That(logs[0], Does.Contain("Invalid URL format").Or.Contain("Blocked opening URL with untrusted scheme"));
        }

        [Test]
        [TestCase("http://example.com")]
        [TestCase("https://ardysamods.my.id")]
        [TestCase("https://github.com/Anneardysa/ArdysaModsTools")]
        [TestCase("steam://rungameid/570")]
        public void OpenUrl_WhitelistedScheme_ValidatesSuccessfully(string validUrl)
        {
            var logs = new List<string>();
            
            bool result = UIHelpers.OpenUrl(validUrl, msg => logs.Add(msg));

            if (!result)
            {
                Assert.That(logs, Has.None.Matches<string>(m => m.Contains("Blocked opening URL with untrusted scheme") || m.Contains("Invalid URL format")),
                    $"URL '{validUrl}' should pass scheme validation.");
            }
        }
    }
}
