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
using System.Collections.Generic;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;

namespace ArdysaModsTools.Core.Services.App
{
    /// <summary>
    /// Manages the newcomer onboarding guide state and step definitions.
    /// Persists completion status via IConfigService.
    /// </summary>
    public class OnboardingService
    {
        private const string ConfigKey = "OnboardingCompleted";

        private readonly IConfigService _configService;

        public OnboardingService(IConfigService configService)
        {
            _configService = configService;
        }

        /// <summary>
        /// Returns true if the user has already completed or dismissed the onboarding guide.
        /// </summary>
        public bool IsOnboardingCompleted()
        {
            return _configService.GetValue(ConfigKey, false);
        }

        /// <summary>
        /// Marks the onboarding guide as completed so it won't show again.
        /// </summary>
        public void MarkOnboardingCompleted()
        {
            _configService.SetValue(ConfigKey, true);
            _configService.Save();
        }

        /// <summary>
        /// Resets the onboarding state so the guide will show again on next trigger.
        /// </summary>
        public void ResetOnboarding()
        {
            _configService.SetValue(ConfigKey, false);
            _configService.Save();
        }

        /// <summary>
        /// Returns the ordered list of onboarding steps to display.
        /// Each step references a WinForms control by its Name property.
        /// </summary>
        public List<OnboardingStep> GetSteps()
        {
            return new List<OnboardingStep>
            {
                new OnboardingStep
                {
                    Title = "Auto Detect",
                    Description = "Automatically finds your Dota 2 installation path.\nClick this first to get started!",
                    ControlName = "autoDetectButton",
                    SpotlightPadding = 8
                },
                new OnboardingStep
                {
                    Title = "Manual Detect",
                    Description = "Can't auto-detect? Use this to manually browse\nto your Dota 2 game folder.",
                    ControlName = "manualDetectButton",
                    SpotlightPadding = 8
                },
                new OnboardingStep
                {
                    Title = "Skin Selector",
                    Description = "Browse hero cosmetic sets and select the skins\nyou want to apply to your heroes.",
                    ControlName = "btn_OpenSelectHero",
                    SpotlightPadding = 8
                },
                new OnboardingStep
                {
                    Title = "Miscellaneous",
                    Description = "Change weather effects, HUD skins, terrain,\ncouriers, wards, and other visual mods.",
                    ControlName = "miscellaneousButton",
                    SpotlightPadding = 8
                },
                new OnboardingStep
                {
                    Title = "Install ModsPack",
                    Description = "Apply all your selected mods to Dota 2.\nThis bundles everything into the game files.",
                    ControlName = "installButton",
                    SpotlightPadding = 8
                },
                new OnboardingStep
                {
                    Title = "Patch Update",
                    Description = "After a Dota 2 game update, click here to\nre-apply your mod patches.",
                    ControlName = "updatePatcherButton",
                    SpotlightPadding = 8
                },
                new OnboardingStep
                {
                    Title = "Console Log",
                    Description = "View real-time operation logs and status\nmessages here. Useful for troubleshooting.",
                    ControlName = "consolePanel",
                    SpotlightPadding = 4
                },
                new OnboardingStep
                {
                    Title = "Settings",
                    Description = "Configure app behavior, manage cache,\ncheck for updates, and more.",
                    ControlName = "btnSettings",
                    SpotlightPadding = 12
                }
            };
        }
    }
}
