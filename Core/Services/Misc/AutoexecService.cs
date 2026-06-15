using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Interfaces;

namespace ArdysaModsTools.Core.Services.Misc
{
    public class AutoexecService : IAutoexecService
    {
        private readonly IFileTransactionFactory _fileTransactionFactory;
        private readonly IAppLogger _logService;

        public AutoexecService(IFileTransactionFactory fileTransactionFactory, IAppLogger logService)
        {
            _fileTransactionFactory = fileTransactionFactory ?? throw new ArgumentNullException(nameof(fileTransactionFactory));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        // The app's target path is the install ROOT ("...\dota 2 beta", the folder containing "game" —
        // see DotaPaths, whose entries are all "game/..."). Dota 2 reads autoexec.cfg from
        // "<root>\game\dota\cfg". We also tolerate callers that pass the "game" or "dota" folder directly.
        // Candidate cfg dirs, ordered from most- to least-qualified:
        private static string[] CfgDirCandidates(string gamePath) => new[]
        {
            Path.Combine(gamePath, "game", "dota", "cfg"), // gamePath = install root  (the real convention)
            Path.Combine(gamePath, "dota", "cfg"),          // gamePath = "...\game" folder
            Path.Combine(gamePath, "cfg"),                  // gamePath = "...\dota" content folder
        };

        // Content folder that must exist for a candidate cfg dir to be a legitimate write target
        // (so we never create a stray "cfg" folder inside an unrelated directory).
        private static (string Parent, string Cfg)[] CfgWriteTargets(string gamePath) => new[]
        {
            (Path.Combine(gamePath, "game", "dota"), Path.Combine(gamePath, "game", "dota", "cfg")),
            (Path.Combine(gamePath, "dota"),          Path.Combine(gamePath, "dota", "cfg")),
        };

        private static string DefaultCfgDota() => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Steam", "steamapps", "common", "dota 2 beta", "game", "dota");

        private string GetCfgDirectory(string? gamePath)
        {
            if (!string.IsNullOrEmpty(gamePath))
            {
                foreach (var candidate in CfgDirCandidates(gamePath))
                    if (Directory.Exists(candidate))
                        return candidate;

                // An explicit gamePath was supplied but contains no cfg folder. Do NOT fall back
                // to the global default install — that would silently read/write an unrelated install.
                return string.Empty;
            }

            var defaultPath = Path.Combine(DefaultCfgDota(), "cfg");
            return Directory.Exists(defaultPath) ? defaultPath : string.Empty;
        }

        /// <summary>
        /// Resolves the cfg directory to <b>write</b> autoexec.cfg into. Unlike <see cref="GetCfgDirectory"/>
        /// (used for reads, which requires the folder to already exist), this returns the correct target
        /// even when the <c>cfg</c> folder does not exist yet — provided the Dota 2 install itself is valid.
        /// A fresh Dota 2 install has no <c>game/dota/cfg</c> folder until a config is created; the
        /// transactional <see cref="ArdysaModsTools.Core.Services.FileTransactions.WriteTextOperation"/>
        /// creates the parent folder on write. Returns empty only when no valid install can be located
        /// (e.g. an explicit path that is not a Dota 2 layout), so the caller surfaces a clear "not found".
        /// </summary>
        private string ResolveCfgDirectoryForWrite(string? gamePath)
        {
            // Prefer an already-existing cfg folder (handles standard and non-standard layouts).
            var existing = GetCfgDirectory(gamePath);
            if (!string.IsNullOrEmpty(existing))
                return existing;

            if (!string.IsNullOrEmpty(gamePath))
            {
                // Pick the target whose content folder exists, so we only ever create "cfg" inside a
                // real Dota 2 layout (install root → game/dota/cfg, or game folder → dota/cfg).
                foreach (var (parent, cfg) in CfgWriteTargets(gamePath))
                    if (Directory.Exists(parent))
                        return cfg;

                // gamePath is itself the "...\dota" content folder → dota\cfg.
                var leaf = Path.GetFileName(gamePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (Directory.Exists(gamePath) && string.Equals(leaf, "dota", StringComparison.OrdinalIgnoreCase))
                    return Path.Combine(gamePath, "cfg");

                // Explicit path supplied but it is not a recognizable Dota 2 install — do not guess.
                return string.Empty;
            }

            // No explicit path: use the default Steam install when its content folder exists.
            var defaultDota = DefaultCfgDota();
            return Directory.Exists(defaultDota) ? Path.Combine(defaultDota, "cfg") : string.Empty;
        }

        public async Task<Dictionary<string, string>> LoadCurrentSettingsAsync(string? gamePath, CancellationToken cancellationToken = default)
        {
            var cfgDir = GetCfgDirectory(gamePath);
            if (string.IsNullOrEmpty(cfgDir))
            {
                _logService.LogWarning("[AutoexecService] Dota 2 cfg folder not found. Cannot load autoexec.cfg.");
                return new Dictionary<string, string>();
            }

            var autoexecPath = Path.Combine(cfgDir, "autoexec.cfg");
            if (!File.Exists(autoexecPath))
            {
                _logService.Log("[AutoexecService] No autoexec.cfg found. Returning empty settings.");
                return new Dictionary<string, string>();
            }

            try
            {
                var lines = await File.ReadAllLinesAsync(autoexecPath, cancellationToken);
                return ParseAutoexec(lines);
            }
            catch (Exception ex)
            {
                _logService.LogError($"[AutoexecService] Failed to read autoexec.cfg at {autoexecPath}: {ex.Message}", ex);
                throw;
            }
        }

        // [AMT:OPUS] File Transaction Boundary — This logic writes directly to the Dota 2 installation folder.
        // It strictly requires a FileTransaction wrap to prevent corruption on failure. Do not bypass this pipeline.
        public async Task ApplySettingsAsync(string? gamePath, Dictionary<string, string> settings, CancellationToken cancellationToken = default)
        {
            var cfgDir = ResolveCfgDirectoryForWrite(gamePath);
            if (string.IsNullOrEmpty(cfgDir))
            {
                throw new DirectoryNotFoundException("Dota 2 cfg folder not found. Cannot apply settings.");
            }

            var autoexecPath = Path.Combine(cfgDir, "autoexec.cfg");
            var content = GenerateAutoexecContent(settings);

            using var transaction = _fileTransactionFactory.CreateTransaction("ApplyAutoexec");
            try
            {
                _logService.Log($"[AutoexecService] Applying autoexec.cfg via transaction to {autoexecPath}");
                
                transaction.AddOperation(new ArdysaModsTools.Core.Services.FileTransactions.WriteTextOperation(autoexecPath, content, Encoding.UTF8));
                
                await transaction.ExecuteAsync(cancellationToken);
                transaction.Commit();
                
                _logService.Log("[AutoexecService] Successfully applied autoexec.cfg.");
            }
            catch (Exception ex)
            {
                // IFileTransaction.ExecuteAsync already rolls back internally on failure, so a manual
                // RollbackAsync here would run the rollback a second time. Just log and surface the error.
                _logService.LogError($"[AutoexecService] Transaction failed when applying autoexec.cfg; it was rolled back automatically. {ex.Message}", ex);
                throw;
            }
        }

        public async Task ExportCfgAsync(string exportPath, Dictionary<string, string> settings, CancellationToken cancellationToken = default)
        {
            try
            {
                var content = GenerateAutoexecContent(settings);
                await File.WriteAllTextAsync(exportPath, content, Encoding.UTF8, cancellationToken);
                _logService.Log($"[AutoexecService] Exported autoexec.cfg to {exportPath}");
            }
            catch (Exception ex)
            {
                _logService.LogError($"[AutoexecService] Failed to export autoexec.cfg to {exportPath}: {ex.Message}", ex);
                throw;
            }
        }

        internal static Dictionary<string, string> ParseAutoexec(string[] lines)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();

                if (string.IsNullOrEmpty(line) || line.StartsWith("//") || line.StartsWith("alias"))
                    continue;

                var commentIdx = line.IndexOf("//");
                var cleanLine = commentIdx >= 0 ? line.Substring(0, commentIdx).Trim() : line;

                if (string.IsNullOrEmpty(cleanLine))
                    continue;

                var parts = cleanLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    var cvar = parts[0];
                    var value = parts[1];

                    if (char.IsLetter(cvar[0]) || cvar[0] == '_')
                    {
                        result[cvar] = value;
                    }
                }
            }

            return result;
        }

        internal static string GenerateAutoexecContent(Dictionary<string, string> settings)
        {
            var caseInsensitiveSettings = new Dictionary<string, string>(settings, StringComparer.OrdinalIgnoreCase);

            var sb = new StringBuilder();
            sb.AppendLine("// DOTA 2 AUTOEXEC.CFG — Generated by ArdysaModsTools Performance Tweak");
            sb.AppendLine($"// Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("// Save as: Steam\\steamapps\\common\\dota 2 beta\\game\\dota\\cfg\\autoexec.cfg");
            sb.AppendLine();

            var categories = new (string Header, string[] Cvars)[]
            {
                ("DISPLAY & FPS", new[] { "fps_max", "fps_max_ui", "mat_viewportscale", "r_fullscreen_gamma" }),
                ("VISUAL TOGGLES", new[] {
                    "dota_portrait_animate", "r_deferred_additive_pass", "r_deferred_simple_light", "r_ssao",
                    "r_dota_normal_maps", "r_dota_allow_parallax_mapping", "dota_ambient_creatures", "dota_ambient_cloth",
                    "r_grass_quality", "r_dota_fxaa", "r_deferred_specular", "r_deferred_specular_bloom",
                    "dota_cheap_water", "r_deferred_height_fog", "r_dashboard_render_quality",
                    "r_dota_allow_wind_on_trees", "r_dota_bloom_compute_shader"
                }),
                ("QUALITY", new[] { "r_texture_stream_mip_bias", "cl_particle_fallback_base", "cl_globallight_shadow_mode", "r_texturefilteringquality" }),
                ("ENGINE TWEAKS", new[] {
                    "cl_particle_fallback_multiplier", "cl_particle_sim_fallback_threshold_ms",
                    "dota_allow_clientside_particles", "dota_disable_particle_lights",
                    "lb_shadow_texture_width_override", "lb_shadow_texture_height_override",
                    "r_dota_spotlight_shadows_resolution", "r_particle_max_detail_level",
                    "r_dota_color_correction", "r_dota_render_2d_skybox", "engine_no_focus_sleep"
                }),
                ("VSYNC & LATENCY", new[] { "engine_low_latency_sleep_after_client_tick", "r_experimental_lag_limiter", "r_low_latency" }),
                ("NETWORK", new[] { "rate", "cl_updaterate", "cl_interp_ratio", "cl_smooth" }),
            };

            var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (header, cvars) in categories)
            {
                sb.AppendLine($"// ── {header} ──");
                foreach (var cvar in cvars)
                {
                    if (caseInsensitiveSettings.TryGetValue(cvar, out var value))
                    {
                        sb.AppendLine($"{cvar} {value}");
                        written.Add(cvar);
                    }
                }
                sb.AppendLine();
            }

            var remaining = caseInsensitiveSettings.Where(kv => !written.Contains(kv.Key)).ToList();
            if (remaining.Count > 0)
            {
                sb.AppendLine("// ── OTHER ──");
                foreach (var kv in remaining)
                {
                    sb.AppendLine($"{kv.Key.ToLowerInvariant()} {kv.Value}");
                }
                sb.AppendLine();
            }

            sb.AppendLine("// End of ArdysaModsTools autoexec.cfg");
            return sb.ToString();
        }
    }
}
