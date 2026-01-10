using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Models;

namespace ArdysaModsTools.UI.Interfaces
{
    /// <summary>
    /// Contract for SelectHero form UI operations.
    /// Allows the presenter to update UI without direct form references.
    /// </summary>
    public interface ISelectHeroView
    {
        #region Hero Display

        /// <summary>
        /// Populates the hero list with the provided heroes.
        /// </summary>
        /// <param name="heroes">List of hero models to display</param>
        void PopulateHeroes(IEnumerable<HeroModel> heroes);

        /// <summary>
        /// Adds a hero row to the display.
        /// </summary>
        /// <param name="hero">Hero to add</param>
        /// <param name="isFavorite">Whether this hero is a favorite</param>
        void AddHeroRow(HeroModel hero, bool isFavorite);

        /// <summary>
        /// Clears all hero rows from the display.
        /// </summary>
        void ClearHeroRows();

        /// <summary>
        /// Filters visible heroes by the specified category.
        /// </summary>
        /// <param name="category">Category to filter by (all, favorite, strength, agility, intelligence, universal)</param>
        void ApplyCategoryFilter(string category);

        /// <summary>
        /// Filters visible heroes by the search text.
        /// </summary>
        /// <param name="searchText">Text to search for</param>
        void ApplySearchFilter(string searchText);

        /// <summary>
        /// Scrolls to a specific hero row.
        /// </summary>
        /// <param name="heroId">ID of the hero to scroll to</param>
        void ScrollToHero(string heroId);

        #endregion

        #region Selections

        /// <summary>
        /// Gets the current hero set selections.
        /// </summary>
        /// <returns>List of (heroId, setName) tuples</returns>
        IEnumerable<(string heroId, string setName)> GetSelections();

        /// <summary>
        /// Sets the selection for a specific hero.
        /// </summary>
        /// <param name="heroId">Hero ID</param>
        /// <param name="setName">Set name to select</param>
        void SetSelection(string heroId, string setName);

        /// <summary>
        /// Clears all selections.
        /// </summary>
        void ClearSelections();

        /// <summary>
        /// Restores selections from the provided dictionary.
        /// </summary>
        /// <param name="selections">Dictionary of heroId -> setName</param>
        void RestoreSelections(Dictionary<string, string> selections);

        #endregion

        #region Favorites

        /// <summary>
        /// Toggles the favorite status of a hero.
        /// </summary>
        /// <param name="heroId">Hero ID to toggle</param>
        /// <returns>New favorite status</returns>
        bool ToggleFavorite(string heroId);

        /// <summary>
        /// Gets all favorited hero IDs.
        /// </summary>
        /// <returns>Set of favorite hero IDs</returns>
        HashSet<string> GetFavorites();

        #endregion

        #region Status Updates

        /// <summary>
        /// Sets the status bar text.
        /// </summary>
        /// <param name="status">Status text to display</param>
        void SetStatus(string status);

        /// <summary>
        /// Appends a debug message (only shown in debug mode).
        /// </summary>
        /// <param name="message">Debug message</param>
        void AppendDebug(string message);

        #endregion

        #region Progress Overlay

        /// <summary>
        /// Shows the progress overlay.
        /// </summary>
        /// <returns>Task that completes when overlay is ready</returns>
        Task ShowProgressOverlayAsync();

        /// <summary>
        /// Hides the progress overlay.
        /// </summary>
        void HideProgressOverlay();

        /// <summary>
        /// Updates progress overlay status.
        /// </summary>
        /// <param name="percent">Progress percentage (0-100)</param>
        /// <param name="status">Status text</param>
        /// <param name="substatus">Optional sub-status text</param>
        Task UpdateProgressAsync(int percent, string status, string? substatus = null);

        #endregion

        #region UI State

        /// <summary>
        /// Enables or disables all UI controls.
        /// </summary>
        /// <param name="enabled">Whether controls should be enabled</param>
        void SetControlsEnabled(bool enabled);

        /// <summary>
        /// Shows a message box.
        /// </summary>
        DialogResult ShowMessageBox(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon);

        /// <summary>
        /// Shows a file open dialog.
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="filter">File filter</param>
        /// <returns>Selected file path or null</returns>
        string? ShowOpenFileDialog(string title, string filter);

        /// <summary>
        /// Shows a file save dialog.
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="filter">File filter</param>
        /// <returns>Selected file path or null</returns>
        string? ShowSaveFileDialog(string title, string filter);

        /// <summary>
        /// Invokes an action on the UI thread.
        /// </summary>
        /// <param name="action">Action to invoke</param>
        void InvokeOnUIThread(Action action);

        /// <summary>
        /// Closes the form with the specified result.
        /// </summary>
        /// <param name="result">Dialog result</param>
        void CloseWithResult(DialogResult result);

        #endregion
    }
}
