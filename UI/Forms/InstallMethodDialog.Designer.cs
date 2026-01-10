using ArdysaModsTools.UI.Controls;

namespace ArdysaModsTools.UI.Forms
{
    partial class InstallMethodDialog
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
            titleLabel = new Label();
            descriptionLabel = new Label();
            btnAutoInstall = new RoundedButton();
            btnManualInstall = new RoundedButton();
            autoInstallDescLabel = new Label();
            manualInstallDescLabel = new Label();
            SuspendLayout();
            // 
            // titleLabel
            // 
            titleLabel.AutoSize = true;
            titleLabel.Font = new Font("JetBrains Mono", 12F, FontStyle.Bold);
            titleLabel.ForeColor = Color.FromArgb(255, 255, 255);
            titleLabel.Location = new Point(68, 24);
            titleLabel.Name = "titleLabel";
            titleLabel.Size = new Size(260, 21);
            titleLabel.TabIndex = 0;
            titleLabel.Text = "⚠ SELECT INSTALL METHOD ⚠";
            titleLabel.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // descriptionLabel
            // 
            descriptionLabel.AutoSize = true;
            descriptionLabel.Font = new Font("JetBrains Mono", 8.25F);
            descriptionLabel.ForeColor = Color.White;
            descriptionLabel.Location = new Point(67, 52);
            descriptionLabel.Name = "descriptionLabel";
            descriptionLabel.Size = new Size(259, 14);
            descriptionLabel.TabIndex = 1;
            descriptionLabel.Text = "Choose how you want to install mods:";
            // 
            // btnAutoInstall
            // 
            btnAutoInstall.BackColor = Color.FromArgb(255, 255, 255);
            btnAutoInstall.BorderColor = Color.FromArgb(51, 51, 51);
            btnAutoInstall.BorderRadius = 0;
            btnAutoInstall.Cursor = Cursors.Hand;
            btnAutoInstall.FlatStyle = FlatStyle.Flat;
            btnAutoInstall.Font = new Font("JetBrains Mono", 10F, FontStyle.Bold);
            btnAutoInstall.ForeColor = Color.FromArgb(0, 0, 0);
            btnAutoInstall.HighlightColor = Color.FromArgb(255, 255, 255);
            btnAutoInstall.Highlighted = false;
            btnAutoInstall.HoverBackColor = Color.FromArgb(0, 0, 0);
            btnAutoInstall.HoverForeColor = Color.FromArgb(255, 255, 255);
            btnAutoInstall.Location = new Point(24, 90);
            btnAutoInstall.Name = "btnAutoInstall";
            btnAutoInstall.Size = new Size(352, 42);
            btnAutoInstall.TabIndex = 1;
            btnAutoInstall.Text = "[ AUTO-INSTALL ]";
            btnAutoInstall.UseVisualStyleBackColor = false;
            btnAutoInstall.Click += BtnAutoInstall_Click;
            // 
            // btnManualInstall
            // 
            btnManualInstall.BackColor = Color.FromArgb(0, 0, 0);
            btnManualInstall.BorderColor = Color.FromArgb(51, 51, 51);
            btnManualInstall.BorderRadius = 0;
            btnManualInstall.Cursor = Cursors.Hand;
            btnManualInstall.FlatStyle = FlatStyle.Flat;
            btnManualInstall.Font = new Font("JetBrains Mono", 10F, FontStyle.Bold);
            btnManualInstall.ForeColor = Color.FromArgb(136, 136, 136);
            btnManualInstall.HighlightColor = Color.FromArgb(255, 255, 255);
            btnManualInstall.Highlighted = false;
            btnManualInstall.HoverBackColor = Color.FromArgb(26, 26, 26);
            btnManualInstall.HoverForeColor = Color.Empty;
            btnManualInstall.Location = new Point(24, 185);
            btnManualInstall.Name = "btnManualInstall";
            btnManualInstall.Size = new Size(352, 42);
            btnManualInstall.TabIndex = 2;
            btnManualInstall.Text = "[ MANUAL-INSTALL ]";
            btnManualInstall.UseVisualStyleBackColor = false;
            btnManualInstall.Click += BtnManualInstall_Click;
            // 
            // autoInstallDescLabel
            // 
            autoInstallDescLabel.Font = new Font("JetBrains Mono", 8F);
            autoInstallDescLabel.ForeColor = Color.FromArgb(136, 136, 136);
            autoInstallDescLabel.Location = new Point(24, 138);
            autoInstallDescLabel.Name = "autoInstallDescLabel";
            autoInstallDescLabel.Size = new Size(352, 32);
            autoInstallDescLabel.TabIndex = 2;
            autoInstallDescLabel.Text = "Download and install the latest ModsPack\nautomatically from the server.";
            autoInstallDescLabel.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // manualInstallDescLabel
            // 
            manualInstallDescLabel.Font = new Font("JetBrains Mono", 8F);
            manualInstallDescLabel.ForeColor = Color.FromArgb(136, 136, 136);
            manualInstallDescLabel.Location = new Point(24, 233);
            manualInstallDescLabel.Name = "manualInstallDescLabel";
            manualInstallDescLabel.Size = new Size(352, 32);
            manualInstallDescLabel.TabIndex = 3;
            manualInstallDescLabel.Text = "Browse and select VPK file\n to install manually.";
            manualInstallDescLabel.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // InstallMethodDialog
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(0, 0, 0);
            ClientSize = new Size(400, 285);
            Controls.Add(titleLabel);
            Controls.Add(descriptionLabel);
            Controls.Add(btnAutoInstall);
            Controls.Add(autoInstallDescLabel);
            Controls.Add(btnManualInstall);
            Controls.Add(manualInstallDescLabel);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "InstallMethodDialog";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Install Mods";
            ResumeLayout(false);
            PerformLayout();
        }

        private Label titleLabel;
        private Label descriptionLabel;
        private RoundedButton btnAutoInstall;
        private RoundedButton btnManualInstall;
        private Label autoInstallDescLabel;
        private Label manualInstallDescLabel;
    }
}
