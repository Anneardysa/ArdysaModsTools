using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;

namespace ArdysaModsTools.UI.Forms
{
    /// <summary>
    /// Modern styled form to display detailed mod status and version information.
    /// </summary>
    public class StatusDetailsForm : Form
    {
        private readonly ModStatusInfo _statusInfo;
        private readonly DotaVersionInfo _versionInfo;
        private readonly Action? _onPatchRequested;
        
        private Panel headerPanel = null!;
        private Label titleLabel = null!;
        private Label closeButton = null!;
        private RichTextBox detailsBox = null!;
        private Panel buttonPanel = null!;
        private Button patchButton = null!;
        private Button closeBtn = null!;

        private bool _dragging;
        private Point _dragStart;

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);

        public StatusDetailsForm(ModStatusInfo statusInfo, DotaVersionInfo versionInfo, Action? onPatchRequested = null)
        {
            _statusInfo = statusInfo;
            _versionInfo = versionInfo;
            _onPatchRequested = onPatchRequested;
            
            InitializeComponents();
            PopulateDetails();
        }

        private void InitializeComponents()
        {
            // Form settings
            this.Text = "Status Details";
            this.Size = new Size(480, 420);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(28, 28, 38);
            this.ShowInTaskbar = false;
            this.KeyPreview = true;
            this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) this.Close(); };

            // Rounded corners
            this.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 12, 12));
            this.Resize += (s, e) => this.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 12, 12));

            // Header panel
            headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.FromArgb(35, 35, 48),
                Padding = new Padding(12, 0, 12, 0)
            };
            headerPanel.MouseDown += Header_MouseDown;
            headerPanel.MouseMove += Header_MouseMove;
            headerPanel.MouseUp += Header_MouseUp;

            titleLabel = new Label
            {
                Text = "Version Status Details",
                Font = new Font("JetBrains Mono", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(220, 220, 230),
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            titleLabel.MouseDown += Header_MouseDown;
            titleLabel.MouseMove += Header_MouseMove;
            titleLabel.MouseUp += Header_MouseUp;

            closeButton = new Label
            {
                Text = "✕",
                Font = new Font("JetBrains Mono", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(150, 150, 160),
                Size = new Size(30, 30),
                Dock = DockStyle.Right,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            closeButton.Click += (s, e) => this.Close();
            closeButton.MouseEnter += (s, e) => closeButton.ForeColor = Color.FromArgb(255, 100, 100);
            closeButton.MouseLeave += (s, e) => closeButton.ForeColor = Color.FromArgb(150, 150, 160);

            headerPanel.Controls.Add(titleLabel);
            headerPanel.Controls.Add(closeButton);

            // Details text box
            detailsBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(22, 22, 30),
                ForeColor = Color.FromArgb(200, 200, 210),
                Font = new Font("Cascadia Code", 9F),
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                Padding = new Padding(16)
            };

            // Container panel for padding
            var contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16, 12, 16, 12),
                BackColor = Color.FromArgb(28, 28, 38)
            };
            contentPanel.Controls.Add(detailsBox);

            // Button panel
            buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = Color.FromArgb(28, 28, 38),
                Padding = new Padding(16, 8, 16, 16)
            };

            patchButton = new Button
            {
                Text = "Apply Patches",
                Size = new Size(130, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(76, 175, 80),
                ForeColor = Color.White,
                Font = new Font("JetBrains Mono", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Right
            };
            patchButton.FlatAppearance.BorderSize = 0;
            patchButton.Location = new Point(buttonPanel.Width - 290, 8);
            patchButton.Click += PatchButton_Click;
            // Show patch button when status indicates update is needed (consistent with ModStatusInfo)
            patchButton.Visible = _statusInfo.Status == ModStatus.NeedUpdate || 
                                  _statusInfo.Status == ModStatus.Disabled;

            closeBtn = new Button
            {
                Text = "Close",
                Size = new Size(100, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(55, 55, 70),
                ForeColor = Color.FromArgb(180, 180, 190),
                Font = new Font("JetBrains Mono", 9F),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Right
            };
            closeBtn.FlatAppearance.BorderSize = 0;
            closeBtn.Location = new Point(buttonPanel.Width - 130, 8);
            closeBtn.Click += (s, e) => this.Close();

            buttonPanel.Controls.Add(patchButton);
            buttonPanel.Controls.Add(closeBtn);

            // Add controls
            this.Controls.Add(contentPanel);
            this.Controls.Add(buttonPanel);
            this.Controls.Add(headerPanel);

            // Position buttons on load
            this.Load += (s, e) => PositionButtons();
            this.Resize += (s, e) => PositionButtons();
        }

        private void PositionButtons()
        {
            int rightMargin = 16;
            closeBtn.Location = new Point(buttonPanel.Width - closeBtn.Width - rightMargin, 8);
            patchButton.Location = new Point(closeBtn.Left - patchButton.Width - 10, 8);
        }

        private void PopulateDetails()
        {
            detailsBox.Clear();
            detailsBox.SelectionAlignment = HorizontalAlignment.Center;
            
            // Top spacing
            AppendLine();
            
            // === MAIN RESULT (Most Important - Show First) ===
            // Use ModStatusInfo.Status for consistency with MainForm status display
            bool needsUpdate = _statusInfo.Status == ModStatus.NeedUpdate || 
                               _statusInfo.Status == ModStatus.Disabled;
            
            if (needsUpdate)
            {
                AppendLine("⚠", Color.FromArgb(255, 180, 100), 18f);
                AppendLine();
                AppendLine("UPDATE NEEDED", Color.FromArgb(255, 180, 100), 12f);
                AppendLine();
                AppendLine("Your mods need to be patched", Color.FromArgb(160, 160, 175));
            }
            else if (_statusInfo.Status == ModStatus.NotInstalled)
            {
                AppendLine("○", Color.FromArgb(120, 120, 120), 18f);
                AppendLine();
                AppendLine("NOT INSTALLED", Color.FromArgb(120, 120, 120), 12f);
                AppendLine();
                AppendLine("ModsPack is not installed yet", Color.FromArgb(160, 160, 175));
            }
            else if (_statusInfo.Status == ModStatus.Error)
            {
                AppendLine("✕", Color.FromArgb(255, 80, 80), 18f);
                AppendLine();
                AppendLine("ERROR", Color.FromArgb(255, 80, 80), 12f);
                AppendLine();
                AppendLine(_statusInfo.ErrorMessage ?? "An error occurred", Color.FromArgb(160, 160, 175));
            }
            else
            {
                AppendLine("✓", Color.FromArgb(100, 220, 100), 18f);
                AppendLine();
                AppendLine("ALL GOOD", Color.FromArgb(100, 220, 100), 12f);
                AppendLine();
                AppendLine("Your mods are working correctly", Color.FromArgb(140, 180, 140));
            }
            
            AppendLine();
            AppendLine();
            
            // === VERSION INFO ===
            AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━", Color.FromArgb(50, 50, 65));
            AppendLine();
            
            string currentVer = _versionInfo.DotaVersion;
            string patchedVer = _versionInfo.LastPatchedVersion ?? "—";
            
            AppendLine("VERSION", Color.FromArgb(100, 100, 120));
            AppendLine();
            AppendText(currentVer, Color.FromArgb(200, 200, 215));
            AppendText("  ›  ", Color.FromArgb(80, 80, 100));
            AppendLine(patchedVer, Color.FromArgb(150, 150, 170));
            AppendLine($"Build {_versionInfo.BuildNumber}", Color.FromArgb(100, 100, 120));
            
            if (_versionInfo.LastPatchedDate.HasValue)
            {
                AppendLine();
                AppendLine($"Patched {_versionInfo.LastPatchedDate.Value:MMM dd, yyyy}", Color.FromArgb(90, 90, 110));
            }
            
            AppendLine();
            AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━", Color.FromArgb(50, 50, 65));
            AppendLine();
            
            // === STATUS CHECKS ===
            AppendLine("STATUS", Color.FromArgb(100, 100, 120));
            AppendLine();
            
            // Game Compatibility - green if status is Ready
            if (_statusInfo.Status == ModStatus.Ready)
            {
                AppendText("●", Color.FromArgb(100, 200, 100));
                AppendLine("  Game Compatibility", Color.FromArgb(200, 200, 210));
            }
            else if (_statusInfo.Status == ModStatus.NeedUpdate)
            {
                AppendText("●", Color.FromArgb(255, 180, 100));
                AppendLine("  Game Compatibility", Color.FromArgb(200, 200, 210));
            }
            else
            {
                AppendText("●", Color.FromArgb(255, 100, 100));
                AppendLine("  Game Compatibility", Color.FromArgb(200, 200, 210));
            }
            
            // Mod Integration - green if gameinfo has mod entry
            if (_versionInfo.GameInfoHasModEntry)
            {
                AppendText("●", Color.FromArgb(100, 200, 100));
                AppendLine("  Mod Integration", Color.FromArgb(200, 200, 210));
            }
            else
            {
                AppendText("●", Color.FromArgb(255, 100, 100));
                AppendLine("  Mod Integration", Color.FromArgb(200, 200, 210));
            }
            
            AppendLine();
        }

        private void AppendLine(string text, Color color, float fontSize)
        {
            var originalFont = detailsBox.SelectionFont ?? detailsBox.Font;
            detailsBox.SelectionFont = new Font(originalFont.FontFamily, fontSize, FontStyle.Bold);
            AppendLine(text, color);
            detailsBox.SelectionFont = originalFont;
        }

        private void AppendText(string text, Color color)
        {
            detailsBox.SelectionStart = detailsBox.TextLength;
            detailsBox.SelectionLength = 0;
            detailsBox.SelectionColor = color;
            detailsBox.AppendText(text);
            detailsBox.SelectionColor = detailsBox.ForeColor;
        }

        private void AppendLine(string text, Color color)
        {
            AppendText(text + Environment.NewLine, color);
        }

        private void AppendLine(string text = "")
        {
            AppendText(text + Environment.NewLine, detailsBox.ForeColor);
        }

        private static string Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return "N/A";
            return value.Length <= maxLength ? value : value[..maxLength] + "...";
        }

        private void PatchButton_Click(object? sender, EventArgs e)
        {
            this.Close();
            _onPatchRequested?.Invoke();
        }

        #region Window Dragging

        private void Header_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                _dragStart = e.Location;
            }
        }

        private void Header_MouseMove(object? sender, MouseEventArgs e)
        {
            if (_dragging)
            {
                Point diff = new Point(e.X - _dragStart.X, e.Y - _dragStart.Y);
                this.Location = new Point(this.Location.X + diff.X, this.Location.Y + diff.Y);
            }
        }

        private void Header_MouseUp(object? sender, MouseEventArgs e)
        {
            _dragging = false;
        }

        #endregion
    }
}
