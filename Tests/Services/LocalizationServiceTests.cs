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
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ArdysaModsTools.Core.Services.Localization;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class LocalizationServiceTests
    {
        private string _localesDir = null!;

        [SetUp]
        public void SetUp()
        {
            var currentDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (currentDir != null && !Directory.Exists(Path.Combine(currentDir.FullName, "Assets", "Locales")))
            {
                currentDir = currentDir.Parent;
            }

            Assert.That(currentDir, Is.Not.Null, "Could not locate Assets/Locales directory in parents of AppDomain.CurrentDomain.BaseDirectory");
            _localesDir = Path.Combine(currentDir!.FullName, "Assets", "Locales");
        }

        [Test]
        public void ResolveSupported_MatchesCulturesCorrectly()
        {
            Assert.That(LocalizationService.ResolveSupported("en"), Is.EqualTo("en"));
            Assert.That(LocalizationService.ResolveSupported("ru"), Is.EqualTo("ru"));
            Assert.That(LocalizationService.ResolveSupported("es"), Is.EqualTo("es"));
            Assert.That(LocalizationService.ResolveSupported("de"), Is.EqualTo("de"));
            Assert.That(LocalizationService.ResolveSupported("fr"), Is.EqualTo("fr"));
            Assert.That(LocalizationService.ResolveSupported("pt"), Is.EqualTo("pt"));
            Assert.That(LocalizationService.ResolveSupported("zh-Hans"), Is.EqualTo("zh-Hans"));
            Assert.That(LocalizationService.ResolveSupported("zh-Hant"), Is.EqualTo("zh-Hant"));

            Assert.That(LocalizationService.ResolveSupported("en-US"), Is.EqualTo("en"));
            Assert.That(LocalizationService.ResolveSupported("de-DE"), Is.EqualTo("de"));
            Assert.That(LocalizationService.ResolveSupported("es-ES"), Is.EqualTo("es"));
            Assert.That(LocalizationService.ResolveSupported("fr-CA"), Is.EqualTo("fr"));
            Assert.That(LocalizationService.ResolveSupported("pt-BR"), Is.EqualTo("pt"));

            Assert.That(LocalizationService.ResolveSupported("zh-CN"), Is.EqualTo("zh-Hans"));
            Assert.That(LocalizationService.ResolveSupported("zh-TW"), Is.EqualTo("zh-Hant"));
            Assert.That(LocalizationService.ResolveSupported("zh-HK"), Is.EqualTo("zh-Hant"));
            Assert.That(LocalizationService.ResolveSupported("zh-MO"), Is.EqualTo("zh-Hant"));
            Assert.That(LocalizationService.ResolveSupported("zh-Hant-HK"), Is.EqualTo("zh-Hant"));

            Assert.That(LocalizationService.ResolveSupported(null), Is.EqualTo("en"));
            Assert.That(LocalizationService.ResolveSupported("  "), Is.EqualTo("en"));
            Assert.That(LocalizationService.ResolveSupported("it"), Is.EqualTo("en"));
        }

        [Test]
        public void LocaleFiles_AllSupportedLanguages_AreValidJson()
        {
            foreach (var code in LocalizationService.SupportedCodes)
            {
                var filePath = Path.Combine(_localesDir, $"{code}.json");
                Assert.That(File.Exists(filePath), Is.True, $"Locale catalog '{code}.json' does not exist at: {filePath}");

                var json = File.ReadAllText(filePath);
                Assert.DoesNotThrow(() =>
                {
                    var map = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    Assert.That(map, Is.Not.Null, $"Failed to deserialize '{code}.json' to Dictionary<string, string>");
                }, $"Parsing '{code}.json' failed with invalid JSON formatting.");
            }
        }

        [Test]
        public void LocaleFiles_CompareKeysAgainstEnglish_NoGapsAllowed()
        {
            var enPath = Path.Combine(_localesDir, "en.json");
            var enJson = File.ReadAllText(enPath);
            var enKeys = JsonSerializer.Deserialize<Dictionary<string, string>>(enJson)!.Keys;

            var missingKeysReport = new List<string>();

            foreach (var code in LocalizationService.SupportedCodes)
            {
                if (code == "en") continue;

                var path = Path.Combine(_localesDir, $"{code}.json");
                var json = File.ReadAllText(path);
                var keys = new HashSet<string>(JsonSerializer.Deserialize<Dictionary<string, string>>(json)!.Keys);

                foreach (var enKey in enKeys)
                {
                    if (!keys.Contains(enKey))
                    {
                        missingKeysReport.Add($"Missing key '{enKey}' in '{code}.json'");
                    }
                }
            }

            Assert.That(missingKeysReport, Is.Empty,
                "Translation catalog mismatch detected!\n" + string.Join("\n", missingKeysReport));
        }

        [Test]
        public void LocalizationService_LoadsAndTranslatesCorrectly()
        {
            var service = new LocalizationService();

            Assert.That(service.CurrentCode, Is.EqualTo("en"));
            Assert.That(service.T("settings.close"), Is.EqualTo("Close"));

            service.SetCulture("de");
            Assert.That(service.CurrentCode, Is.EqualTo("de"));
            Assert.That(service.T("settings.close"), Is.EqualTo("Schließen"));

            service.SetCulture("en");
            Assert.That(service.CurrentCode, Is.EqualTo("en"));
        }
    }
}
