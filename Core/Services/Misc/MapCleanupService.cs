using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Services.Utilities;

namespace ArdysaModsTools.Core.Services.Misc
{
    public class MapCleanupService : IMapCleanupService
    {
        private const string StateFile = "map_cleanup.json";

        private readonly IVpkExtractor _extractor;
        private readonly IVpkRecompiler _recompiler;
        private readonly IAppLogger _logService;

        public MapCleanupService(IVpkExtractor extractor, IVpkRecompiler recompiler, IAppLogger logService)
        {
            _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
            _recompiler = recompiler ?? throw new ArgumentNullException(nameof(recompiler));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        private static readonly Lazy<byte[]> BlankModel = new(() => ReadResource("blank.vmdl_c"));
        private static readonly Lazy<byte[]> BlankMaterial = new(() => ReadResource("blank.vmat_c"));
        private static readonly Lazy<byte[]> BlankTexture = new(VtexGenerator.GenerateEmptyPipTexture);
        private static readonly Lazy<string[]> FoliagePaths = new(() => ReadList("remove_foliage.txt"));
        private static readonly Lazy<string[]> PropsPaths = new(() => ReadList("remove_map_props.txt"));
        private static readonly MapCleanupGroup[] AllGroups = { MapCleanupGroup.Foliage, MapCleanupGroup.Props };

        internal static IReadOnlyList<string> Paths(MapCleanupGroup group)
            => group == MapCleanupGroup.Foliage ? FoliagePaths.Value : PropsPaths.Value;

        internal static byte[] BlankFor(string path) => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".vmdl_c" => BlankModel.Value,
            ".vmat_c" => BlankMaterial.Value,
            ".vtex_c" => BlankTexture.Value,
            var ext => throw new NotSupportedException($"No blank resource for '{ext}' ({path})."),
        };


        private static string StatePath(string gamePath) => Path.Combine(gamePath, DotaPaths.TempFolder, StateFile);

        public static IReadOnlySet<MapCleanupGroup> ReadEnabled(string? gamePath)
        {
            try
            {
                if (string.IsNullOrEmpty(gamePath) || !File.Exists(StatePath(gamePath))) return new HashSet<MapCleanupGroup>();
                var names = JsonSerializer.Deserialize<string[]>(File.ReadAllText(StatePath(gamePath))) ?? Array.Empty<string>();
                var enabled = new HashSet<MapCleanupGroup>();
                foreach (var n in names)
                    if (Enum.TryParse<MapCleanupGroup>(n, out var g)) enabled.Add(g);
                return enabled;
            }
            catch (Exception ex)
            {
                FallbackLogger.LogFileOnly($"MapCleanupService: unreadable state, treating as off: {ex.Message}");
                return new HashSet<MapCleanupGroup>();
            }
        }

        internal static void WriteEnabled(string gamePath, IEnumerable<MapCleanupGroup> enabled)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StatePath(gamePath))!);
            File.WriteAllText(StatePath(gamePath), JsonSerializer.Serialize(enabled.Select(g => g.ToString()).OrderBy(n => n).ToArray()));
        }

        public static int ApplyTo(string packageDir, IReadOnlySet<MapCleanupGroup> enabled)
        {
            int written = 0;
            foreach (var group in AllGroups)
            {
                foreach (var rel in Paths(group))
                {
                    var path = Path.Combine(packageDir, rel.Replace('/', Path.DirectorySeparatorChar));
                    if (enabled.Contains(group))
                    {
                        if (File.Exists(path) && !IsOurs(path)) continue;
                        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                        File.WriteAllBytes(path, BlankFor(rel));
                        written++;
                    }
                    else if (IsOurs(path))
                    {
                        File.Delete(path);
                    }
                }
            }
            return written;
        }


        public Task<MapCleanupStatus> GetStatusAsync(string? gamePath, CancellationToken cancellationToken = default)
            => Task.Run(() => GetStatus(gamePath), cancellationToken);

        private static MapCleanupStatus GetStatus(string? gamePath)
        {
            if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(Path.Combine(gamePath, "game", "dota")))
                return new MapCleanupStatus(false, false, false);

            var gameInfo = Path.Combine(gamePath, DotaPaths.GameInfo);
            bool mounted = File.Exists(gameInfo) && ProtectedVpkStore.IsMountedBy(File.ReadAllText(gameInfo));

            var vpk = ProtectedVpkStore.VpkPath(gamePath);
            var entries = File.Exists(vpk) ? RankChangerService.ReadVpkEntries(vpk) : new HashSet<string>();
            return new MapCleanupStatus(
                Paths(MapCleanupGroup.Foliage).All(entries.Contains),
                Paths(MapCleanupGroup.Props).All(entries.Contains),
                mounted);
        }

        public async Task<bool> SetEnabledAsync(string? gamePath, IReadOnlySet<MapCleanupGroup> enabled, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(gamePath) || !Path.IsPathFullyQualified(gamePath)
                || !Directory.Exists(Path.Combine(gamePath, "game", "dota")))
                throw new DirectoryNotFoundException("Dota 2 install not found. Cannot change map cleanup.");

            var current = await GetStatusAsync(gamePath, ct).ConfigureAwait(false);
            if (current.Foliage == enabled.Contains(MapCleanupGroup.Foliage) && current.Props == enabled.Contains(MapCleanupGroup.Props))
            {
                WriteEnabled(gamePath, enabled);
                return false;
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string hlExtractPath = Path.Combine(baseDir, "HLExtract.exe");
            string vpkToolPath = Path.Combine(baseDir, "vpk.exe");
            string tempRoot = Path.Combine(SafeTempPathHelper.GetSafeTempPath(), $"ArdysaMapCleanup_{Guid.NewGuid():N}");
            string packageDir = Path.Combine(tempRoot, "pak01_dir");
            string buildDir = Path.Combine(tempRoot, "build");

            try
            {
                Directory.CreateDirectory(packageDir);
                Directory.CreateDirectory(buildDir);

                string protVpk = ProtectedVpkStore.VpkPath(gamePath);
                if (File.Exists(protVpk) && !await _extractor.ExtractAsync(hlExtractPath, protVpk, packageDir,
                        line => _logService.LogDebug($"[MapCleanup] {line}"), ct, null, requireItemsGame: false).ConfigureAwait(false))
                    throw new InvalidOperationException("Could not unpack the installed mod package.");

                ct.ThrowIfCancellationRequested();
                await Task.Run(() => ApplyTo(packageDir, enabled), ct).ConfigureAwait(false);

                string? newVpk = null;
                if (Directory.EnumerateFiles(packageDir, "*", SearchOption.AllDirectories).Any())
                {
                    newVpk = await _recompiler.RecompileAsync(vpkToolPath, packageDir, buildDir, tempRoot,
                        line => _logService.LogDebug($"[MapCleanup] {line}"), ct).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(newVpk))
                        throw new InvalidOperationException("Building the mod package failed.");
                    VpkSignatureSection.TryApply(newVpk);
                }

                ct.ThrowIfCancellationRequested();
                if (!await ProtectedVpkStore.DeployAsync(gamePath, newVpk, line => _logService.Log($"[MapCleanup] {line}"), ct, _logService)
                        .ConfigureAwait(false))
                    throw new IOException("Installing the mod package failed — close Dota 2 and try again.");

                WriteEnabled(gamePath, enabled);
                _logService.Log($"Map cleanup: package rebuilt with [{string.Join(", ", enabled)}].");
                return true;
            }
            finally
            {
                try { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); }
                catch (Exception ex) { _logService.LogDebug($"[MapCleanup] temp cleanup failed: {ex.Message}"); }
            }
        }

        private static bool IsOurs(string path)
        {
            if (!File.Exists(path)) return false;
            var blank = BlankFor(path);
            return new FileInfo(path).Length == blank.Length && File.ReadAllBytes(path).AsSpan().SequenceEqual(blank);
        }

        private static byte[] ReadResource(string name)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ArdysaModsTools.Assets.Performance." + name)
                ?? throw new InvalidOperationException($"Embedded resource '{name}' is missing.");
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        private static string[] ReadList(string name)
            => System.Text.Encoding.UTF8.GetString(ReadResource(name))
                .Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith('#'))
                .ToArray();
    }
}
