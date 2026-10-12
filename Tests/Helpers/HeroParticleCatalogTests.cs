/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ArdysaModsTools.Core.Data;
using ArdysaModsTools.Models;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Helpers
{
    [TestFixture]
    public class HeroParticleCatalogTests
    {
        [Test]
        public void Catalog_IsTheKeptUnusualsPlusWyvernHatchling_WithUniqueIds()
        {
            Assert.That(HeroParticleCatalog.All, Has.Count.EqualTo(36));
            Assert.That(HeroParticleCatalog.All.Count(p => p.Source == "unusual"), Is.EqualTo(35), "kept named unusuals");
            Assert.That(HeroParticleCatalog.All.Where(p => p.Source != "unusual").Select(p => p.Fit), Has.All.EqualTo("model"));
            Assert.That(HeroParticleCatalog.All.Select(p => p.Id.ToLowerInvariant()), Is.Unique);
            Assert.That(HeroParticleCatalog.All.Select(p => p.Path.ToLowerInvariant()), Is.Unique);
        }

        [Test]
        public void Catalog_EveryEntryIsWritableAndReadable()
        {
            foreach (var p in HeroParticleCatalog.All)
            {
                Assert.That(p.Path, Does.Match(@"^particles/[A-Za-z0-9_\-/]+\.vpcf$"), p.Id);
                Assert.That(p.Source, Is.AnyOf("unusual", "courier"), p.Id);
                Assert.That(p.Fit, Is.AnyOf("model", "ground", "hang"), p.Id);
                Assert.That(p.Pools == null || p.Source == "unusual", Is.True, $"{p.Id}: only unusuals roll in a pool");
                Assert.That(p.Name, Is.Not.Empty.And.Not.Contain("_").And.Not.Contain("—"), $"{p.Id}: a name a player reads");
            }
        }

        [Test]
        public void WithPreview_NamesOnlyCatalogIds()
        {
            Assert.That(HeroParticleCatalog.WithPreview, Has.Count.EqualTo(36));
            foreach (var id in HeroParticleCatalog.WithPreview)
                Assert.That(HeroParticleCatalog.TryGet(id, out _), Is.True, $"{id}: preview for an id the catalog lacks");
            Assert.That(HeroParticleCatalog.WithPreview.Contains("22"), Is.True, "Affliction of Vermin has a GIF");
        }

        [Test]
        public void TryGet_KnownUnknownAndBlank()
        {
            Assert.That(HeroParticleCatalog.TryGet("833", out var gold), Is.True);
            Assert.That(gold.Name, Is.EqualTo("Gold Drop"));
            Assert.That(HeroParticleCatalog.TryGet(" 833 ", out _), Is.True);
            Assert.That(HeroParticleCatalog.TryGet("nope", out _), Is.False);
            Assert.That(HeroParticleCatalog.TryGet(null, out _), Is.False);
        }

        [Test]
        public void HostsFor_Juggernaut_IsEveryOneOfItsOwnDefaultItems()
        {
            var jugg = new HeroModel
            {
                HeroId = "npc_dota_hero_juggernaut",
                Name = "Juggernaut",
                ItemIds = new List<int> { 6, 7, 8, 9, 62, 811, 560, 610, 253, 7 }
            };

            var hosts = HeroParticleCatalog.HostsFor(jugg);

            Assert.That(hosts.Select(h => h.Id), Is.EqualTo(new[] { "6", "7", "8", "9", "62", "811", "560", "610" }));
            Assert.That(hosts.Single(h => h.Id == "7").Label, Is.EqualTo("Weapon"), "the weapon's slot name is empty");
        }

        [Test]
        public void HostsFor_NoHeroOrNoItems_IsEmpty()
        {
            Assert.That(HeroParticleCatalog.HostsFor(null), Is.Empty);
            Assert.That(HeroParticleCatalog.HostsFor(new HeroModel { HeroId = "npc_dota_hero_juggernaut" }), Is.Empty);
        }

        [Test]
        public void MaxPerHero_MatchesTheGalleryPage()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "build.txt"))) dir = dir.Parent;
            Assert.That(dir, Is.Not.Null, "could not locate the repository root");
            var helpers = File.ReadAllText(Path.Combine(dir!.FullName, "Frontend", "src", "pages", "hero_gallery", "helpers.ts"));

            var m = Regex.Match(helpers, @"export const MAX_PARTICLES\s*=\s*(\d+)");
            Assert.That(m.Success, Is.True, "MAX_PARTICLES not found in helpers.ts");
            Assert.That(int.Parse(m.Groups[1].Value), Is.EqualTo(HeroParticleCatalog.MaxPerHero));
        }
    }
}
