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
using Moq;
using NUnit.Framework;
using ArdysaModsTools.Core.Controllers;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;

namespace ArdysaModsTools.Tests.Integration
{
    [TestFixture]
    public class MiscControllerResolutionTests
    {
        private Mock<IConflictDetector> _detector = null!;
        private Mock<IConflictResolver> _resolver = null!;
        private Mock<IModPriorityService> _priority = null!;
        private MiscController _controller = null!;

        [SetUp]
        public void Setup()
        {
            _detector = new Mock<IConflictDetector>();
            _resolver = new Mock<IConflictResolver>();
            _priority = new Mock<IModPriorityService>();

            _priority.Setup(p => p.LoadConfigAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ModPriorityConfig.CreateDefault());
            _priority.Setup(p => p.SaveConfigAsync(
                    It.IsAny<ModPriorityConfig>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _controller = new MiscController(_detector.Object, _resolver.Object, _priority.Object);
        }

        [Test]
        public async Task ApplyConflictResolutionsAsync_DropsLosingSelection_KeepsWinnerAndUnrelated()
        {
            var winner = ModSource.FromSelection("Weather", "Ash");
            var loser = ModSource.FromSelection("River", "Lava");
            var conflict = ModConflict.CreateCriticalConflict(winner, loser, "overlapping files");
            var chosen = conflict.AvailableResolutions.First();

            _resolver.Setup(r => r.ApplyUserChoiceAsync(conflict, chosen, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ConflictResolutionResult.Successful(
                    conflict.Id, ResolutionStrategy.Interactive, winner));

            var selections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Weather"] = "Ash",
                ["River"] = "Lava",
                ["HUD"] = "MyHud"
            };
            var userChoices = new Dictionary<string, ConflictResolutionOption> { [conflict.Id] = chosen };

            var (result, adjusted) = await _controller.ApplyConflictResolutionsAsync(
                new[] { conflict }, userChoices, selections, "C:\\dota", _ => { });

            Assert.That(result.Success, Is.True);
            Assert.That(adjusted.ContainsKey("River"), Is.False, "losing selection should be dropped");
            Assert.That(adjusted["Weather"], Is.EqualTo("Ash"), "winning selection must be retained");
            Assert.That(adjusted["HUD"], Is.EqualTo("MyHud"), "unrelated selection must be retained");
        }

        [Test]
        public async Task ApplyConflictResolutionsAsync_FailedResolution_RetainsAllSelections()
        {
            var winner = ModSource.FromSelection("Weather", "Ash");
            var loser = ModSource.FromSelection("River", "Lava");
            var conflict = ModConflict.CreateCriticalConflict(winner, loser, "overlapping files");
            var chosen = conflict.AvailableResolutions.First();

            _resolver.Setup(r => r.ApplyUserChoiceAsync(conflict, chosen, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ConflictResolutionResult.Failed(
                    conflict.Id, ResolutionStrategy.Interactive, "could not resolve"));

            var selections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Weather"] = "Ash",
                ["River"] = "Lava"
            };
            var userChoices = new Dictionary<string, ConflictResolutionOption> { [conflict.Id] = chosen };

            var (result, adjusted) = await _controller.ApplyConflictResolutionsAsync(
                new[] { conflict }, userChoices, selections, "C:\\dota", _ => { });

            Assert.That(result.Success, Is.True);
            Assert.That(adjusted.ContainsKey("River"), Is.True, "no drop when resolution failed");
            Assert.That(adjusted.ContainsKey("Weather"), Is.True);
        }
    }
}
