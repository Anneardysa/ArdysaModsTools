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
        public void Apply_SplicesTheBundledFontFaces()
        {
            Theme.SetTheme(darkMode: true);

            var html = WebViewTheming.Apply(Page);

            Assert.That(html, Does.Contain("id=\"amt-fonts\""), "bundled fonts must ride in with the page");
            Assert.That(html, Does.Contain("@font-face"));
            Assert.That(html, Does.Contain("JetBrains Mono"));
            Assert.That(html.IndexOf("id=\"amt-fonts\"", StringComparison.Ordinal),
                Is.LessThan(html.IndexOf("<body>", StringComparison.Ordinal)),
                "the faces must be in <head> — declared after <body> the page paints in a fallback face first");
        }

        [Test]
        public void BuildBootstrapScript_OmitsTheFontPayload()
        {
            Theme.SetTheme(darkMode: true);

            var script = WebViewTheming.BuildBootstrapScript();

            Assert.That(script, Does.Contain("amt-theme"));
            Assert.That(script, Does.Not.Contain("@font-face"));
            Assert.That(script, Does.Not.Contain("base64"));
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
            var htmlDir = LocateHtmlDir();
            Assert.That(File.Exists(Path.Combine(htmlDir, "theme.css")), Is.True, "theme.css must ship next to the pages");

            foreach (var file in Directory.GetFiles(htmlDir, "*.html"))
            {
                var text = File.ReadAllText(file);
                var name = Path.GetFileName(file);

                Assert.That(text, Does.Contain("<html lang=\"en\""), $"{name} lost the <html lang=\"en\"> anchor");
                Assert.That(text, Does.Contain("</head>"), $"{name} lost the </head> anchor");
            }
        }

        [Test]
        public void NoShippedAsset_PullsFontsOrStylesFromACdn()
        {
            var htmlDir = LocateHtmlDir();

            string[] banned = { "fonts.googleapis.com", "fonts.gstatic.com", "cdn.tailwindcss.com" };

            foreach (var file in Directory.GetFiles(htmlDir, "*.*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext is not (".html" or ".css")) continue;

                var text = File.ReadAllText(file);
                foreach (var host in banned)
                    Assert.That(text, Does.Not.Contain($"//{host}"),
                        $"{Path.GetFileName(file)} loads from {host} — bundle it instead, the UI must work offline");
            }
        }

        [Test]
        public void PerformancePage_LinksTheBundledFontsAndVendoredTailwind()
        {
            var htmlDir = LocateHtmlDir();
            var page = File.ReadAllText(Path.Combine(htmlDir, "dota2_performance.html"));

            Assert.That(page, Does.Contain("href=\"fonts.css\""), "the file:// page links fonts.css itself");
            Assert.That(page, Does.Contain("src=\"vendor/tailwind.min.js\""), "Tailwind must load from the vendored copy");

            Assert.That(File.Exists(Path.Combine(htmlDir, "fonts.css")), Is.True,
                "fonts.css must ship — without it every page falls back to Consolas");
            Assert.That(File.Exists(Path.Combine(htmlDir, "vendor", "tailwind.min.js")), Is.True,
                "the vendored Tailwind must ship — without it the Performance page renders unstyled");
        }

        [Test]
        public void LightTheme_DarkensTheVerificationVerdictColours()
        {
            var htmlDir = LocateHtmlDir();
            var theme = File.ReadAllText(Path.Combine(htmlDir, "theme.css"));
            var shell = File.ReadAllText(Path.Combine(htmlDir, "main_shell.html"));

            Assert.Multiple(() =>
            {
                Assert.That(shell, Does.Contain("--verify-ok:"), "the shell must define the dark default");
                Assert.That(shell, Does.Contain("--verify-bad:"), "the shell must define the dark default");
                Assert.That(theme, Does.Contain("--verify-ok:"), "light theme must darken the pass colour");
                Assert.That(theme, Does.Contain("--verify-bad:"), "light theme must darken the fail colour");

                Assert.That(shell, Does.Contain("--verify-fix-ink:"));
                Assert.That(shell, Does.Not.Contain("background: #ffb432"),
                    "the amber fill must come from --verify-fix-bg so both themes stay in step");
            });
        }

        private static string LocateHtmlDir()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "Html")))
                dir = dir.Parent;

            Assert.That(dir, Is.Not.Null, "Could not locate Assets/Html in any parent directory");
            return Path.Combine(dir!.FullName, "Assets", "Html");
        }
    }
}
