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

namespace ArdysaModsTools.Core.Models
{
    /// <summary>
    /// Represents a single step in the newcomer onboarding guide.
    /// Each step highlights a specific UI control with a title and description.
    /// </summary>
    public class OnboardingStep
    {
        /// <summary>
        /// Display title for this step (e.g. "Auto Detect").
        /// </summary>
        public string Title { get; set; } = "";

        /// <summary>
        /// Description explaining what this feature does.
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// The Name property of the WinForms control to highlight.
        /// </summary>
        public string ControlName { get; set; } = "";

        /// <summary>
        /// Extra padding around the spotlight cutout (in pixels).
        /// </summary>
        public int SpotlightPadding { get; set; } = 6;
    }
}
