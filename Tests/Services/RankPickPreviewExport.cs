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
using System.IO;
using System.Linq;
using System.Text.Json;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services.Utilities;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture, Explicit("Writes the Rank Changer picking-label preview; run by name.")]
    public class RankPickPreviewExport
    {
        private static readonly int[] Placings = { 1, 6, 10, 50, 100, 500, 1000, 2691, 4471, 5000 };

        [Test]
        public void Export()
        {
            string dotaRoot = Environment.GetEnvironmentVariable("AMT_DOTA_ROOT")
                ?? @"C:\Program Files (x86)\Steam\steamapps\common\dota 2 beta";
            string font = Path.Combine(dotaRoot, DotaPaths.PanoramaFonts.Replace('/', '\\'), "radiance-regular.otf");
            if (!File.Exists(font)) Assert.Ignore($"Dota not found at {dotaRoot} (set AMT_DOTA_ROOT).");

            string repo = RepoRoot();
            string outDir = Environment.GetEnvironmentVariable("AMT_RANK_PREVIEW_OUT")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "amt-rank-preview");
            Directory.CreateDirectory(outDir);

            var texts = RankTierNamesTests.AllAcceptedRequests().Select(RankTierNames.English.Label)
                .Concat(Placings.Select(RankTierNames.English.LeaderboardLabel))
                .Distinct().ToList();
            var labels = texts.Select(t => new
            {
                text = t,
                png = Convert.ToBase64String(Png(VtexGenerator.GeneratePickRankTexture(t, dotaRoot))),
            });

            var data = new
            {
                box = new { h = VtexGenerator.PickRankCssBoxHeightPx, w = VtexGenerator.PickRankCssBoxWidthPx },
                font = Convert.ToBase64String(File.ReadAllBytes(font)),
                labels,
                built = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            };
            File.WriteAllText(Path.Combine(outDir, "data.js"), "window.RANK_PREVIEW = " + JsonSerializer.Serialize(data) + ";\n");
            File.Copy(Path.Combine(repo, "scripts", "rank_preview", "preview.html"), Path.Combine(outDir, "preview.html"), overwrite: true);

            var (request, onlyMine) = Request(Environment.GetEnvironmentVariable("AMT_RANK_PREVIEW"));
            RankOverridePlan.Build(request);
            string label = RankTierNames.English.Label(request);
            byte[] sheet = File.ReadAllBytes(Path.Combine(repo, "Tests", "Data", "RankChanger", "dota_hud_hero_picking_player.vcss_c"));

            string tree = Path.Combine(outDir, "_ArdysaMods");
            if (Directory.Exists(tree)) Directory.Delete(tree, recursive: true);
            Write(tree, PanoramaCssPatcher.PickPhaseSheet, PanoramaCssPatcher.PatchHeroPickingPlayer(sheet, onlyMine, forceVisible: true));
            Write(tree, PanoramaCssPatcher.PickLabelTexture, VtexGenerator.GeneratePickRankTexture(label, dotaRoot));
            File.WriteAllText(Path.Combine(outDir, "ingame.txt"),
                $"label: {label}\nscope: {(onlyMine ? "your slot only (.IsLocalPlayerPawn)" : "every slot")}\nbuilt: {data.built}\n");

            TestContext.Out.WriteLine($"preview: {Path.Combine(outDir, "preview.html")}");
            TestContext.Out.WriteLine($"in-game: \"{label}\" ({(onlyMine ? "mine" : "every")}) -> {tree}");
        }

        private static (RankChangerRequest, bool onlyMine) Request(string? spec)
        {
            string[] p = (spec ?? "8,5,3,1,mine").Split(',', StringSplitOptions.TrimEntries);
            if (p.Length != 5 || (p[4] != "mine" && p[4] != "every"))
                throw new ArgumentException($"AMT_RANK_PREVIEW must be tier,stars,bracket,placing,mine|every - got '{spec}'");
            bool mine = p[4] == "mine";
            return (new RankChangerRequest(!mine, 4, 1, int.Parse(p[0]), int.Parse(p[1]),
                TargetLeaderboard: int.Parse(p[2]), LeaderboardRank: int.Parse(p[3])), mine);
        }

        private static byte[] Png(byte[] vtex) => vtex.Skip(VtexGenerator.TemplateHeaderSize).ToArray();

        private static void Write(string root, string rel, byte[] bytes)
        {
            string p = Path.Combine(root, rel.Replace('/', '\\'));
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            File.WriteAllBytes(p, bytes);
        }

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "build.txt"))) dir = dir.Parent;
            Assert.That(dir, Is.Not.Null, "could not locate the repository root");
            return dir!.FullName;
        }
    }
}
