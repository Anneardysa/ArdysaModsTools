using System;
using System.IO;
using ArdysaModsTools.UI;
using ArdysaModsTools.UI.Helpers;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Helpers
{
    [TestFixture]
    public class WebViewThemingTests
    {
        private const string Page = "<!DOCTYPE html>\n<html lang=\"en\">\n<head><title>x</title></head>\n<body></body>\n</html>";

        private bool _originalDark;

        [SetUp]
        public void SetUp() => _originalDark = Theme.IsDarkMode;

        [TearDown]
        public void TearDown() => Theme.SetTheme(_originalDark);

        [Test]
        public void Apply_Light_StampsThemeAttributeAndStylesheet()
        {
            Theme.SetTheme(darkMode: false);

            var html = WebViewTheming.Apply(Page);

            Assert.That(html, Does.Contain("<html lang=\"en\" data-theme=\"light\">"));
            Assert.That(html, Does.Contain("id=\"amt-theme\""), "light overrides must ride in with the page");
            Assert.That(html.IndexOf("id=\"amt-theme\"", StringComparison.Ordinal),
                Is.LessThan(html.IndexOf("<body>", StringComparison.Ordinal)),
                "the stylesheet must be in <head> — after <body> the page paints dark first");
        }

        [Test]
        public void Apply_Dark_LeavesMarkupOnTheShippedLook()
        {
            Theme.SetTheme(darkMode: true);

            var html = WebViewTheming.Apply(Page);

            Assert.That(html, Does.Contain("<html lang=\"en\">"), "dark is the default: the tag carries no data-theme");
            Assert.That(html, Does.Contain("id=\"amt-theme\""), "the stylesheet still ships so a live flip is attribute-only");
        }

        [Test]
        public void Apply_PageWithoutAnchors_IsReturnedIntact()
        {
            Theme.SetTheme(darkMode: false);

            const string malformed = "<div>no html tag, no head</div>";

            Assert.That(WebViewTheming.Apply(malformed), Is.EqualTo(malformed),
                "a page we cannot splice must load unthemed, never corrupted");
        }

        [Test]
        public void BuildBootstrapScript_CarriesStylesheetAndMatchesTheme()
        {
            Theme.SetTheme(darkMode: false);
            var light = WebViewTheming.BuildBootstrapScript();
            Assert.That(light, Does.Contain("amt-theme"));
            Assert.That(light, Does.Contain("data-theme"));
            Assert.That(light, Does.Contain("setAttribute"));

            Theme.SetTheme(darkMode: true);
            Assert.That(WebViewTheming.BuildBootstrapScript(), Does.Contain("removeAttribute"));
        }

        [Test]
        public void SetThemeScript_TogglesTheAttributeBothWays()
        {
            Theme.SetTheme(darkMode: false);
            Assert.That(WebViewTheming.SetThemeScript(), Does.Contain("setAttribute('data-theme','light')"));

            Theme.SetTheme(darkMode: true);
            Assert.That(WebViewTheming.SetThemeScript(), Does.Contain("removeAttribute('data-theme')"));
        }

        [Test]
        public void EveryHtmlAsset_KeepsTheInjectionAnchors()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "Html")))
                dir = dir.Parent;

            Assert.That(dir, Is.Not.Null, "Could not locate Assets/Html in any parent directory");

            var htmlDir = Path.Combine(dir!.FullName, "Assets", "Html");
            Assert.That(File.Exists(Path.Combine(htmlDir, "theme.css")), Is.True, "theme.css must ship next to the pages");

            foreach (var file in Directory.GetFiles(htmlDir, "*.html"))
            {
                var text = File.ReadAllText(file);
                var name = Path.GetFileName(file);

                Assert.That(text, Does.Contain("<html lang=\"en\""), $"{name} lost the <html lang=\"en\"> anchor");
                Assert.That(text, Does.Contain("</head>"), $"{name} lost the </head> anchor");
            }
        }
    }
}
