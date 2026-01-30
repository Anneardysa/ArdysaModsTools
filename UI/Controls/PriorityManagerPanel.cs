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
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;

namespace ArdysaModsTools.UI.Controls
{
    /// <summary>
    /// Panel for managing mod priorities with visual ordering.
    /// Allows users to reorder mods to control conflict resolution precedence.
    /// </summary>
    public class PriorityManagerPanel : Panel
    {
        private readonly IModPriorityService? _priorityService;
        private string? _targetPath;
        private List<ModPriority> _priorities = new();
        private Panel _listContainer = null!;
        private Panel? _selectedItem;
        private int _selectedIndex = -1;
        private RoundedButton _btnMoveUp = null!;
        private RoundedButton _btnMoveDown = null!;
        private RoundedButton _btnSave = null!;
        private RoundedButton _btnReset = null!;
        private Label _statusLabel = null!;
        private bool _hasChanges = false;

        /// <summary>
        /// Event raised when priorities are saved.
        /// </summary>
        public event EventHandler? PrioritiesSaved;

        /// <summary>
        /// Creates a new PriorityManagerPanel.
        /// </summary>
        /// <param name="priorityService">Optional service for loading/saving priorities.</param>
        public PriorityManagerPanel(IModPriorityService? priorityService = null)
        {
            _priorityService = priorityService;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Size = new Size(400, 450);
            BackColor = Color.Black;
            Padding = new Padding(15);

            // Header
            var headerLabel = new Label
            {
                Text = "[ MOD PRIORITY ORDER ]",
                Font = new Font("JetBrains Mono", 12f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 40,
                TextAlign = ContentAlignment.MiddleCenter
            };
            Controls.Add(headerLabel);

            // Description
            var descLabel = new Label
            {
                Text = "Higher position = Higher priority (wins conflicts)",
                Font = new Font("JetBrains Mono", 8f),
                ForeColor = Color.FromArgb(136, 136, 136),
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 25,
                TextAlign = ContentAlignment.TopCenter
            };
            Controls.Add(descLabel);

            // List container
            _listContainer = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(10, 10, 10),
                Padding = new Padding(5)
            };
            _listContainer.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(51, 51, 51), 1);
                e.Graphics.DrawRectangle(pen, 0, 0, _listContainer.Width - 1, _listContainer.Height - 1);
            };

            // Button panel
            var buttonPanel = CreateButtonPanel();
            Controls.Add(buttonPanel);
            Controls.Add(_listContainer);

            // Ensure proper z-order
            headerLabel.BringToFront();
            descLabel.BringToFront();
        }

        private Panel CreateButtonPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 110,
                BackColor = Color.Black,
                Padding = new Padding(0, 10, 0, 0)
            };

            // Move buttons row
            var movePanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point((Width - 200) / 2, 5)
            };

            _btnMoveUp = CreateButton("▲ UP", 80);
            _btnMoveUp.Click += (s, e) => MoveSelected(-1);
            _btnMoveUp.Enabled = false;

            _btnMoveDown = CreateButton("▼ DOWN", 80);
            _btnMoveDown.Click += (s, e) => MoveSelected(1);
            _btnMoveDown.Enabled = false;

            movePanel.Controls.Add(_btnMoveUp);
            movePanel.Controls.Add(_btnMoveDown);

            // Save/Reset row
            var actionPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point((Width - 220) / 2, 50)
            };

            _btnSave = CreateButton("[ SAVE ]", 100, true);
            _btnSave.Click += async (s, e) => await SavePrioritiesAsync();
            _btnSave.Enabled = false;

            _btnReset = CreateButton("[ RESET ]", 100);
            _btnReset.Click += async (s, e) => await LoadPrioritiesAsync();

            actionPanel.Controls.Add(_btnReset);
            actionPanel.Controls.Add(_btnSave);

            // Status label
            _statusLabel = new Label
            {
                Text = "",
                Font = new Font("JetBrains Mono", 8f),
                ForeColor = Color.FromArgb(100, 255, 100),
                AutoSize = true,
                Location = new Point(15, 90)
            };

            panel.Controls.Add(movePanel);
            panel.Controls.Add(actionPanel);
            panel.Controls.Add(_statusLabel);

            // Center on resize
            panel.Resize += (s, e) =>
            {
                movePanel.Location = new Point((panel.Width - movePanel.Width) / 2, 5);
                actionPanel.Location = new Point((panel.Width - actionPanel.Width) / 2, 50);
            };

            return panel;
        }

        private RoundedButton CreateButton(string text, int width, bool primary = false)
        {
            return new RoundedButton
            {
                Text = text,
                Size = new Size(width, 36),
                BackColor = primary ? Color.White : Color.FromArgb(26, 26, 26),
                ForeColor = primary ? Color.Black : Color.FromArgb(200, 200, 200),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("JetBrains Mono", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                BorderRadius = 0,
                BorderColor = Color.FromArgb(51, 51, 51),
                HoverBackColor = primary ? Color.FromArgb(200, 200, 200) : Color.FromArgb(40, 40, 40),
                HoverForeColor = primary ? Color.Black : Color.White,
                Margin = new Padding(5)
            };
        }

        /// <summary>
        /// Loads priorities from the specified target path.
        /// </summary>
        public async Task LoadPrioritiesAsync(string? targetPath = null)
        {
            if (targetPath != null)
                _targetPath = targetPath;

            if (_priorityService == null || string.IsNullOrEmpty(_targetPath))
            {
                // Demo data for design-time or when no service
                _priorities = new List<ModPriority>
                {
                    new() { ModId = "demo_1", ModName = "High Priority Mod", Category = "Weather", Priority = 1 },
                    new() { ModId = "demo_2", ModName = "Medium Priority", Category = "River", Priority = 50 },
                    new() { ModId = "demo_3", ModName = "Low Priority Mod", Category = "Shader", Priority = 100 }
                };
            }
            else
            {
                var orderedPriorities = await _priorityService.GetOrderedPrioritiesAsync(_targetPath);
                _priorities = orderedPriorities.ToList();
            }

            RefreshList();
            _hasChanges = false;
            UpdateButtonStates();
            SetStatus("");
        }

        /// <summary>
        /// Sets the priority list directly (for testing or external data).
        /// </summary>
        public void SetPriorities(IEnumerable<ModPriority> priorities)
        {
            _priorities = priorities.OrderBy(p => p.Priority).ToList();
            RefreshList();
            _hasChanges = false;
            UpdateButtonStates();
        }

        private void RefreshList()
        {
            _listContainer.Controls.Clear();
            _selectedItem = null;
            _selectedIndex = -1;

            int yPos = 5;
            for (int i = 0; i < _priorities.Count; i++)
            {
                var item = CreatePriorityItem(_priorities[i], i, yPos);
                _listContainer.Controls.Add(item);
                yPos += 50;
            }
        }

        private Panel CreatePriorityItem(ModPriority priority, int index, int yPos)
        {
            var item = new Panel
            {
                Size = new Size(_listContainer.Width - 30, 45),
                Location = new Point(5, yPos),
                BackColor = Color.FromArgb(15, 15, 15),
                Cursor = Cursors.Hand,
                Tag = index
            };

            // Priority number
            var numLabel = new Label
            {
                Text = $"{index + 1}.",
                Font = new Font("JetBrains Mono", 11f, FontStyle.Bold),
                ForeColor = Theme.Accent,
                Size = new Size(35, 40),
                Location = new Point(10, 5),
                TextAlign = ContentAlignment.MiddleRight,
                Cursor = Cursors.Hand
            };

            // Mod name
            var nameLabel = new Label
            {
                Text = priority.ModName,
                Font = new Font("JetBrains Mono", 10f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(55, 8),
                Cursor = Cursors.Hand
            };

            // Category badge
            var catLabel = new Label
            {
                Text = $"[{priority.Category}]",
                Font = new Font("JetBrains Mono", 8f),
                ForeColor = Color.FromArgb(136, 136, 136),
                AutoSize = true,
                Location = new Point(55, 27),
                Cursor = Cursors.Hand
            };

            item.Controls.Add(numLabel);
            item.Controls.Add(nameLabel);
            item.Controls.Add(catLabel);

            // Click handlers
            void Select() => SelectItem(item, index);
            item.Click += (s, e) => Select();
            numLabel.Click += (s, e) => Select();
            nameLabel.Click += (s, e) => Select();
            catLabel.Click += (s, e) => Select();

            // Hover effects
            void OnEnter(object? s, EventArgs e) { if (_selectedItem != item) item.BackColor = Color.FromArgb(25, 25, 25); }
            void OnLeave(object? s, EventArgs e) { if (_selectedItem != item) item.BackColor = Color.FromArgb(15, 15, 15); }

            item.MouseEnter += OnEnter;
            item.MouseLeave += OnLeave;
            foreach (Control c in item.Controls)
            {
                c.MouseEnter += OnEnter;
                c.MouseLeave += OnLeave;
            }

            // Border
            item.Paint += (s, e) =>
            {
                var isSelected = _selectedItem == item;
                var color = isSelected ? Theme.Accent : Color.FromArgb(51, 51, 51);
                using var pen = new Pen(color, isSelected ? 2 : 1);
                e.Graphics.DrawRectangle(pen, 0, 0, item.Width - 1, item.Height - 1);
            };

            return item;
        }

        private void SelectItem(Panel item, int index)
        {
            // Deselect previous
            if (_selectedItem != null)
            {
                _selectedItem.BackColor = Color.FromArgb(15, 15, 15);
                _selectedItem.Invalidate();
            }

            // Select new
            _selectedItem = item;
            _selectedIndex = index;
            item.BackColor = Color.FromArgb(20, 30, 30);
            item.Invalidate();

            UpdateButtonStates();
        }

        private void MoveSelected(int direction)
        {
            if (_selectedIndex < 0 || _selectedIndex >= _priorities.Count)
                return;

            int newIndex = _selectedIndex + direction;
            if (newIndex < 0 || newIndex >= _priorities.Count)
                return;

            // Swap items
            var temp = _priorities[_selectedIndex];
            _priorities[_selectedIndex] = _priorities[newIndex];
            _priorities[newIndex] = temp;

            // Update priority values
            for (int i = 0; i < _priorities.Count; i++)
            {
                _priorities[i].Priority = (i + 1) * 10;
            }

            _selectedIndex = newIndex;
            _hasChanges = true;
            RefreshList();

            // Reselect the moved item
            if (_listContainer.Controls.Count > newIndex)
            {
                var newItem = _listContainer.Controls[newIndex] as Panel;
                if (newItem != null)
                {
                    SelectItem(newItem, newIndex);
                }
            }

            UpdateButtonStates();
            SetStatus("Changes pending...", Color.FromArgb(255, 200, 100));
        }

        private async Task SavePrioritiesAsync()
        {
            if (_priorityService == null || string.IsNullOrEmpty(_targetPath))
            {
                SetStatus("No service configured", Color.FromArgb(255, 100, 100));
                return;
            }

            try
            {
                var config = await _priorityService.LoadConfigAsync(_targetPath);

                foreach (var priority in _priorities)
                {
                    config.SetPriority(priority.ModId, priority.ModName, priority.Category, priority.Priority);
                }

                await _priorityService.SaveConfigAsync(config, _targetPath);

                _hasChanges = false;
                UpdateButtonStates();
                SetStatus("Priorities saved!", Color.FromArgb(100, 255, 100));

                PrioritiesSaved?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                SetStatus($"Error: {ex.Message}", Color.FromArgb(255, 100, 100));
            }
        }

        private void UpdateButtonStates()
        {
            _btnMoveUp.Enabled = _selectedIndex > 0;
            _btnMoveDown.Enabled = _selectedIndex >= 0 && _selectedIndex < _priorities.Count - 1;
            _btnSave.Enabled = _hasChanges;

            // Update button colors based on enabled state
            _btnMoveUp.BackColor = _btnMoveUp.Enabled ? Color.FromArgb(26, 26, 26) : Color.FromArgb(15, 15, 15);
            _btnMoveDown.BackColor = _btnMoveDown.Enabled ? Color.FromArgb(26, 26, 26) : Color.FromArgb(15, 15, 15);
            _btnSave.BackColor = _btnSave.Enabled ? Color.White : Color.FromArgb(51, 51, 51);
            _btnSave.ForeColor = _btnSave.Enabled ? Color.Black : Color.FromArgb(100, 100, 100);
        }

        private void SetStatus(string message, Color? color = null)
        {
            _statusLabel.Text = message;
            _statusLabel.ForeColor = color ?? Color.FromArgb(136, 136, 136);
        }
    }
}
