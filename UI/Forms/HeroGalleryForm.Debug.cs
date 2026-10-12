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
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArdysaModsTools.UI.Forms
{
    public partial class HeroGalleryForm
    {
        private const string DebugHeroesJson = @"F:\Projects\ModsPack\Assets\heroes.json";
        private const string ModsPackOutputRoot = @"F:\Projects\ModsPack\output\modspack";
        private static readonly JsonSerializerOptions DebugJson = new(JsonSerializerDefaults.Web);

        private Task EnableDebugCardAsync()
            => ExecuteScriptAsync($"setDebugMode(true, {JsonSerializer.Serialize(DebugHeroesJson)})");

        private bool HandleDebugMessage(string? type, JsonElement message)
        {
            switch (type)
            {
                case "debugPickZips":
                    Defer(async () =>
                    {
                        using var ofd = new OpenFileDialog { Title = "Add set zips", Filter = "Set zip (*.zip)|*.zip", Multiselect = true };
                        if (ofd.ShowDialog(this) == DialogResult.OK)
                            await ExecuteScriptAsync($"debugZipsPicked({JsonSerializer.Serialize(ofd.FileNames)})");
                    });
                    return true;

                case "debugPickHeroesJson":
                    Defer(async () =>
                    {
                        using var ofd = new OpenFileDialog { Title = "heroes.json", Filter = "heroes.json|heroes.json|JSON (*.json)|*.json" };
                        if (ofd.ShowDialog(this) == DialogResult.OK)
                            await ExecuteScriptAsync($"setDebugHeroesJson({JsonSerializer.Serialize(ofd.FileName)})");
                    });
                    return true;

                case "debugPlan":
                {
                    var (zips, heroesJson) = ReadDebugRequest(message);
                    int seq = message.TryGetProperty("seq", out var s) && s.TryGetInt32(out var n) ? n : 0;
                    Defer(async () =>
                    {
                        object answer;
                        try
                        {
                            var plan = await _presenter.PlanDebugBuildAsync(zips, heroesJson);
                            answer = new { seq, rows = plan.Rows, errors = plan.Errors, errorsByPath = plan.ErrorsByPath, warnings = plan.Warnings };
                        }
                        catch (Exception ex)
                        {
                            answer = new { seq, rows = Array.Empty<object>(), errors = new[] { $"Plan failed: {ex.Message}" } };
                        }
                        await ExecuteScriptAsync($"setDebugPlan({JsonSerializer.Serialize(answer, DebugJson)})");
                    });
                    return true;
                }

                case "debugBuild":
                {
                    var (zips, heroesJson) = ReadDebugRequest(message);
                    Defer(() => _presenter.DebugBuildAsync(zips, heroesJson));
                    return true;
                }

                case "debugGenerateModsPack":
                    if (message.TryGetProperty("selections", out var selectionsEl))
                        _selections = ParseSelections(selectionsEl);
                    Defer(() => _presenter.GenerateModsPackAsync(_heroes, _selections, ModsPackOutputRoot));
                    return true;
            }
            return false;
        }

        private static (IReadOnlyList<string> zips, string heroesJson) ReadDebugRequest(JsonElement message)
        {
            var zips = message.TryGetProperty("zips", out var z) && z.ValueKind == JsonValueKind.Array
                ? z.EnumerateArray().Select(e => e.GetString() ?? "").Where(p => p.Length > 0).ToList()
                : new List<string>();
            var heroesJson = message.TryGetProperty("heroesJson", out var h) ? h.GetString() ?? "" : "";
            return (zips, heroesJson);
        }

        private void Defer(Func<Task> work) => BeginInvoke(new Action(async () =>
        {
            try { await work(); }
            catch (Exception ex) { await ShowAlertAsync("Debug", ex.Message); }
        }));
    }
}
#endif
