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
using System.Threading.Tasks;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Models;

using ArdysaModsTools.UI;

namespace ArdysaModsTools.UI.Interfaces
{
    public interface IHeroGalleryView
    {
        Task UpdateStatusAsync(string status);

        Task<bool> ConfirmBaseNoSetAsync(string title, string htmlMessage);

        Task ShowGenerationAlertAsync(string title, string message, bool hasFailures, string? logText = null);

        Task ShowAlertAsync(string title, string message);

        bool ShowGenerationPreview(IReadOnlyList<(HeroModel hero, string setName, string? thumbnailUrl)> items);

        void ShowWarning(string message, string title);

        void ShowErrorDialog(string title, string subtitle, string details);

        Task<OperationResult> RunGenerationWithProgressAsync(
            string initialStatus,
            Func<ProgressOperationRunnerContext, Task<OperationResult>> operation);

        Task SaveSelectionsAsync();

        void StoreResult(ModGenerationResult result);

        void CloseWithSuccess();
    }
}
