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
using System.Linq;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Helpers
{
    [TestFixture]
    public class CdnHeadRequestGuardTests
    {
        private static readonly HashSet<string> Allowlist = new(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine("Core", "Services", "Hero", "SkinSelectorCooldownService.cs"),
        };

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "build.txt")))
                dir = dir.Parent;
            Assert.That(dir, Is.Not.Null, "could not locate the repo root (no build.txt in any parent)");
            return dir!.FullName;
        }

        [Test]
        public void ShippedSource_ContainsNoDisallowedHttpMethodHead()
        {
            var root = RepoRoot();
            var offenders = new List<string>();

            foreach (var subdir in new[] { "Core", "UI", "Installer", "Updater" })
            {
                var dirPath = Path.Combine(root, subdir);
                if (!Directory.Exists(dirPath)) continue;

                foreach (var file in Directory.EnumerateFiles(dirPath, "*.cs", SearchOption.AllDirectories))
                {
                    var relative = Path.GetRelativePath(root, file);
                    if (Allowlist.Contains(relative)) continue;
                    if (relative.Contains(Path.Combine("obj", "")) || relative.Contains(Path.Combine("bin", ""))) continue;

                    var text = File.ReadAllText(file);
                    if (text.Contains("HttpMethod.Head"))
                        offenders.Add(relative);
                }
            }

            Assert.That(offenders, Is.Empty,
                "HttpMethod.Head against our own CDN bills an R2 Class B op on every call (Cloudflare " +
                "never edge-caches HEAD on the R2 custom domain). Use CdnProbe.ProbeAsync (a cacheable " +
                "ranged GET) instead, or add the file to Allowlist above with a comment justifying why " +
                "it targets a third-party host we don't pay for.\nOffending files:\n  " +
                string.Join("\n  ", offenders));
        }
    }
}
