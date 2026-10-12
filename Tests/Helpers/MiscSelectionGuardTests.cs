using System.Collections.Generic;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Models;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Helpers
{
    [TestFixture]
    public class MiscSelectionGuardTests
    {
        private static readonly List<MiscOption> Options =
        [
            new MiscOption
            {
                Id = "Courier",
                Choices = ["Default Courier", "Ageless Apothecary", "Bearzky"],
                ChoiceStyles = new() { ["Bearzky"] = ["Bearzky (Style 0)", "Bearzky (Style 1)"] },
            },
            new MiscOption { Id = "Ward", Choices = ["Default Ward", "Frosty the North Ward"] },
            new MiscOption { Id = "HUD", Choices = ["Default HUD", "TI HUD"] },
        ];

        private static Dictionary<string, string> Sanitize(Dictionary<string, string> input, out List<string> dropped) =>
            MiscSelectionGuard.Sanitize(Options, input, out dropped);

        [Test]
        public void Sanitize_KeepsValidPicksAndStyles()
        {
            var result = Sanitize(new() { ["Courier"] = "Bearzky (Style 1)", ["HUD"] = "TI HUD" }, out var dropped);

            Assert.That(result, Is.EquivalentTo(new Dictionary<string, string> { ["Courier"] = "Bearzky (Style 1)", ["HUD"] = "TI HUD" }));
            Assert.That(dropped, Is.Empty);
        }

        [Test]
        public void Sanitize_CanonicalizesKeyAndValueCase()
        {
            var result = Sanitize(new() { ["courier"] = "ageless apothecary" }, out var dropped);

            Assert.That(result.Keys, Is.EqualTo(new[] { "Courier" }));
            Assert.That(result["Courier"], Is.EqualTo("Ageless Apothecary"));
            Assert.That(dropped, Is.Empty);
        }

        [Test]
        public void Sanitize_DropsUnknownOptionsAndChoices()
        {
            var result = Sanitize(new() { ["RemovedOption"] = "Anything", ["HUD"] = "Retired HUD" }, out var dropped);

            Assert.That(result, Is.Empty);
            Assert.That(dropped, Is.EquivalentTo(new[] { "RemovedOption", "HUD" }));
        }

        [Test]
        public void Sanitize_EmptyValuesAreNotSetNotDropped()
        {
            var result = Sanitize(new() { ["HUD"] = "", ["CourierEthereal"] = "  " }, out var dropped);

            Assert.That(result, Is.Empty);
            Assert.That(dropped, Is.Empty);
        }

        [Test]
        public void Sanitize_EtherealKeepsKnownEffectsUpToTheSlotCap()
        {
            var result = Sanitize(new() { ["courierethereal"] = "dark moon, Ionic Vapor,Desert Sands" }, out var dropped);

            Assert.That(result["CourierEthereal"], Is.EqualTo("Dark Moon,Ionic Vapor"));
            Assert.That(dropped, Is.EqualTo(new[] { "courierethereal" }), "the third effect did not fit");
        }

        [Test]
        public void Sanitize_EtherealDropsUnknownEffectNames()
        {
            var result = Sanitize(new() { ["WardEthereal"] = "Dark Moon,Not An Effect" }, out var dropped);

            Assert.That(result["WardEthereal"], Is.EqualTo("Dark Moon"));
            Assert.That(dropped, Is.EqualTo(new[] { "WardEthereal" }));
        }

        [Test]
        public void Sanitize_EtherealOnAnOptionWithoutEtherealIsDropped()
        {
            var result = Sanitize(new() { ["HUDEthereal"] = "Dark Moon" }, out var dropped);

            Assert.That(result, Is.Empty);
            Assert.That(dropped, Is.EqualTo(new[] { "HUDEthereal" }));
        }

        [Test]
        public void Sanitize_NullInputIsEmpty()
        {
            Assert.That(MiscSelectionGuard.Sanitize(Options, null, out var dropped), Is.Empty);
            Assert.That(dropped, Is.Empty);
        }

        [Test]
        public void HasCustomSelections_DefaultAndDisableChoicesAreNoChange()
        {
            Assert.That(MiscSelectionGuard.HasCustomSelections(new Dictionary<string, string>()), Is.False);
            Assert.That(MiscSelectionGuard.HasCustomSelections(new Dictionary<string, string>
            {
                ["Courier"] = "Default Courier",
                ["Effect"] = "Disable Effect",
                ["legacy"] = "default",
                ["HUD"] = "",
            }), Is.False);
        }

        [Test]
        public void HasCustomSelections_RealPickCounts()
        {
            Assert.That(MiscSelectionGuard.HasCustomSelections(new Dictionary<string, string>
            {
                ["Courier"] = "Default Courier",
                ["HUD"] = "TI HUD",
            }), Is.True);
        }

        [Test]
        public void HasCustomSelections_EtherealOnTheDefaultCourierCounts()
        {
            Assert.That(MiscSelectionGuard.HasCustomSelections(new Dictionary<string, string>
            {
                ["Courier"] = "Default Courier",
                ["CourierEthereal"] = "Dark Moon",
            }), Is.True);
        }
    }
}
