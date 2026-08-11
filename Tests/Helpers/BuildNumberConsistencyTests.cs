using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Helpers
{
    [TestFixture]
    public class BuildNumberConsistencyTests
    {
        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "build.txt")))
                dir = dir.Parent;
            Assert.That(dir, Is.Not.Null, "could not locate the repo root (no build.txt in any parent)");
            return dir!.FullName;
        }

        [Test]
        public void BuildTxt_MatchesCsprojAssemblyAndFileVersion()
        {
            var root = RepoRoot();
            var build = File.ReadAllText(Path.Combine(root, "build.txt")).Trim();
            var csproj = File.ReadAllText(Path.Combine(root, "ArdysaModsTools.csproj"));

            Assert.That(build, Does.Match(@"^\d+$"), "build.txt must hold a bare build number");

            var assembly = Regex.Match(csproj, @"<AssemblyVersion>([^<]+)</AssemblyVersion>");
            var file = Regex.Match(csproj, @"<FileVersion>([^<]+)</FileVersion>");
            Assert.That(assembly.Success, Is.True, "csproj has no <AssemblyVersion>");
            Assert.That(file.Success, Is.True, "csproj has no <FileVersion>");

            var assemblyBuild = assembly.Groups[1].Value.Split('.')[^1];
            var fileBuild = file.Groups[1].Value.Split('.')[^1];

            Assert.Multiple(() =>
            {
                Assert.That(assemblyBuild, Is.EqualTo(build),
                    $"AssemblyVersion ends in {assemblyBuild} but build.txt says {build} — bump both in the same commit");
                Assert.That(fileBuild, Is.EqualTo(build),
                    $"FileVersion ends in {fileBuild} but build.txt says {build} — bump both in the same commit");
            });
        }

        [Test]
        public void Csproj_HasNoLeftoverConflictMarkers()
        {
            var csproj = File.ReadAllText(Path.Combine(RepoRoot(), "ArdysaModsTools.csproj"));
            Assert.That(Regex.IsMatch(csproj, @"^(<<<<<<<|=======|>>>>>>>)", RegexOptions.Multiline), Is.False,
                "ArdysaModsTools.csproj still contains merge conflict markers");
        }
    }
}
