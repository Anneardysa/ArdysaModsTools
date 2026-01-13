using ArdysaModsTools.UI;
using ArdysaModsTools.UI.Controls;

namespace ArdysaModsTools
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            sidebarPanel = new Panel();
            pictureBox1 = new PictureBox();
            lblDetectSection = new Label();
            paypalPictureBox = new PictureBox();
            autoDetectButton = new RoundedButton();
            manualDetectButton = new RoundedButton();
            lblModsSection = new Label();
            btn_OpenSelectHero = new RoundedButton();
            lblToolsSection = new Label();
            updatePatcherButton = new RoundedButton();
            miscellaneousButton = new RoundedButton();
            dividerLabel = new Label();
            statusModsDotLabel = new Label();
            statusModsTextLabel = new Label();
            statusRefreshButton = new Label();
            discordPictureBox = new PictureBox();
            youtubePictureBox = new PictureBox();
            versionLabel = new Label();
            installButton = new RoundedButton();
            disableButton = new RoundedButton();
            consolePanel = new RoundedPanel();
            mainConsoleBox = new RetroTerminal();
            copyConsoleBtn = new Label();
            label1 = new Label();
            label3 = new Label();
            label4 = new Label();
            lblDotaWarning = new Label();
            imageContainer = new RoundedPanel();
            imagePictureBox = new PictureBox();
            headerPanel = new Panel();
            btnMinimize = new Label();
            btnClose = new Label();
            components = new System.ComponentModel.Container();
            trayIcon = new NotifyIcon(components);
            sidebarPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)paypalPictureBox).BeginInit();
            ((System.ComponentModel.ISupportInitialize)discordPictureBox).BeginInit();
            ((System.ComponentModel.ISupportInitialize)youtubePictureBox).BeginInit();
            consolePanel.SuspendLayout();
            imageContainer.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)imagePictureBox).BeginInit();
            headerPanel.SuspendLayout();
            SuspendLayout();
            // 
            // trayIcon
            // 
            trayIcon.Text = "ArdysaModsTools";
            trayIcon.Visible = false;
            // 
            // sidebarPanel
            // 
            sidebarPanel.BackColor = Color.FromArgb(5, 5, 5);
            sidebarPanel.Controls.Add(pictureBox1);
            sidebarPanel.Controls.Add(lblDetectSection);
            sidebarPanel.Controls.Add(paypalPictureBox);
            sidebarPanel.Controls.Add(autoDetectButton);
            sidebarPanel.Controls.Add(manualDetectButton);
            sidebarPanel.Controls.Add(lblModsSection);
            sidebarPanel.Controls.Add(btn_OpenSelectHero);
            sidebarPanel.Controls.Add(lblToolsSection);
            sidebarPanel.Controls.Add(updatePatcherButton);
            sidebarPanel.Controls.Add(miscellaneousButton);
            sidebarPanel.Controls.Add(dividerLabel);
            sidebarPanel.Controls.Add(statusModsDotLabel);
            sidebarPanel.Controls.Add(statusModsTextLabel);
            sidebarPanel.Controls.Add(statusRefreshButton);
            sidebarPanel.Controls.Add(discordPictureBox);
            sidebarPanel.Controls.Add(youtubePictureBox);
            sidebarPanel.Controls.Add(versionLabel);
            sidebarPanel.Location = new Point(0, 0);
            sidebarPanel.Name = "sidebarPanel";
            sidebarPanel.Size = new Size(192, 600);
            sidebarPanel.TabIndex = 0;
            sidebarPanel.Paint += sidebarPanel_Paint;
            // 
            // pictureBox1
            // 
            pictureBox1.Image = (Image)resources.GetObject("pictureBox1.Image");
            pictureBox1.ImageLocation = "";
            pictureBox1.Location = new Point(14, 16);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(163, 41);
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox1.TabIndex = 51;
            pictureBox1.TabStop = false;
            pictureBox1.Click += pictureBox1_Click;
            // 
            // lblDetectSection
            // 
            lblDetectSection.Font = new Font("JetBrains Mono", 8.25F, FontStyle.Bold);
            lblDetectSection.ForeColor = Color.FromArgb(68, 68, 68);
            lblDetectSection.Location = new Point(14, 72);
            lblDetectSection.Name = "lblDetectSection";
            lblDetectSection.Size = new Size(163, 19);
            lblDetectSection.TabIndex = 40;
            lblDetectSection.Text = "[DETECT PATH]";
            // 
            // paypalPictureBox
            // 
            paypalPictureBox.Cursor = Cursors.Hand;
            paypalPictureBox.Image = (Image)resources.GetObject("paypalPictureBox.Image");
            paypalPictureBox.ImageLocation = "";
            paypalPictureBox.Location = new Point(14, 463);
            paypalPictureBox.Name = "paypalPictureBox";
            paypalPictureBox.Size = new Size(163, 41);
            paypalPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            paypalPictureBox.TabIndex = 16;
            paypalPictureBox.TabStop = false;
            paypalPictureBox.Click += PaypalPictureBox_Click;
            // 
            // autoDetectButton
            // 
            autoDetectButton.BackColor = Color.FromArgb(0, 0, 0);
            autoDetectButton.BorderColor = Color.FromArgb(51, 51, 51);
            autoDetectButton.BorderRadius = 0;
            autoDetectButton.Cursor = Cursors.Hand;
            autoDetectButton.FlatStyle = FlatStyle.Flat;
            autoDetectButton.Font = new Font("JetBrains Mono", 9F);
            autoDetectButton.ForeColor = Color.FromArgb(136, 136, 136);
            autoDetectButton.HighlightColor = Color.FromArgb(255, 255, 255);
            autoDetectButton.Highlighted = false;
            autoDetectButton.HoverBackColor = Color.FromArgb(26, 26, 26);
            autoDetectButton.HoverForeColor = Color.Empty;
            autoDetectButton.Location = new Point(14, 96);
            autoDetectButton.Name = "autoDetectButton";
            autoDetectButton.Size = new Size(163, 41);
            autoDetectButton.TabIndex = 1;
            autoDetectButton.Text = "[ AUTO DETECT ]";
            autoDetectButton.UseVisualStyleBackColor = false;
            autoDetectButton.Click += AutoDetectButton_Click;
            // 
            // manualDetectButton
            // 
            manualDetectButton.BackColor = Color.FromArgb(0, 0, 0);
            manualDetectButton.BorderColor = Color.FromArgb(51, 51, 51);
            manualDetectButton.BorderRadius = 0;
            manualDetectButton.Cursor = Cursors.Hand;
            manualDetectButton.FlatStyle = FlatStyle.Flat;
            manualDetectButton.Font = new Font("JetBrains Mono", 9F);
            manualDetectButton.ForeColor = Color.FromArgb(136, 136, 136);
            manualDetectButton.HighlightColor = Color.FromArgb(255, 255, 255);
            manualDetectButton.Highlighted = false;
            manualDetectButton.HoverBackColor = Color.FromArgb(26, 26, 26);
            manualDetectButton.HoverForeColor = Color.Empty;
            manualDetectButton.Location = new Point(14, 144);
            manualDetectButton.Name = "manualDetectButton";
            manualDetectButton.Size = new Size(163, 41);
            manualDetectButton.TabIndex = 2;
            manualDetectButton.Text = "[ MANUAL DETECT ]";
            manualDetectButton.UseVisualStyleBackColor = false;
            manualDetectButton.Click += ManualDetectButton_Click;
            // 
            // lblModsSection
            // 
            lblModsSection.Font = new Font("JetBrains Mono", 8.25F, FontStyle.Bold);
            lblModsSection.ForeColor = Color.FromArgb(68, 68, 68);
            lblModsSection.Location = new Point(14, 202);
            lblModsSection.Name = "lblModsSection";
            lblModsSection.Size = new Size(163, 19);
            lblModsSection.TabIndex = 41;
            lblModsSection.Text = "[MODS OPTION]";
            // 
            // btn_OpenSelectHero
            // 
            btn_OpenSelectHero.BackColor = Color.FromArgb(255, 255, 255);
            btn_OpenSelectHero.BorderColor = Color.FromArgb(51, 51, 51);
            btn_OpenSelectHero.BorderRadius = 0;
            btn_OpenSelectHero.Cursor = Cursors.Hand;
            btn_OpenSelectHero.FlatStyle = FlatStyle.Flat;
            btn_OpenSelectHero.Font = new Font("JetBrains Mono", 9F, FontStyle.Bold);
            btn_OpenSelectHero.ForeColor = Color.FromArgb(0, 0, 0);
            btn_OpenSelectHero.HighlightColor = Color.FromArgb(136, 136, 136);
            btn_OpenSelectHero.Highlighted = false;
            btn_OpenSelectHero.HoverBackColor = Color.FromArgb(0, 0, 0);
            btn_OpenSelectHero.HoverForeColor = Color.FromArgb(255, 255, 255);
            btn_OpenSelectHero.Location = new Point(14, 226);
            btn_OpenSelectHero.Name = "btn_OpenSelectHero";
            btn_OpenSelectHero.Size = new Size(163, 41);
            btn_OpenSelectHero.TabIndex = 3;
            btn_OpenSelectHero.Text = "[ SKIN SELECTOR ]";
            btn_OpenSelectHero.UseVisualStyleBackColor = false;
            btn_OpenSelectHero.Click += Btn_OpenSelectHero_Click;
            // 
            // lblToolsSection
            // 
            lblToolsSection.Font = new Font("JetBrains Mono", 8.25F, FontStyle.Bold);
            lblToolsSection.ForeColor = Color.FromArgb(68, 68, 68);
            lblToolsSection.Location = new Point(14, 331);
            lblToolsSection.Name = "lblToolsSection";
            lblToolsSection.Size = new Size(163, 19);
            lblToolsSection.TabIndex = 42;
            lblToolsSection.Text = "[TOOLS]";
            // 
            // updatePatcherButton
            // 
            updatePatcherButton.BackColor = Color.FromArgb(0, 0, 0);
            updatePatcherButton.BorderColor = Color.FromArgb(51, 51, 51);
            updatePatcherButton.BorderRadius = 0;
            updatePatcherButton.Cursor = Cursors.Hand;
            updatePatcherButton.FlatStyle = FlatStyle.Flat;
            updatePatcherButton.Font = new Font("JetBrains Mono", 9F);
            updatePatcherButton.ForeColor = Color.FromArgb(255, 255, 255);
            updatePatcherButton.HighlightColor = Color.FromArgb(255, 255, 255);
            updatePatcherButton.Highlighted = false;
            updatePatcherButton.HoverBackColor = Color.FromArgb(26, 26, 26);
            updatePatcherButton.HoverForeColor = Color.FromArgb(255, 255, 255);
            updatePatcherButton.Location = new Point(14, 355);
            updatePatcherButton.Name = "updatePatcherButton";
            updatePatcherButton.Size = new Size(163, 41);
            updatePatcherButton.TabIndex = 8;
            updatePatcherButton.Text = "[ PATCH UPDATE ]";
            updatePatcherButton.UseVisualStyleBackColor = false;
            updatePatcherButton.Click += UpdatePatcherButton_Click;
            // 
            // miscellaneousButton
            // 
            miscellaneousButton.BackColor = Color.FromArgb(255, 255, 255);
            miscellaneousButton.BorderColor = Color.FromArgb(51, 51, 51);
            miscellaneousButton.BorderRadius = 0;
            miscellaneousButton.Cursor = Cursors.Hand;
            miscellaneousButton.FlatStyle = FlatStyle.Flat;
            miscellaneousButton.Font = new Font("JetBrains Mono", 9F, FontStyle.Bold);
            miscellaneousButton.ForeColor = Color.FromArgb(0, 0, 0);
            miscellaneousButton.HighlightColor = Color.FromArgb(136, 136, 136);
            miscellaneousButton.Highlighted = false;
            miscellaneousButton.HoverBackColor = Color.FromArgb(0, 0, 0);
            miscellaneousButton.HoverForeColor = Color.FromArgb(255, 255, 255);
            miscellaneousButton.Location = new Point(14, 274);
            miscellaneousButton.Name = "miscellaneousButton";
            miscellaneousButton.Size = new Size(163, 41);
            miscellaneousButton.TabIndex = 7;
            miscellaneousButton.Text = "[ MISCELLANEOUS ]";
            miscellaneousButton.UseVisualStyleBackColor = false;
            miscellaneousButton.Click += MiscellaneousButton_Click;
            // 
            // dividerLabel
            // 
            dividerLabel.BackColor = Color.FromArgb(51, 51, 51);
            dividerLabel.Location = new Point(14, 446);
            dividerLabel.Name = "dividerLabel";
            dividerLabel.Size = new Size(163, 1);
            dividerLabel.TabIndex = 50;
            // 
            // statusModsDotLabel
            // 
            statusModsDotLabel.BackColor = Color.FromArgb(100, 100, 110);
            statusModsDotLabel.Location = new Point(18, 415);
            statusModsDotLabel.Name = "statusModsDotLabel";
            statusModsDotLabel.Size = new Size(12, 12);
            statusModsDotLabel.TabIndex = 10;
            // 
            // statusModsTextLabel
            // 
            statusModsTextLabel.AutoSize = true;
            statusModsTextLabel.Font = new Font("JetBrains Mono", 8.5F);
            statusModsTextLabel.ForeColor = Color.FromArgb(120, 120, 135);
            statusModsTextLabel.Location = new Point(37, 413);
            statusModsTextLabel.Name = "statusModsTextLabel";
            statusModsTextLabel.Size = new Size(98, 16);
            statusModsTextLabel.TabIndex = 11;
            statusModsTextLabel.Text = "- NOT CHECKED";
            // 
            // statusRefreshButton
            // 
            statusRefreshButton.AutoSize = true;
            statusRefreshButton.Cursor = Cursors.Hand;
            statusRefreshButton.Font = new Font("JetBrains Mono", 14F);
            statusRefreshButton.ForeColor = Color.FromArgb(100, 100, 100);
            statusRefreshButton.Location = new Point(157, 406);
            statusRefreshButton.Name = "statusRefreshButton";
            statusRefreshButton.Size = new Size(28, 25);
            statusRefreshButton.TabIndex = 52;
            statusRefreshButton.Text = "↻";
            statusRefreshButton.Click += StatusRefreshButton_Click;
            statusRefreshButton.MouseEnter += StatusRefreshButton_MouseEnter;
            statusRefreshButton.MouseLeave += StatusRefreshButton_MouseLeave;
            // 
            // discordPictureBox
            // 
            discordPictureBox.Cursor = Cursors.Hand;
            discordPictureBox.Image = (Image)resources.GetObject("discordPictureBox.Image");
            discordPictureBox.ImageLocation = "";
            discordPictureBox.Location = new Point(54, 511);
            discordPictureBox.Name = "discordPictureBox";
            discordPictureBox.Size = new Size(38, 38);
            discordPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            discordPictureBox.TabIndex = 14;
            discordPictureBox.TabStop = false;
            discordPictureBox.Click += DiscordPictureBox_Click;
            // 
            // youtubePictureBox
            // 
            youtubePictureBox.Cursor = Cursors.Hand;
            youtubePictureBox.Image = (Image)resources.GetObject("youtubePictureBox.Image");
            youtubePictureBox.ImageLocation = "";
            youtubePictureBox.Location = new Point(100, 511);
            youtubePictureBox.Name = "youtubePictureBox";
            youtubePictureBox.Size = new Size(38, 38);
            youtubePictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            youtubePictureBox.TabIndex = 15;
            youtubePictureBox.TabStop = false;
            youtubePictureBox.Click += YoutubePictureBox_Click;
            // 
            // versionLabel
            // 
            versionLabel.Font = new Font("JetBrains Mono", 7.5F);
            versionLabel.ForeColor = Color.FromArgb(70, 70, 85);
            versionLabel.Location = new Point(14, 564);
            versionLabel.Name = "versionLabel";
            versionLabel.Size = new Size(163, 19);
            versionLabel.TabIndex = 17;
            versionLabel.Text = "Checking...";
            versionLabel.TextAlign = ContentAlignment.MiddleCenter;
            versionLabel.Click += label2_Click;
            // 
            // installButton
            // 
            installButton.BackColor = Color.FromArgb(255, 255, 255);
            installButton.BorderColor = Color.FromArgb(51, 51, 51);
            installButton.BorderRadius = 0;
            installButton.Cursor = Cursors.Hand;
            installButton.FlatStyle = FlatStyle.Flat;
            installButton.Font = new Font("JetBrains Mono", 9F, FontStyle.Bold);
            installButton.ForeColor = Color.FromArgb(0, 0, 0);
            installButton.HighlightColor = Color.FromArgb(136, 136, 136);
            installButton.Highlighted = false;
            installButton.HoverBackColor = Color.FromArgb(0, 0, 0);
            installButton.HoverForeColor = Color.FromArgb(255, 255, 255);
            installButton.Location = new Point(216, 252);
            installButton.Name = "installButton";
            installButton.Size = new Size(288, 50);
            installButton.TabIndex = 4;
            installButton.Text = "[ INSTALL MODSPACK ]";
            installButton.UseVisualStyleBackColor = false;
            installButton.Click += InstallButton_Click;
            // 
            // disableButton
            // 
            disableButton.BackColor = Color.FromArgb(0, 0, 0);
            disableButton.BorderColor = Color.FromArgb(51, 51, 51);
            disableButton.BorderRadius = 0;
            disableButton.Cursor = Cursors.Hand;
            disableButton.FlatStyle = FlatStyle.Flat;
            disableButton.Font = new Font("JetBrains Mono", 9F);
            disableButton.ForeColor = Color.FromArgb(136, 136, 136);
            disableButton.HighlightColor = Color.FromArgb(255, 255, 255);
            disableButton.Highlighted = false;
            disableButton.HoverBackColor = Color.FromArgb(26, 26, 26);
            disableButton.HoverForeColor = Color.Empty;
            disableButton.Location = new Point(516, 252);
            disableButton.Name = "disableButton";
            disableButton.Size = new Size(300, 50);
            disableButton.TabIndex = 5;
            disableButton.Text = "[ DISABLE MODS ]";
            disableButton.UseVisualStyleBackColor = false;
            disableButton.Click += DisableButton_Click;
            // 
            // consolePanel
            // 
            consolePanel.BackColor = Color.FromArgb(0, 0, 0);
            consolePanel.BorderColor = Color.FromArgb(51, 51, 51);
            consolePanel.BorderRadius = 0;
            consolePanel.BorderThickness = 1;
            consolePanel.Controls.Add(mainConsoleBox);
            consolePanel.Location = new Point(216, 396);
            consolePanel.Name = "consolePanel";
            consolePanel.Padding = new Padding(12, 10, 12, 10);
            consolePanel.Size = new Size(600, 187);
            consolePanel.TabIndex = 55;
            // 
            // mainConsoleBox
            // 
            mainConsoleBox.BackColor = Color.Black;
            mainConsoleBox.Dock = DockStyle.Fill;
            mainConsoleBox.EnableGlow = true;
            mainConsoleBox.EnableScanlines = true;
            mainConsoleBox.Font = new Font("JetBrains Mono", 8F, FontStyle.Bold);
            mainConsoleBox.GlowRadius = 1;
            mainConsoleBox.Location = new Point(12, 10);
            mainConsoleBox.MaxLines = 500;
            mainConsoleBox.Name = "mainConsoleBox";
            mainConsoleBox.ScanlineAlpha = 15;
            mainConsoleBox.ScanlineSpacing = 3;
            mainConsoleBox.Size = new Size(576, 167);
            mainConsoleBox.TabIndex = 13;
            // 
            // copyConsoleBtn
            // 
            copyConsoleBtn.AutoSize = true;
            copyConsoleBtn.Cursor = Cursors.Hand;
            copyConsoleBtn.Font = new Font("JetBrains Mono", 9F);
            copyConsoleBtn.ForeColor = Color.FromArgb(100, 100, 100);
            copyConsoleBtn.Location = new Point(759, 372);
            copyConsoleBtn.Name = "copyConsoleBtn";
            copyConsoleBtn.Size = new Size(63, 16);
            copyConsoleBtn.TabIndex = 60;
            copyConsoleBtn.Text = "[ COPY ]";
            copyConsoleBtn.Click += copyConsoleBtn_Click;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("JetBrains Mono", 9F);
            label1.ForeColor = Color.FromArgb(68, 68, 68);
            label1.Location = new Point(212, 372);
            label1.Name = "label1";
            label1.Size = new Size(84, 16);
            label1.TabIndex = 14;
            label1.Text = ". / CONSOLE";
            label1.Click += label1_Click;
            // 
            // label3
            // 
            label3.BackColor = Color.FromArgb(51, 51, 51);
            label3.Location = new Point(216, 319);
            label3.Name = "label3";
            label3.Size = new Size(600, 1);
            label3.TabIndex = 22;
            // 
            // label4
            // 
            label4.BackColor = Color.FromArgb(51, 51, 51);
            label4.Location = new Point(216, 360);
            label4.Name = "label4";
            label4.Size = new Size(600, 1);
            label4.TabIndex = 23;
            // 
            // lblDotaWarning
            // 
            lblDotaWarning.BackColor = Color.FromArgb(180, 70, 70);
            lblDotaWarning.Font = new Font("JetBrains Mono", 9F, FontStyle.Bold);
            lblDotaWarning.ForeColor = Color.White;
            lblDotaWarning.Location = new Point(216, 323);
            lblDotaWarning.Name = "lblDotaWarning";
            lblDotaWarning.Size = new Size(600, 34);
            lblDotaWarning.TabIndex = 0;
            lblDotaWarning.Text = "/// ⚠ CLOSE DOTA 2 BEFORE MODIFYING ⚠ ///";
            lblDotaWarning.TextAlign = ContentAlignment.MiddleCenter;
            lblDotaWarning.Visible = false;
            // 
            // imageContainer
            // 
            imageContainer.BackColor = Color.Transparent;
            imageContainer.BorderColor = Color.FromArgb(51, 51, 51);
            imageContainer.BorderRadius = 0;
            imageContainer.BorderThickness = 1;
            imageContainer.Controls.Add(imagePictureBox);
            imageContainer.Location = new Point(216, 54);
            imageContainer.Name = "imageContainer";
            imageContainer.Size = new Size(600, 178);
            imageContainer.TabIndex = 30;
            // 
            // imagePictureBox
            // 
            imagePictureBox.BackColor = Color.Transparent;
            imagePictureBox.Dock = DockStyle.Fill;
            imagePictureBox.Location = new Point(0, 0);
            imagePictureBox.Name = "imagePictureBox";
            imagePictureBox.Size = new Size(600, 178);
            imagePictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            imagePictureBox.TabIndex = 0;
            imagePictureBox.TabStop = false;
            // 
            // headerPanel
            // 
            headerPanel.BackColor = Color.Transparent;
            headerPanel.Controls.Add(btnMinimize);
            headerPanel.Controls.Add(btnClose);
            headerPanel.Location = new Point(192, 0);
            headerPanel.Name = "headerPanel";
            headerPanel.Size = new Size(648, 38);
            headerPanel.TabIndex = 60;
            headerPanel.MouseDown += HeaderPanel_MouseDown;
            headerPanel.MouseMove += HeaderPanel_MouseMove;
            headerPanel.MouseUp += HeaderPanel_MouseUp;
            // 
            // btnMinimize
            // 
            btnMinimize.BackColor = Color.Transparent;
            btnMinimize.Cursor = Cursors.Hand;
            btnMinimize.Font = new Font("JetBrains Mono", 9F, FontStyle.Bold);
            btnMinimize.ForeColor = Color.FromArgb(68, 68, 68);
            btnMinimize.Location = new Point(576, 6);
            btnMinimize.Name = "btnMinimize";
            btnMinimize.Size = new Size(29, 29);
            btnMinimize.TabIndex = 1;
            btnMinimize.Text = "─";
            btnMinimize.TextAlign = ContentAlignment.MiddleCenter;
            btnMinimize.Click += BtnMinimize_Click;
            btnMinimize.MouseEnter += BtnMinimize_MouseEnter;
            btnMinimize.MouseLeave += BtnMinimize_MouseLeave;
            // 
            // btnClose
            // 
            btnClose.BackColor = Color.Transparent;
            btnClose.Cursor = Cursors.Hand;
            btnClose.Font = new Font("JetBrains Mono", 10F, FontStyle.Bold);
            btnClose.ForeColor = Color.FromArgb(68, 68, 68);
            btnClose.Location = new Point(610, 5);
            btnClose.Name = "btnClose";
            btnClose.Size = new Size(29, 29);
            btnClose.TabIndex = 2;
            btnClose.Text = "✕";
            btnClose.TextAlign = ContentAlignment.MiddleCenter;
            btnClose.Click += BtnClose_Click;
            btnClose.MouseEnter += BtnClose_MouseEnter;
            btnClose.MouseLeave += BtnClose_MouseLeave;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(0, 0, 0);
            ClientSize = new Size(840, 600);
            Controls.Add(sidebarPanel);
            Controls.Add(headerPanel);
            Controls.Add(imageContainer);
            Controls.Add(installButton);
            Controls.Add(disableButton);
            Controls.Add(label3);
            Controls.Add(label4);
            Controls.Add(label1);
            Controls.Add(copyConsoleBtn);
            Controls.Add(consolePanel);
            Controls.Add(lblDotaWarning);
            FormBorderStyle = FormBorderStyle.None;
            Icon = (Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            Name = "MainForm";
            SizeGripStyle = SizeGripStyle.Hide;
            StartPosition = FormStartPosition.CenterScreen;
            Text = "ArdysaModsTools";
            Load += Form1_Load;
            sidebarPanel.ResumeLayout(false);
            sidebarPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            ((System.ComponentModel.ISupportInitialize)paypalPictureBox).EndInit();
            ((System.ComponentModel.ISupportInitialize)discordPictureBox).EndInit();
            ((System.ComponentModel.ISupportInitialize)youtubePictureBox).EndInit();
            consolePanel.ResumeLayout(false);
            imageContainer.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)imagePictureBox).EndInit();
            headerPanel.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        // Field declarations
        private System.Windows.Forms.Panel sidebarPanel;
        private System.Windows.Forms.Label lblDetectSection;
        private System.Windows.Forms.Label lblModsSection;
        private System.Windows.Forms.Label lblToolsSection;
        private RoundedButton miscellaneousButton;
        private RoundedButton autoDetectButton;
        private RoundedButton manualDetectButton;
        private RoundedButton installButton;
        private RoundedButton disableButton;
        private System.Windows.Forms.Label dividerLabel;
        private RoundedButton updatePatcherButton;
        private System.Windows.Forms.Label statusModsDotLabel;
        private System.Windows.Forms.Label statusModsTextLabel;
        private System.Windows.Forms.Label statusRefreshButton;
        private RetroTerminal mainConsoleBox;
        private Label copyConsoleBtn;
        private Label label1;
        private Label versionLabel;
        private PictureBox discordPictureBox;
        private PictureBox paypalPictureBox;
        private RoundedPanel imageContainer;
        private PictureBox imagePictureBox;
        private Label label3;
        private Label label4;
        private Label lblDotaWarning;
        private RoundedButton btn_OpenSelectHero;
        private PictureBox youtubePictureBox;
        private RoundedPanel consolePanel;
        private Panel headerPanel;
        private Label btnMinimize;
        private Label btnClose;
        private PictureBox pictureBox1;
        private NotifyIcon trayIcon;
    }
}