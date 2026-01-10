using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ArdysaModsTools.UI.Controls;

namespace ArdysaModsTools.UI.Forms
{
    /// <summary>
    /// Preview dialog showing selected misc options before generation.
    /// Styled to match GenerationPreviewForm theme.
    /// </summary>
    public sealed class MiscGenerationPreviewForm : Form
    {
        private FlowLayoutPanel _itemsPanel = null!;
        private RoundedButton _generateBtn = null!;
        private RoundedButton _cancelBtn = null!;
        private Label _headerLabel = null!;
        private Label _subHeaderLabel = null!;
        private readonly Dictionary<string, string> _selections;
        private bool _isClosing;

        public bool Confirmed { get; private set; }

        public MiscGenerationPreviewForm(Dictionary<string, string> selections)
        {
            _selections = selections ?? new Dictionary<string, string>();
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            // Form settings - borderless modern look
            Text = "Confirm Generation";
            Size = new Size(480, 450);
            MinimumSize = new Size(400, 350);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Color.Black;
            Font = new Font("JetBrains Mono", 10F);
            Padding = new Padding(1);
            KeyPreview = true;
            
            // Handle Escape key to cancel
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape && !_isClosing)
                {
                    _isClosing = true;
                    Confirmed = false;
                    DialogResult = DialogResult.Cancel;
                    e.Handled = true;
                }
            };

            // Main container with border
            var mainContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                Padding = new Padding(0)
            };
            Controls.Add(mainContainer);

            // Header panel
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.Black,
                Padding = new Padding(20, 15, 20, 10)
            };
            mainContainer.Controls.Add(headerPanel);

            _headerLabel = new Label
            {
                Text = "⚠ CONFIRM GENERATION ⚠",
                Dock = DockStyle.Top,
                Height = 30,
                ForeColor = Color.White,
                Font = new Font("JetBrains Mono", 12F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            headerPanel.Controls.Add(_headerLabel);

            _subHeaderLabel = new Label
            {
                Text = $"The following {_selections.Count} option(s) will be applied:",
                Dock = DockStyle.Bottom,
                Height = 25,
                ForeColor = Color.FromArgb(136, 136, 136),
                Font = new Font("JetBrains Mono", 9F),
                TextAlign = ContentAlignment.MiddleCenter
            };
            headerPanel.Controls.Add(_subHeaderLabel);

            // Scrollable items panel
            _itemsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.Black,
                Padding = new Padding(15, 10, 15, 10)
            };
            mainContainer.Controls.Add(_itemsPanel);

            // Button panel
            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 80,
                BackColor = Color.Black,
                Padding = new Padding(15, 12, 15, 12)
            };
            mainContainer.Controls.Add(buttonPanel);

            // Generate button - white bg, black text (primary action)
            _generateBtn = new RoundedButton
            {
                Text = "[ GENERATE ]",
                Size = new Size(150, 44),
                BackColor = Color.White,
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("JetBrains Mono", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                BorderRadius = 0,
                BorderColor = Color.FromArgb(51, 51, 51),
                HoverBackColor = Color.Black,
                HoverForeColor = Color.White
            };
            _generateBtn.FlatAppearance.BorderSize = 0;
            _generateBtn.Click += (s, e) =>
            {
                if (_isClosing) return;
                _isClosing = true;
                Confirmed = true;
                DialogResult = DialogResult.OK;
            };

            // Cancel button - black bg, grey text (secondary action)
            _cancelBtn = new RoundedButton
            {
                Text = "[ CANCEL ]",
                Size = new Size(120, 44),
                BackColor = Color.Black,
                ForeColor = Color.FromArgb(136, 136, 136),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("JetBrains Mono", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                BorderRadius = 0,
                BorderColor = Color.FromArgb(51, 51, 51),
                HoverBackColor = Color.FromArgb(26, 26, 26),
                HoverForeColor = Color.White
            };
            _cancelBtn.FlatAppearance.BorderSize = 0;
            _cancelBtn.Click += (s, e) =>
            {
                if (_isClosing) return;
                _isClosing = true;
                Confirmed = false;
                DialogResult = DialogResult.Cancel;
            };

            buttonPanel.Controls.Add(_generateBtn);
            buttonPanel.Controls.Add(_cancelBtn);

            // Center buttons
            buttonPanel.Resize += (s, e) =>
            {
                int totalWidth = _cancelBtn.Width + 10 + _generateBtn.Width;
                int startX = (buttonPanel.Width - totalWidth) / 2;
                _cancelBtn.Location = new Point(startX, 15);
                _generateBtn.Location = new Point(startX + _cancelBtn.Width + 10, 15);
            };

            // Bring panels to front in correct order
            _itemsPanel.BringToFront();

            // Make form draggable from header
            headerPanel.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    NativeMethods.ReleaseCapture();
                    NativeMethods.SendMessage(Handle, 0xA1, 0x2, 0);
                }
            };
            
            // Populate items
            LoadItems();
        }

        private void LoadItems()
        {
            foreach (var kvp in _selections)
            {
                var item = CreateItemPanel(kvp.Key, kvp.Value);
                _itemsPanel.Controls.Add(item);
            }
        }

        private Panel CreateItemPanel(string optionName, string choiceName)
        {
            var panel = new Panel
            {
                Width = _itemsPanel.ClientSize.Width - 35,
                Height = 40,
                BackColor = Color.FromArgb(15, 15, 15),
                Margin = new Padding(0, 0, 0, 4)
            };

            // Add border effect via paint
            panel.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(51, 51, 51), 1);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
            };

            // Option name label
            var optionLabel = new Label
            {
                Text = optionName + ":",
                Location = new Point(12, 10),
                Size = new Size(150, 20),
                ForeColor = Color.FromArgb(150, 150, 150),
                Font = new Font("JetBrains Mono", 9F)
            };
            panel.Controls.Add(optionLabel);

            // Choice value label (cyan)
            var choiceLabel = new Label
            {
                Text = choiceName,
                Location = new Point(165, 10),
                Size = new Size(panel.Width - 180, 20),
                ForeColor = Color.FromArgb(0, 255, 255), // Cyan
                Font = new Font("JetBrains Mono", 9F, FontStyle.Bold)
            };
            panel.Controls.Add(choiceLabel);

            // Resize handler
            _itemsPanel.Resize += (s, e) =>
            {
                panel.Width = _itemsPanel.ClientSize.Width - 35;
                choiceLabel.Width = panel.Width - 180;
            };

            return panel;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Draw border around form
            using var pen = new Pen(Color.FromArgb(51, 51, 51), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        private static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
            
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern bool ReleaseCapture();
        }
    }
}
