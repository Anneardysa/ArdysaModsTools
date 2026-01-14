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
namespace ArdysaModsTools.UI.Forms
{
    /// <summary>
    /// Install method selection options.
    /// </summary>
    public enum InstallMethod
    {
        None,
        AutoInstall,
        ManualInstall
    }

    /// <summary>
    /// Dialog to select install method (Auto or Manual).
    /// </summary>
    public partial class InstallMethodDialog : Form
    {
        public InstallMethod SelectedMethod { get; private set; } = InstallMethod.None;

        public InstallMethodDialog()
        {
            InitializeComponent();
            
            // Apply font fallback if JetBrains Mono not installed
            FontHelper.ApplyToForm(this);
        }

        private void BtnAutoInstall_Click(object? sender, EventArgs e)
        {
            SelectedMethod = InstallMethod.AutoInstall;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void BtnManualInstall_Click(object? sender, EventArgs e)
        {
            SelectedMethod = InstallMethod.ManualInstall;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}

