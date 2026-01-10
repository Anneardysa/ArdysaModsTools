using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Core.Services; // assuming ConfigService lives here

namespace ArdysaModsTools.Core.Services
{
    public static class UserSettingsService
    {
        private static string ResolveDotaPath()
        {
            try
            {
                var configService = new ConfigService();
                string? savedPath = configService.GetLastTargetPath();

                if (!string.IsNullOrEmpty(savedPath) && Directory.Exists(savedPath))
                    return savedPath;

                // fallback — look for game folder in exe directory
                string fallback = AppDomain.CurrentDomain.BaseDirectory;
                if (Directory.Exists(Path.Combine(fallback, "game")))
                    return fallback;

                return fallback;
            }
            catch
            {
                return AppDomain.CurrentDomain.BaseDirectory;
            }
        }

        private static string JsonPath
        {
            get
            {
                string dotaPath = ResolveDotaPath();
                string tempDir = Path.Combine(dotaPath, "game", "_ArdysaMods", "_temp");
                return Path.Combine(tempDir, "vuex.json");
            }
        }

        // ============================================================
        // SAVE SELECTIONS
        // ============================================================
        public static async Task SaveSelectionsAsync(Form form)
        {
            try
            {
                var selections = form.Controls.OfType<ComboBox>()
                    .Where(c => c.Name.EndsWith("Box", StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(
                        c => c.Name.Replace("Box", "", StringComparison.OrdinalIgnoreCase),
                        c => c.SelectedItem?.ToString() ?? "",
                        StringComparer.OrdinalIgnoreCase);

                var dir = Path.GetDirectoryName(JsonPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(selections, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(JsonPath, json);

                Console.WriteLine($"[INFO] Saved vuex.json to {JsonPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] Failed to save vuex.json: {ex.Message}");
            }
        }

        // ============================================================
        // RESTORE SELECTIONS
        // ============================================================
        public static async Task RestoreSelectionsAsync(Form form, Action<string>? logAction = null)
        {
            try
            {
                if (!File.Exists(JsonPath))
                {
                    logAction?.Invoke("No previous settings found.");
                    return;
                }

                var json = await File.ReadAllTextAsync(JsonPath);
                var saved = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

                if (saved == null || saved.Count == 0)
                {
                    logAction?.Invoke("vuex.json empty or unreadable.");
                    return;
                }

                foreach (var kv in saved)
                {
                    var box = form.Controls.OfType<ComboBox>()
                        .FirstOrDefault(c => c.Name.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase));

                    if (box != null && box.Items.Contains(kv.Value))
                        box.SelectedItem = kv.Value;
                }

                logAction?.Invoke("Restored previous settings successfully.");
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Failed to restore vuex.json: {ex.Message}");
            }
        }
    }
}
