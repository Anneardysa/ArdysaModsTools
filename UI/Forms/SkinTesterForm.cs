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
#if DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.UI.Helpers;
using ArdysaModsTools.UI.Presenters;

namespace ArdysaModsTools.UI.Forms
{
    public sealed class SkinTesterForm : Form, ISkinTesterView
    {
        private const string DefaultHeroesJson = @"F:\Projects\ModsPack\Assets\heroes.json";

        private readonly ListBox _zips = new() { Dock = DockStyle.Fill, AllowDrop = true, HorizontalScrollbar = true };
        private readonly TextBox _heroesJson = new() { Dock = DockStyle.Fill, Text = DefaultHeroesJson };
        private readonly Label _target = new() { Dock = DockStyle.Fill, AutoSize = false, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };
        private readonly TextBox _log = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new System.Drawing.Font("Consolas", 9f) };
        private readonly Button _add = new() { Text = "Add…", AutoSize = true };
        private readonly Button _remove = new() { Text = "Remove", AutoSize = true };
        private readonly Button _clear = new() { Text = "Clear", AutoSize = true };
        private readonly Button _build = new() { Text = "Build + install", AutoSize = true };
        private readonly Button _cancel = new() { Text = "Cancel", AutoSize = true, Enabled = false };
        private readonly CheckBox _live = new()
        {
            Text = "Live sandbox (Dota open) - push from live.html", AutoSize = true, Anchor = AnchorStyles.Left,
        };
        private readonly SkinTesterPresenter _presenter;

        public SkinTesterForm(IConfigService config)
        {
            Text = "Skin Tester (Debug) - local zips through the Skin Selector pipeline";
            Width = 1000;
            Height = 720;
            StartPosition = FormStartPosition.CenterScreen;

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            buttons.Controls.AddRange(new Control[] { _add, _remove, _clear, _build, _cancel, _live });
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5, Padding = new Padding(8) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 65));
            grid.Controls.Add(new Label { Text = "heroes.json", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            grid.Controls.Add(_heroesJson, 1, 0);
            grid.Controls.Add(new Label { Text = "Dota 2", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            grid.Controls.Add(_target, 1, 1);
            grid.Controls.Add(new Label { Text = "Zips (drop here)", AutoSize = true }, 0, 2);
            grid.Controls.Add(_zips, 1, 2);
            grid.Controls.Add(buttons, 1, 3);
            grid.Controls.Add(new Label { Text = "Log", AutoSize = true }, 0, 4);
            grid.Controls.Add(_log, 1, 4);
            Controls.Add(grid);

            _zips.DragEnter += (_, e) => e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
                ? DragDropEffects.Copy : DragDropEffects.None;
            _zips.DragDrop += (_, e) => AddZips(e.Data?.GetData(DataFormats.FileDrop) as string[]);
            _add.Click += (_, _) =>
            {
                using var dlg = new OpenFileDialog { Filter = "Set zip (*.zip)|*.zip", Multiselect = true };
                if (dlg.ShowDialog(this) == DialogResult.OK) AddZips(dlg.FileNames);
            };
            _remove.Click += (_, _) =>
            {
                foreach (var item in _zips.SelectedItems.Cast<object>().ToList()) _zips.Items.Remove(item);
            };
            _clear.Click += (_, _) => _zips.Items.Clear();
            _zips.SelectionMode = SelectionMode.MultiExtended;

            _presenter = new SkinTesterPresenter(this, config);
            _build.Click += (_, _) => { _log.Clear(); _presenter.OnBuildRequested(); };
            _cancel.Click += (_, _) => _presenter.Cancel();
            FormClosing += (_, _) => _presenter.Cancel();
            DpiLayout.AttachClamp(this);
        }

        private void AddZips(IEnumerable<string>? paths)
        {
            foreach (var p in paths ?? Enumerable.Empty<string>())
                if (p.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && !_zips.Items.Contains(p))
                    _zips.Items.Add(p);
        }

        public IReadOnlyList<string> ZipPaths => _zips.Items.Cast<string>().ToList();
        public string HeroesJsonPath => _heroesJson.Text.Trim();
        public bool LiveSandbox => _live.Checked;

        public void ShowTarget(string? targetPath) => _target.Text = string.IsNullOrWhiteSpace(targetPath)
            ? "(not set - run Detect in the app first)" : targetPath;

        public void AppendLog(string line)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(AppendLog), line);
                return;
            }
            _log.AppendText(line + Environment.NewLine);
        }

        public void SetBusy(bool busy)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<bool>(SetBusy), busy);
                return;
            }
            foreach (var b in new Control[] { _add, _remove, _clear, _build, _live }) b.Enabled = !busy;
            _heroesJson.Enabled = !busy;
            _zips.AllowDrop = !busy;
            _cancel.Enabled = busy;
        }
    }
}
#endif
