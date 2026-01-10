using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ArdysaModsTools.Helpers
{
    public static partial class FileHelper
    {
        /// <summary>
        /// Poll until the file can be opened for read/write without throwing an IOException.
        /// Returns true when file becomes available, false if timeout reached.
        /// </summary>
        public static bool WaitForFileReady(string path, TimeSpan timeout, TimeSpan pollInterval)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                try
                {
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        return true;
                    }
                }
                catch (IOException)
                {
                    Thread.Sleep(pollInterval);
                }
                catch (UnauthorizedAccessException)
                {
                    Thread.Sleep(pollInterval);
                }
            }
            return false;
        }

        /// <summary>
        /// Copies src file to a unique filename inside destFolder with retries.
        /// Returns destination path on success or throws on failure.
        /// </summary>
        public static string SafeCopyFileWithRetries(string src, string destFolder, int maxRetries = 6, int baseDelayMs = 150)
        {
            if (!File.Exists(src)) throw new FileNotFoundException("Source file not found", src);
            if (!Directory.Exists(destFolder)) Directory.CreateDirectory(destFolder);

            string dest;
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                dest = Path.Combine(destFolder, $"{Guid.NewGuid():N}_{Path.GetFileName(src)}");
                try
                {
                    // wait for source to be ready a little
                    WaitForFileReady(src, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(100));

                    using (var inFs = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var outFs = new FileStream(dest, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        inFs.CopyTo(outFs);
                    }

                    // check dest
                    if (WaitForFileReady(dest, TimeSpan.FromSeconds(3), TimeSpan.FromMilliseconds(50)))
                        return dest;
                }
                catch (IOException)
                {
                    Thread.Sleep(baseDelayMs * (attempt + 1));
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    Thread.Sleep(baseDelayMs * (attempt + 1));
                    continue;
                }
            }

            // Last resort - attempt overwrite copy (may throw)
            dest = Path.Combine(destFolder, $"{Guid.NewGuid():N}_{Path.GetFileName(src)}");
            File.Copy(src, dest, overwrite: true);
            return dest;
        }

        /// <summary>
        /// Async helper to run a process and capture stdout/stderr.
        /// Throws InvalidOperationException on non-zero exit with combined output.
        /// </summary>
        public static async Task<string> RunProcessCaptureAsync(string fileName, string args, string workingDir, CancellationToken ct)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var proc = new Process { StartInfo = psi })
            {
                var stdoutSb = new StringWriter();
                var stderrSb = new StringWriter();

                proc.Start();

                // async read
                var stdOutTask = proc.StandardOutput.ReadToEndAsync();
                var stdErrTask = proc.StandardError.ReadToEndAsync();

                // Wait with cancellation
                while (!proc.HasExited)
                {
                    await Task.Delay(100, ct).ConfigureAwait(false);
                    if (ct.IsCancellationRequested)
                    {
                        try { proc.Kill(true); } catch { }
                        ct.ThrowIfCancellationRequested();
                    }
                }

                var outText = await stdOutTask.ConfigureAwait(false);
                var errText = await stdErrTask.ConfigureAwait(false);

                var combined = $"STDOUT:\n{outText}\n\nSTDERR:\n{errText}";

                if (proc.ExitCode != 0)
                {
                    throw new InvalidOperationException($"Process exited with code {proc.ExitCode}\n{combined}");
                }

                return combined;
            }
        }
    }
}
