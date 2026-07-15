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
using ArdysaModsTools.Core.Services.Localization;

namespace ArdysaModsTools.Core.Services.App
{
    public class OnboardingService
    {
        private const string ConfigKey = "OnboardingCompleted";

        private readonly IConfigService _configService;

        public OnboardingService(IConfigService configService)
        {
            _configService = configService;
        }

        public bool IsOnboardingCompleted()
        {
            return _configService.GetValue(ConfigKey, false);
        }

        public void MarkOnboardingCompleted()
        {
            _configService.SetValue(ConfigKey, true);
            _configService.Save();
        }

        public void ResetOnboarding()
        {
            _configService.SetValue(ConfigKey, false);
            _configService.Save();
        }

        public List<OnboardingStep> GetSteps()
        {
            return new List<OnboardingStep>
            {
                new OnboardingStep
                {
                    Title = Loc.T("onboarding.autoDetect.title"),
                    Description = Loc.T("onboarding.autoDetect.desc"),
                    ControlName = "autoDetectButton",
                    SpotlightPadding = 8
                },
                new OnboardingStep
                {
                    Title = Loc.T("onboarding.manualDetect.title"),
                    Description = Loc.T("onboarding.manualDetect.desc"),
                    ControlName = "manualDetectButton",
                    SpotlightPadding = 8
                },
                new OnboardingStep
                {
                    Title = Loc.T("onboarding.skinSelector.title"),
                    Description = Loc.T("onboarding.skinSelector.desc"),
                    ControlName = "btn_OpenSelectHero",
                    SpotlightPadding = 8
                },
                new OnboardingStep
                {
                    Title = Loc.T("onboarding.misc.title"),
                    Description = Loc.T("onboarding.misc.desc"),
                    ControlName = "miscellaneousButton",
                    SpotlightPadding = 8
                },
                new OnboardingStep
                {
                    Title = Loc.T("onboarding.install.title"),
                    Description = Loc.T("onboarding.install.desc"),
                    ControlName = "installButton",
                    SpotlightPadding = 8
                },
                new OnboardingStep
                {
                    Title = Loc.T("onboarding.patch.title"),
                    Description = Loc.T("onboarding.patch.desc"),
                    ControlName = "updatePatcherButton",
                    SpotlightPadding = 8
                },
                new OnboardingStep
                {
                    Title = Loc.T("onboarding.console.title"),
                    Description = Loc.T("onboarding.console.desc"),
                    ControlName = "consolePanel",
                    SpotlightPadding = 4
                },
                new OnboardingStep
                {
                    Title = Loc.T("onboarding.settings.title"),
                    Description = Loc.T("onboarding.settings.desc"),
                    ControlName = "btnSettings",
                    SpotlightPadding = 12
                }
            };
        }
    }
}
