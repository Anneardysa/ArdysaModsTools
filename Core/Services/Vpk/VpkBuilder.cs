using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ArdysaModsTools.Core.Services
{
    public sealed class VpkBuilder
    {
        /// <summary>
        /// Runs vpk.exe to build a VPK from a folder.
        /// vpkExePath: full path to vpk.exe (if null or empty, tries "vpk.exe" on PATH)
        /// sourceFolder: folder containing "root" folder expected by vpk (or the files directly)
        /// outputFolder and outputFileName determine where pak is written.
        /// Returns full path to created vpk on success.
        /// </summary>
        public static async Task<string> BuildVpkAsync(string sourceFolder, string outputFolder, string outputFileName = "pak01_dir.vpk", string? vpkExePath = null, IProgress<string>? log = null, CancellationToken ct = default)
        {
            if (!Directory.Exists(sourceFolder)) throw new DirectoryNotFoundException(sourceFolder);
            Directory.CreateDirectory(outputFolder);

            var exe = string.IsNullOrWhiteSpace(vpkExePath) ? "vpk.exe" : vpkExePath;
            var destPath = Path.Combine(outputFolder, outputFileName);

            // example arguments: a outputFolder\pak01_dir.vpk <sourceFolder>\*
            // many vpk builds prefer to run from folder where 'vpk.exe a foldername' works.
            // We'll pass args that add everything inside sourceFolder into destPath.
            var args = $"a \"{destPath}\" \"{sourceFolder}\"";

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = sourceFolder
            };

            var tcs = new TaskCompletionSource<int>();
            using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            p.Exited += (s, e) => tcs.TrySetResult(p.ExitCode);

            log?.Report($"[VpkBuilder] Running: {psi.FileName} {psi.Arguments}");
            try
            {
                p.Start();

                // read outputs asynchronously
                _ = Task.Run(async () =>
                {
                    while (!p.HasExited)
                    {
                        var line = await p.StandardOutput.ReadLineAsync();
                        if (line != null) log?.Report("[vpk] " + line);
                    }
                });

                _ = Task.Run(async () =>
                {
                    while (!p.HasExited)
                    {
                        var err = await p.StandardError.ReadLineAsync();
                        if (err != null) log?.Report("[vpk-err] " + err);
                    }
                });

                using (ct.Register(() =>
                {
                    try { if (!p.HasExited) p.Kill(); } catch { }
                }))
                {
                    var exit = await tcs.Task.ConfigureAwait(false);
                    if (exit != 0)
                    {
                        throw new InvalidOperationException($"vpk.exe exited with code {exit}");
                    }
                }

                if (!File.Exists(destPath))
                    throw new FileNotFoundException("Expected VPK not generated", destPath);

                log?.Report($"[VpkBuilder] Created VPK: {destPath}");
                return destPath;
            }
            catch (Exception ex)
            {
                log?.Report("[VpkBuilder] Error: " + ex.Message);
                throw;
            }
        }
    }
}
