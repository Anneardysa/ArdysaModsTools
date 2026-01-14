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
using ArdysaModsTools.UI.Controls;

namespace ArdysaModsTools.UI.Forms
{
    partial class SelectHero
    {
        private System.ComponentModel.IContainer components = null;

        // Top controls (similar to MiscForm)
        private System.Windows.Forms.Panel TopBar;
        private System.Windows.Forms.Panel ActionBar;
        private System.Windows.Forms.Label TitleLabel;
        private System.Windows.Forms.FlowLayoutPanel ActionButtonsFlow;
        private RoundedButton btn_SelectGenerate;
        private RoundedButton btn_ClearSelections;
        private RoundedButton btn_SelectLoad;
        private RoundedButton btn_SelectSave;
        private System.Windows.Forms.Label lbl_Status;

        // Categories row with search
        private System.Windows.Forms.Panel CategoryBar;
        private System.Windows.Forms.FlowLayoutPanel CategoryFlow;
        private System.Windows.Forms.FlowLayoutPanel SearchFlow;
        private System.Windows.Forms.Label lbl_All;
        private System.Windows.Forms.Label lbl_Favorite;
        private System.Windows.Forms.Label lbl_Strength;
        private System.Windows.Forms.Label lbl_Agility;
        private System.Windows.Forms.Label lbl_Intelligence;
        private System.Windows.Forms.Label lbl_Universal;
        private ArdysaModsTools.UI.Controls.ModernSearchBox modernSearchBox;

        // Separator
        private System.Windows.Forms.Panel Separator;

        // Main scroll area
        private System.Windows.Forms.Panel ScrollContainer;
        // FlowLayoutPanel inside ScrollContainer for automatic row layout
        private System.Windows.Forms.FlowLayoutPanel RowsFlow;
        
        // Dimming overlay for progress
        private System.Windows.Forms.Panel DimmingOverlay;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            TopBar = new Panel();
            ActionBar = new Panel();
            lbl_Status = new Label();
            TitleLabel = new Label();
            ActionButtonsFlow = new FlowLayoutPanel();
            btn_SelectGenerate = new RoundedButton();
            btn_ClearSelections = new RoundedButton();
            btn_SelectLoad = new RoundedButton();
            btn_SelectSave = new RoundedButton();
            CategoryBar = new Panel();
            SearchFlow = new FlowLayoutPanel();
            modernSearchBox = new ModernSearchBox();
            CategoryFlow = new FlowLayoutPanel();
            lbl_All = new Label();
            lbl_Favorite = new Label();
            lbl_Strength = new Label();
            lbl_Agility = new Label();
            lbl_Intelligence = new Label();
            lbl_Universal = new Label();
            Separator = new Panel();
            ScrollContainer = new Panel();
            RowsFlow = new FlowLayoutPanel();
            DimmingOverlay = new Panel();
            TopBar.SuspendLayout();
            ActionBar.SuspendLayout();
            ActionButtonsFlow.SuspendLayout();
            CategoryBar.SuspendLayout();
            SearchFlow.SuspendLayout();
            CategoryFlow.SuspendLayout();
            ScrollContainer.SuspendLayout();
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
            TopBar.Size = new Size(1192, 68);
            TopBar.TabIndex = 3;
            // 
            // ActionBar
            // 
            ActionBar.Controls.Add(lbl_Status);
            ActionBar.Controls.Add(TitleLabel);
            ActionBar.Controls.Add(ActionButtonsFlow);
            ActionBar.Dock = DockStyle.Fill;
            ActionBar.Location = new Point(20, 12);
            ActionBar.Name = "ActionBar";
            ActionBar.Size = new Size(1152, 44);
            ActionBar.TabIndex = 1;
            // 
            // lbl_Status
            // 
            lbl_Status.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lbl_Status.Font = new Font("JetBrains Mono", 9F);
            lbl_Status.ForeColor = Color.FromArgb(0, 255, 255);
            lbl_Status.Location = new Point(7, 27);
            lbl_Status.Name = "lbl_Status";
            lbl_Status.Size = new Size(300, 16);
            lbl_Status.TabIndex = 0;
            lbl_Status.Text = "Ready";
            // 
            // TitleLabel
            // 
            TitleLabel.AutoSize = true;
            TitleLabel.Font = new Font("JetBrains Mono", 16F, FontStyle.Bold);
            TitleLabel.ForeColor = Color.White;
            TitleLabel.Location = new Point(4, 2);
            TitleLabel.Name = "TitleLabel";
            TitleLabel.Size = new Size(156, 29);
            TitleLabel.TabIndex = 0;
            TitleLabel.Text = "Select Skin";
            // 
            // ActionButtonsFlow
            // 
            ActionButtonsFlow.AutoSize = true;
            ActionButtonsFlow.Controls.Add(btn_SelectGenerate);
            ActionButtonsFlow.Controls.Add(btn_ClearSelections);
            ActionButtonsFlow.Controls.Add(btn_SelectLoad);
            ActionButtonsFlow.Controls.Add(btn_SelectSave);
            ActionButtonsFlow.Dock = DockStyle.Right;
            ActionButtonsFlow.FlowDirection = FlowDirection.RightToLeft;
            ActionButtonsFlow.Location = new Point(826, 0);
            ActionButtonsFlow.Name = "ActionButtonsFlow";
            ActionButtonsFlow.Padding = new Padding(0, 4, 0, 4);
            ActionButtonsFlow.Size = new Size(326, 44);
            ActionButtonsFlow.TabIndex = 0;
            ActionButtonsFlow.WrapContents = false;
            // 
            // btn_SelectGenerate
            // 
            btn_SelectGenerate.BackColor = Color.FromArgb(0, 255, 255);
            btn_SelectGenerate.BorderColor = Color.FromArgb(51, 51, 51);
            btn_SelectGenerate.BorderRadius = 0;
            btn_SelectGenerate.FlatAppearance.BorderSize = 0;
            btn_SelectGenerate.FlatStyle = FlatStyle.Flat;
            btn_SelectGenerate.Font = new Font("JetBrains Mono", 10F, FontStyle.Bold);
            btn_SelectGenerate.ForeColor = Color.Black;
            btn_SelectGenerate.HighlightColor = Color.FromArgb(255, 255, 255);
            btn_SelectGenerate.Highlighted = false;
            btn_SelectGenerate.HoverBackColor = Color.White;
            btn_SelectGenerate.HoverForeColor = Color.Black;
            btn_SelectGenerate.Location = new Point(190, 7);
            btn_SelectGenerate.Margin = new Padding(6, 3, 6, 3);
            btn_SelectGenerate.Name = "btn_SelectGenerate";
            btn_SelectGenerate.Size = new Size(130, 36);
            btn_SelectGenerate.TabIndex = 0;
            btn_SelectGenerate.Text = "Generate";
            btn_SelectGenerate.UseVisualStyleBackColor = false;
            btn_SelectGenerate.Click += Btn_SelectGenerate_Click;
            // 
            // btn_ClearSelections
            // 
            btn_ClearSelections.BackColor = Color.FromArgb(60, 30, 30);
            btn_ClearSelections.BorderColor = Color.FromArgb(100, 50, 50);
            btn_ClearSelections.BorderRadius = 0;
            btn_ClearSelections.FlatAppearance.BorderSize = 0;
            btn_ClearSelections.FlatStyle = FlatStyle.Flat;
            btn_ClearSelections.Font = new Font("JetBrains Mono", 10F);
            btn_ClearSelections.ForeColor = Color.FromArgb(200, 150, 150);
            btn_ClearSelections.HighlightColor = Color.FromArgb(255, 255, 255);
            btn_ClearSelections.Highlighted = false;
            btn_ClearSelections.HoverBackColor = Color.FromArgb(100, 50, 50);
            btn_ClearSelections.HoverForeColor = Color.White;
            btn_ClearSelections.Location = new Point(180, 7);
            btn_ClearSelections.Margin = new Padding(6, 3, 6, 3);
            btn_ClearSelections.Name = "btn_ClearSelections";
            btn_ClearSelections.Size = new Size(80, 36);
            btn_ClearSelections.TabIndex = 3;
            btn_ClearSelections.Text = "Clear";
            btn_ClearSelections.UseVisualStyleBackColor = false;
            btn_ClearSelections.Click += Btn_ClearSelections_Click;
            // 
            // btn_SelectLoad
            // 
            btn_SelectLoad.BackColor = Color.FromArgb(30, 30, 30);
            btn_SelectLoad.BorderColor = Color.FromArgb(51, 51, 51);
            btn_SelectLoad.BorderRadius = 0;
            btn_SelectLoad.FlatAppearance.BorderSize = 0;
            btn_SelectLoad.FlatStyle = FlatStyle.Flat;
            btn_SelectLoad.Font = new Font("JetBrains Mono", 10F);
            btn_SelectLoad.ForeColor = Color.FromArgb(150, 150, 150);
            btn_SelectLoad.HighlightColor = Color.FromArgb(255, 255, 255);
            btn_SelectLoad.Highlighted = false;
            btn_SelectLoad.HoverBackColor = Color.White;
            btn_SelectLoad.HoverForeColor = Color.Black;
            btn_SelectLoad.Location = new Point(98, 7);
            btn_SelectLoad.Margin = new Padding(6, 3, 6, 3);
            btn_SelectLoad.Name = "btn_SelectLoad";
            btn_SelectLoad.Size = new Size(80, 36);
            btn_SelectLoad.TabIndex = 1;
            btn_SelectLoad.Text = "Load";
            btn_SelectLoad.UseVisualStyleBackColor = false;
            // 
            // btn_SelectSave
            // 
            btn_SelectSave.BackColor = Color.FromArgb(30, 30, 30);
            btn_SelectSave.BorderColor = Color.FromArgb(51, 51, 51);
            btn_SelectSave.BorderRadius = 0;
            btn_SelectSave.FlatAppearance.BorderSize = 0;
            btn_SelectSave.FlatStyle = FlatStyle.Flat;
            btn_SelectSave.Font = new Font("JetBrains Mono", 10F);
            btn_SelectSave.ForeColor = Color.FromArgb(150, 150, 150);
            btn_SelectSave.HighlightColor = Color.FromArgb(255, 255, 255);
            btn_SelectSave.Highlighted = false;
            btn_SelectSave.HoverBackColor = Color.White;
            btn_SelectSave.HoverForeColor = Color.Black;
            btn_SelectSave.Location = new Point(6, 7);
            btn_SelectSave.Margin = new Padding(6, 3, 6, 3);
            btn_SelectSave.Name = "btn_SelectSave";
            btn_SelectSave.Size = new Size(80, 36);
            btn_SelectSave.TabIndex = 2;
            btn_SelectSave.Text = "Save";
            btn_SelectSave.UseVisualStyleBackColor = false;
            // 
            // CategoryBar
            // 
            CategoryBar.BackColor = Color.Black;
            CategoryBar.Controls.Add(SearchFlow);
            CategoryBar.Controls.Add(CategoryFlow);
            CategoryBar.Dock = DockStyle.Top;
            CategoryBar.Location = new Point(0, 68);
            CategoryBar.Name = "CategoryBar";
            CategoryBar.Size = new Size(1192, 56);
            CategoryBar.TabIndex = 2;
            // 
            // SearchFlow
            // 
            SearchFlow.Controls.Add(modernSearchBox);
            SearchFlow.Dock = DockStyle.Right;
            SearchFlow.FlowDirection = FlowDirection.RightToLeft;
            SearchFlow.Location = new Point(892, 0);
            SearchFlow.Name = "SearchFlow";
            SearchFlow.Padding = new Padding(0, 8, 20, 8);
            SearchFlow.Size = new Size(300, 56);
            SearchFlow.TabIndex = 1;
            SearchFlow.WrapContents = false;
            // 
            // modernSearchBox
            // 
            modernSearchBox.BackColor = Color.Transparent;
            modernSearchBox.DebounceMs = 300;
            modernSearchBox.Location = new Point(20, 8);
            modernSearchBox.Margin = new Padding(0);
            modernSearchBox.Name = "modernSearchBox";
            modernSearchBox.Padding = new Padding(12, 9, 12, 8);
            modernSearchBox.PlaceholderText = "Search heroes...";
            modernSearchBox.SearchText = "";
            modernSearchBox.Size = new Size(260, 40);
            modernSearchBox.TabIndex = 0;
            modernSearchBox.Load += modernSearchBox_Load_1;
            // 
            // CategoryFlow
            // 
            CategoryFlow.AutoSize = true;
            CategoryFlow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            CategoryFlow.BackColor = Color.Transparent;
            CategoryFlow.Controls.Add(lbl_All);
            CategoryFlow.Controls.Add(lbl_Favorite);
            CategoryFlow.Controls.Add(lbl_Strength);
            CategoryFlow.Controls.Add(lbl_Agility);
            CategoryFlow.Controls.Add(lbl_Intelligence);
            CategoryFlow.Controls.Add(lbl_Universal);
            CategoryFlow.Dock = DockStyle.Left;
            CategoryFlow.Location = new Point(0, 0);
            CategoryFlow.Name = "CategoryFlow";
            CategoryFlow.Padding = new Padding(20, 18, 0, 18);
            CategoryFlow.Size = new Size(892, 56);
            CategoryFlow.TabIndex = 0;
            CategoryFlow.WrapContents = false;
            // 
            // lbl_All
            // 
            lbl_All.AutoSize = true;
            lbl_All.Cursor = Cursors.Hand;
            lbl_All.Font = new Font("JetBrains Mono", 10F);
            lbl_All.ForeColor = Color.FromArgb(150, 150, 150);
            lbl_All.Location = new Point(40, 21);
            lbl_All.Margin = new Padding(20, 3, 20, 3);
            lbl_All.Name = "lbl_All";
            lbl_All.Size = new Size(64, 18);
            lbl_All.TabIndex = 0;
            lbl_All.Text = "[ All ]";
            // 
            // lbl_Favorite
            // 
            lbl_Favorite.AutoSize = true;
            lbl_Favorite.Cursor = Cursors.Hand;
            lbl_Favorite.Font = new Font("JetBrains Mono", 10F);
            lbl_Favorite.ForeColor = Color.FromArgb(150, 150, 150);
            lbl_Favorite.Location = new Point(144, 21);
            lbl_Favorite.Margin = new Padding(20, 3, 20, 3);
            lbl_Favorite.Name = "lbl_Favorite";
            lbl_Favorite.Size = new Size(120, 18);
            lbl_Favorite.TabIndex = 1;
            lbl_Favorite.Text = "[ ★ Favorite ]";
            // 
            // lbl_Strength
            // 
            lbl_Strength.AutoSize = true;
            lbl_Strength.Cursor = Cursors.Hand;
            lbl_Strength.Font = new Font("JetBrains Mono", 10F);
            lbl_Strength.ForeColor = Color.FromArgb(150, 150, 150);
            lbl_Strength.Location = new Point(304, 21);
            lbl_Strength.Margin = new Padding(20, 3, 20, 3);
            lbl_Strength.Name = "lbl_Strength";
            lbl_Strength.Size = new Size(104, 18);
            lbl_Strength.TabIndex = 2;
            lbl_Strength.Text = "[ Strength ]";
            // 
            // lbl_Agility
            // 
            lbl_Agility.AutoSize = true;
            lbl_Agility.Cursor = Cursors.Hand;
            lbl_Agility.Font = new Font("JetBrains Mono", 10F);
            lbl_Agility.ForeColor = Color.FromArgb(150, 150, 150);
            lbl_Agility.Location = new Point(448, 21);
            lbl_Agility.Margin = new Padding(20, 3, 20, 3);
            lbl_Agility.Name = "lbl_Agility";
            lbl_Agility.Size = new Size(96, 18);
            lbl_Agility.TabIndex = 3;
            lbl_Agility.Text = "[ Agility ]";
            // 
            // lbl_Intelligence
            // 
            lbl_Intelligence.AutoSize = true;
            lbl_Intelligence.Cursor = Cursors.Hand;
            lbl_Intelligence.Font = new Font("JetBrains Mono", 10F);
            lbl_Intelligence.ForeColor = Color.FromArgb(150, 150, 150);
            lbl_Intelligence.Location = new Point(584, 21);
            lbl_Intelligence.Margin = new Padding(20, 3, 20, 3);
            lbl_Intelligence.Name = "lbl_Intelligence";
            lbl_Intelligence.Size = new Size(136, 18);
            lbl_Intelligence.TabIndex = 4;
            lbl_Intelligence.Text = "[ Intelligence ]";
            // 
            // lbl_Universal
            // 
            lbl_Universal.AutoSize = true;
            lbl_Universal.Cursor = Cursors.Hand;
            lbl_Universal.Font = new Font("JetBrains Mono", 10F);
            lbl_Universal.ForeColor = Color.FromArgb(150, 150, 150);
            lbl_Universal.Location = new Point(760, 21);
            lbl_Universal.Margin = new Padding(20, 3, 20, 3);
            lbl_Universal.Name = "lbl_Universal";
            lbl_Universal.Size = new Size(112, 18);
            lbl_Universal.TabIndex = 5;
            lbl_Universal.Text = "[ Universal ]";
            // 
            // Separator
            // 
            Separator.BackColor = Color.FromArgb(51, 51, 51);
            Separator.Dock = DockStyle.Top;
            Separator.Location = new Point(0, 124);
            Separator.Name = "Separator";
            Separator.Size = new Size(1192, 1);
            Separator.TabIndex = 1;
            // 
            // ScrollContainer
            // 
            ScrollContainer.AutoScroll = true;
            ScrollContainer.BackColor = Color.Black;
            ScrollContainer.Controls.Add(RowsFlow);
            ScrollContainer.Dock = DockStyle.Fill;
            ScrollContainer.Location = new Point(0, 125);
            ScrollContainer.Name = "ScrollContainer";
            ScrollContainer.Padding = new Padding(20, 16, 32, 16);
            ScrollContainer.Size = new Size(1192, 598);
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
            RowsFlow.Size = new Size(1140, 12);
            RowsFlow.TabIndex = 0;
            RowsFlow.WrapContents = false;
            // 
            // DimmingOverlay
            // 
            DimmingOverlay.BackColor = Color.FromArgb(200, 0, 0, 0);
            DimmingOverlay.Dock = DockStyle.Fill;
            DimmingOverlay.Location = new Point(0, 125);
            DimmingOverlay.Name = "DimmingOverlay";
            DimmingOverlay.Size = new Size(1192, 598);
            DimmingOverlay.TabIndex = 10;
            DimmingOverlay.Visible = false;
            // 
            // SelectHero
            // 
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.Black;
            ClientSize = new Size(1192, 723);
            Controls.Add(DimmingOverlay);
            Controls.Add(ScrollContainer);
            Controls.Add(Separator);
            Controls.Add(CategoryBar);
            Controls.Add(TopBar);
            Font = new Font("JetBrains Mono", 9F);
            MinimumSize = new Size(700, 500);
            Name = "SelectHero";
            ShowIcon = false;
            Text = "ArydsaModsTools - Select Hero";
            TopBar.ResumeLayout(false);
            ActionBar.ResumeLayout(false);
            ActionBar.PerformLayout();
            ActionButtonsFlow.ResumeLayout(false);
            CategoryBar.ResumeLayout(false);
            CategoryBar.PerformLayout();
            SearchFlow.ResumeLayout(false);
            CategoryFlow.ResumeLayout(false);
            CategoryFlow.PerformLayout();
            ScrollContainer.ResumeLayout(false);
            ScrollContainer.PerformLayout();
            ResumeLayout(false);
        }

        #endregion
    }
}

