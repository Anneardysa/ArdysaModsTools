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
using ArdysaModsTools.UI;
using ArdysaModsTools.UI.Controls;

namespace ArdysaModsTools
{
    partial class MiscForm
    {
        private System.ComponentModel.IContainer components = null;

        // Top bar controls
        private System.Windows.Forms.Panel TopBar;
        private System.Windows.Forms.Panel ActionBar;
        private System.Windows.Forms.Label TitleLabel;
        private System.Windows.Forms.Label StatusLabel;
        private System.Windows.Forms.FlowLayoutPanel ActionButtonsFlow;
        private RoundedButton generateButton;
        private RoundedButton LoadPreset;
        private RoundedButton SavePreset;

        // Separator
        private System.Windows.Forms.Panel Separator;

        // Main scroll area
        private System.Windows.Forms.Panel ScrollContainer;
        private System.Windows.Forms.FlowLayoutPanel RowsFlow;

        private System.Windows.Forms.Panel ConsolePanel;
        private RoundedPanel ConsoleBoxWrapper;
        private RetroTerminal ConsoleLogBox;
        private System.Windows.Forms.Label ConsoleLabel;
        private System.Windows.Forms.Label CopyConsoleBtn;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            TopBar = new Panel();
            ActionBar = new Panel();
            TitleLabel = new Label();
            StatusLabel = new Label();
            ActionButtonsFlow = new FlowLayoutPanel();
            generateButton = new RoundedButton();
            LoadPreset = new RoundedButton();
            SavePreset = new RoundedButton();
            Separator = new Panel();
            ScrollContainer = new Panel();
            RowsFlow = new FlowLayoutPanel();
            ConsolePanel = new Panel();
            ConsoleBoxWrapper = new RoundedPanel();
            ConsoleLogBox = new RetroTerminal();
            ConsoleLabel = new Label();
            CopyConsoleBtn = new Label();
            TopBar.SuspendLayout();
            ActionBar.SuspendLayout();
            ActionButtonsFlow.SuspendLayout();
            ScrollContainer.SuspendLayout();
            ConsolePanel.SuspendLayout();
            ConsoleBoxWrapper.SuspendLayout();
            SuspendLayout();
            // 
            // TopBar
            // 
            TopBar.BackColor = Color.Black;
            TopBar.Controls.Add(ActionBar);
            TopBar.Dock = DockStyle.Top;
            TopBar.Location = new Point(0, 0);
            TopBar.Name = "TopBar";
            TopBar.Padding = new Padding(20, 12, 20, 12);
            TopBar.Size = new Size(950, 68);
            TopBar.TabIndex = 0;
            // 
            // ActionBar
            // 
            ActionBar.Controls.Add(TitleLabel);
            ActionBar.Controls.Add(StatusLabel);
            ActionBar.Controls.Add(ActionButtonsFlow);
            ActionBar.Dock = DockStyle.Fill;
            ActionBar.Location = new Point(20, 12);
            ActionBar.Name = "ActionBar";
            ActionBar.Size = new Size(910, 44);
            ActionBar.TabIndex = 1;
            // 
            // TitleLabel
            // 
            TitleLabel.AutoSize = true;
            TitleLabel.Font = new Font("JetBrains Mono", 16F, FontStyle.Bold);
            TitleLabel.ForeColor = Color.White;
            TitleLabel.Location = new Point(4, 2);
            TitleLabel.Name = "TitleLabel";
            TitleLabel.Size = new Size(182, 29);
            TitleLabel.TabIndex = 0;
            TitleLabel.Text = "Miscellaneous";
            // 
            // StatusLabel
            // 
            StatusLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            StatusLabel.AutoSize = true;
            StatusLabel.Font = new Font("JetBrains Mono", 9F);
            StatusLabel.ForeColor = Color.FromArgb(0, 255, 255);
            StatusLabel.Location = new Point(7, 27);
            StatusLabel.Name = "StatusLabel";
            StatusLabel.Size = new Size(42, 16);
            StatusLabel.TabIndex = 3;
            StatusLabel.Text = "Ready";
            // 
            // ActionButtonsFlow
            // 
            ActionButtonsFlow.AutoSize = true;
            ActionButtonsFlow.Controls.Add(generateButton);
            ActionButtonsFlow.Controls.Add(LoadPreset);
            ActionButtonsFlow.Controls.Add(SavePreset);
            ActionButtonsFlow.Dock = DockStyle.Right;
            ActionButtonsFlow.FlowDirection = FlowDirection.RightToLeft;
            ActionButtonsFlow.Location = new Point(584, 0);
            ActionButtonsFlow.Name = "ActionButtonsFlow";
            ActionButtonsFlow.Padding = new Padding(0, 4, 0, 4);
            ActionButtonsFlow.Size = new Size(326, 44);
            ActionButtonsFlow.TabIndex = 0;
            ActionButtonsFlow.WrapContents = false;
            // 
            // generateButton
            // 
            generateButton.BackColor = Color.FromArgb(0, 255, 255);
            generateButton.BorderColor = Color.FromArgb(51, 51, 51);
            generateButton.BorderRadius = 0;
            generateButton.FlatAppearance.BorderSize = 0;
            generateButton.FlatStyle = FlatStyle.Flat;
            generateButton.Font = new Font("JetBrains Mono", 10F, FontStyle.Bold);
            generateButton.ForeColor = Color.Black;
            generateButton.HighlightColor = Color.White;
            generateButton.Highlighted = false;
            generateButton.HoverBackColor = Color.White;
            generateButton.HoverForeColor = Color.Black;
            generateButton.Location = new Point(190, 7);
            generateButton.Margin = new Padding(6, 3, 6, 3);
            generateButton.Name = "generateButton";
            generateButton.Size = new Size(130, 36);
            generateButton.TabIndex = 0;
            generateButton.Text = "Generate";
            generateButton.UseVisualStyleBackColor = false;
            // 
            // LoadPreset
            // 
            LoadPreset.BackColor = Color.FromArgb(30, 30, 30);
            LoadPreset.BorderColor = Color.FromArgb(51, 51, 51);
            LoadPreset.BorderRadius = 0;
            LoadPreset.FlatAppearance.BorderSize = 0;
            LoadPreset.FlatStyle = FlatStyle.Flat;
            LoadPreset.Font = new Font("JetBrains Mono", 10F);
            LoadPreset.ForeColor = Color.FromArgb(150, 150, 150);
            LoadPreset.HighlightColor = Color.White;
            LoadPreset.Highlighted = false;
            LoadPreset.HoverBackColor = Color.White;
            LoadPreset.HoverForeColor = Color.Black;
            LoadPreset.Location = new Point(98, 7);
            LoadPreset.Margin = new Padding(6, 3, 6, 3);
            LoadPreset.Name = "LoadPreset";
            LoadPreset.Size = new Size(80, 36);
            LoadPreset.TabIndex = 1;
            LoadPreset.Text = "Load";
            LoadPreset.UseVisualStyleBackColor = false;
            // 
            // SavePreset
            // 
            SavePreset.BackColor = Color.FromArgb(30, 30, 30);
            SavePreset.BorderColor = Color.FromArgb(51, 51, 51);
            SavePreset.BorderRadius = 0;
            SavePreset.FlatAppearance.BorderSize = 0;
            SavePreset.FlatStyle = FlatStyle.Flat;
            SavePreset.Font = new Font("JetBrains Mono", 10F);
            SavePreset.ForeColor = Color.FromArgb(150, 150, 150);
            SavePreset.HighlightColor = Color.White;
            SavePreset.Highlighted = false;
            SavePreset.HoverBackColor = Color.White;
            SavePreset.HoverForeColor = Color.Black;
            SavePreset.Location = new Point(6, 7);
            SavePreset.Margin = new Padding(6, 3, 6, 3);
            SavePreset.Name = "SavePreset";
            SavePreset.Size = new Size(80, 36);
            SavePreset.TabIndex = 2;
            SavePreset.Text = "Save";
            SavePreset.UseVisualStyleBackColor = false;
            // 
            // Separator
            // 
            Separator.BackColor = Color.FromArgb(51, 51, 51);
            Separator.Dock = DockStyle.Top;
            Separator.Location = new Point(0, 68);
            Separator.Name = "Separator";
            Separator.Size = new Size(950, 1);
            Separator.TabIndex = 1;
            // 
            // ScrollContainer
            // 
            ScrollContainer.AutoScroll = true;
            ScrollContainer.BackColor = Color.Black;
            ScrollContainer.Controls.Add(RowsFlow);
            ScrollContainer.Dock = DockStyle.Fill;
            ScrollContainer.Location = new Point(0, 69);
            ScrollContainer.Name = "ScrollContainer";
            ScrollContainer.Padding = new Padding(20, 16, 32, 16);
            ScrollContainer.Size = new Size(950, 481);
            ScrollContainer.TabIndex = 0;
            // 
            // RowsFlow
            // 
            RowsFlow.AutoSize = true;
            RowsFlow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            RowsFlow.BackColor = Color.Black;
            RowsFlow.Dock = DockStyle.Top;
            RowsFlow.FlowDirection = FlowDirection.TopDown;
            RowsFlow.Location = new Point(20, 16);
            RowsFlow.Margin = new Padding(0);
            RowsFlow.Name = "RowsFlow";
            RowsFlow.Padding = new Padding(0, 0, 0, 12);
            RowsFlow.Size = new Size(898, 12);
            RowsFlow.TabIndex = 0;
            RowsFlow.WrapContents = false;
            // 
            // ConsolePanel
            // 
            ConsolePanel.BackColor = Color.Black;
            ConsolePanel.Controls.Add(ConsoleBoxWrapper);
            ConsolePanel.Controls.Add(ConsoleLabel);
            ConsolePanel.Controls.Add(CopyConsoleBtn);
            ConsolePanel.Dock = DockStyle.Bottom;
            ConsolePanel.Location = new Point(0, 550);
            ConsolePanel.Name = "ConsolePanel";
            ConsolePanel.Padding = new Padding(20, 8, 20, 8);
            ConsolePanel.Size = new Size(950, 150);
            ConsolePanel.TabIndex = 2;
            // 
            // ConsoleBoxWrapper
            // 
            ConsoleBoxWrapper.BackColor = Color.Black;
            ConsoleBoxWrapper.BorderColor = Color.FromArgb(51, 51, 51);
            ConsoleBoxWrapper.BorderRadius = 0;
            ConsoleBoxWrapper.BorderThickness = 1;
            ConsoleBoxWrapper.Controls.Add(ConsoleLogBox);
            ConsoleBoxWrapper.Dock = DockStyle.Fill;
            ConsoleBoxWrapper.Location = new Point(20, 32);
            ConsoleBoxWrapper.Name = "ConsoleBoxWrapper";
            ConsoleBoxWrapper.Padding = new Padding(10, 8, 10, 8);
            ConsoleBoxWrapper.Size = new Size(910, 110);
            ConsoleBoxWrapper.TabIndex = 0;
            // 
            // ConsoleLogBox
            // 
            ConsoleLogBox.BackColor = Color.Black;
            ConsoleLogBox.Dock = DockStyle.Fill;
            ConsoleLogBox.EnableGlow = true;
            ConsoleLogBox.EnableScanlines = true;
            ConsoleLogBox.Font = new Font("JetBrains Mono", 8F, FontStyle.Bold);
            ConsoleLogBox.GlowRadius = 1;
            ConsoleLogBox.Location = new Point(10, 8);
            ConsoleLogBox.MaxLines = 500;
            ConsoleLogBox.Name = "ConsoleLogBox";
            ConsoleLogBox.ScanlineAlpha = 15;
            ConsoleLogBox.ScanlineSpacing = 3;
            ConsoleLogBox.Size = new Size(890, 94);
            ConsoleLogBox.TabIndex = 0;
            // 
            // ConsoleLabel
            // 
            ConsoleLabel.AutoSize = true;
            ConsoleLabel.Dock = DockStyle.Top;
            ConsoleLabel.Font = new Font("JetBrains Mono", 9F);
            ConsoleLabel.ForeColor = Color.FromArgb(68, 68, 68);
            ConsoleLabel.Location = new Point(20, 8);
            ConsoleLabel.Name = "ConsoleLabel";
            ConsoleLabel.Padding = new Padding(0, 4, 0, 4);
            ConsoleLabel.Size = new Size(84, 24);
            ConsoleLabel.TabIndex = 1;
            ConsoleLabel.Text = ". / CONSOLE";
            // 
            // CopyConsoleBtn
            // 
            CopyConsoleBtn.AutoSize = true;
            CopyConsoleBtn.Cursor = Cursors.Hand;
            CopyConsoleBtn.Font = new Font("JetBrains Mono", 9F);
            CopyConsoleBtn.ForeColor = Color.FromArgb(100, 100, 100);
            CopyConsoleBtn.Location = new Point(872, 11);
            CopyConsoleBtn.Name = "CopyConsoleBtn";
            CopyConsoleBtn.Size = new Size(63, 16);
            CopyConsoleBtn.TabIndex = 3;
            CopyConsoleBtn.Text = "[ COPY ]";
            CopyConsoleBtn.Click += CopyConsoleBtn_Click;
            // 
            // MiscForm
            // 
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.Black;
            ClientSize = new Size(950, 700);
            Controls.Add(ScrollContainer);
            Controls.Add(ConsolePanel);
            Controls.Add(Separator);
            Controls.Add(TopBar);
            Font = new Font("JetBrains Mono", 9F);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            MinimumSize = new Size(800, 600);
            Name = "MiscForm";
            ShowIcon = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "ArdysaModsTools - Miscellaneous";
            Load += MiscForm_Load;
            TopBar.ResumeLayout(false);
            ActionBar.ResumeLayout(false);
            ActionBar.PerformLayout();
            ActionButtonsFlow.ResumeLayout(false);
            ScrollContainer.ResumeLayout(false);
            ScrollContainer.PerformLayout();
            ConsolePanel.ResumeLayout(false);
            ConsolePanel.PerformLayout();
            ConsoleBoxWrapper.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion
    }
}
