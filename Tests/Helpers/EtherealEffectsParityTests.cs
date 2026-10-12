using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ArdysaModsTools.Core.Data;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Helpers
{
    [TestFixture]
    public class EtherealEffectsParityTests
    {
        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "build.txt")))
                dir = dir.Parent;

            Assert.That(dir, Is.Not.Null, "could not locate the repository root");
            return dir!.FullName;
        }

        [Test]
        public void PageSource_ListsExactlyTheEffectsGenerationKnows()
        {
            var source = File.ReadAllText(Path.Combine(RepoRoot(), "Frontend", "src", "pages", "misc_form", "ethereal-effects.ts"));
            var pageNames = Regex.Matches(source, "^\\s*(?:\"([^\"]+)\"|([A-Za-z_]\\w*)):", RegexOptions.Multiline)
                .Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)
                .ToList();

            Assert.That(pageNames, Is.Not.Empty, "the regex no longer matches ethereal-effects.ts");
            Assert.That(pageNames, Is.EquivalentTo(EtherealEffects.Effects.Keys));
        }

        [Test]
        public void ShippedPage_CarriesEveryEffect()
        {
            var html = File.ReadAllText(Path.Combine(RepoRoot(), "Assets", "Html", "misc_form.html"));

            var missing = EtherealEffects.Effects.Keys.Where(k => !html.Contains(k, StringComparison.Ordinal)).ToList();
            Assert.That(missing, Is.Empty);
        }
    }
}
