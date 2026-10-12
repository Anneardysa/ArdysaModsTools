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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services.Security;
using ArdysaModsTools.Helpers;

namespace ArdysaModsTools.Core.Services.Hero
{
    public sealed class SkinManifestReader : ISkinManifestReader
    {
        private const int ExtractTimeoutSeconds = 45;

        private const string CacheKeyLabel = "amt/skinselector/state/v1";

        private static readonly byte[] CacheMagic = { 0x41, 0x4D, 0x53, 0x31 };
        private const byte CacheVersion = 1;
        private const int NonceLen = 12;
        private const int TagLen = 16;

        private readonly IAppLogger? _logger;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly string _cachePath;

        public SkinManifestReader(IAppLogger? logger = null, string? cachePath = null)
        {
            _logger = logger;
            _cachePath = cachePath ?? DefaultCachePath;
        }

        public async Task<SkinManifest?> ReadInstalledAsync(string targetPath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(targetPath)) return null;

            string vpkPath = Path.Combine(
                PathUtility.NormalizeTargetPath(targetPath),
                DotaPaths.ModsVpk.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(vpkPath)) return null;

            var stamp = Stamp.Of(vpkPath);
            if (stamp is null) return null;

            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var cached = ReadCache();
                if (cached != null && cached.Value.stamp.Matches(stamp.Value))
                    return cached.Value.manifest;

                string? text = await ExtractManifestFileAsync(vpkPath, ct).ConfigureAwait(false);
                var manifest = SkinManifestStore.Parse(text);

                WriteCache(stamp.Value, manifest);
                return manifest;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"SkinManifestReader: read failed - {ex.Message}");
                return null;
            }
            finally
            {
                _gate.Release();
            }
        }

        #region Package read

        private async Task<string?> ExtractManifestFileAsync(string vpkPath, CancellationToken ct)
        {
            string hlExtractPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HLExtract.exe");
            if (!File.Exists(hlExtractPath))
            {
                _logger?.LogDebug($"SkinManifestReader: HLExtract.exe missing at {hlExtractPath}");
                return null;
            }

            string folder = SkinManifestStore.TreeRelativePath.Substring(
                0, SkinManifestStore.TreeRelativePath.IndexOf('/'));
            string fileName = Path.GetFileName(SkinManifestStore.TreeRelativePath);

            string tempDir = Path.Combine(SafeTempPathHelper.GetSafeTempPath(), $"ArdysaSkinManifest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                foreach (var entry in new[] { "root/" + folder, folder })
                {
                    ct.ThrowIfCancellationRequested();

                    string arguments = $"-p \"{vpkPath}\" -d \"{tempDir}\" -e \"{entry}\"";
                    if (!await RunHlExtractAsync(hlExtractPath, arguments, ct).ConfigureAwait(false))
                        continue;

                    var found = Directory.EnumerateFiles(tempDir, fileName, SearchOption.AllDirectories).FirstOrDefault();
                    if (found != null)
                        return await File.ReadAllTextAsync(found, ct).ConfigureAwait(false);
                }

                return null;
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch {  }
            }
        }

        private async Task<bool> RunHlExtractAsync(string hlExtractPath, string arguments, CancellationToken ct)
        {
            var psi = new ProcessStartInfo
            {
                FileName = hlExtractPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            proc.Exited += (s, e) => tcs.TrySetResult(proc.ExitCode);

            proc.Start();

            _ = proc.StandardOutput.ReadToEndAsync();
            _ = proc.StandardError.ReadToEndAsync();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(ExtractTimeoutSeconds));

            using (ct.Register(() => { try { if (!proc.HasExited) proc.Kill(); } catch { } }))
            {
                var completed = await Task.WhenAny(
                    tcs.Task, Task.Delay(Timeout.Infinite, timeoutCts.Token)).ConfigureAwait(false);

                if (completed != tcs.Task)
                {
                    try { if (!proc.HasExited) proc.Kill(); } catch { }
                    ct.ThrowIfCancellationRequested();
                    _logger?.LogDebug($"SkinManifestReader: HLExtract timed out after {ExtractTimeoutSeconds} seconds.");
                    return false;
                }
            }

            return await tcs.Task.ConfigureAwait(false) == 0;
        }

        #endregion

        #region Encrypted sidecar cache

        private readonly struct Stamp
        {
            [JsonInclude, JsonPropertyName("len")] public readonly long Length;
            [JsonInclude, JsonPropertyName("mtime")] public readonly long WriteTimeUtcTicks;

            [JsonConstructor]
            public Stamp(long length, long writeTimeUtcTicks)
            {
                Length = length;
                WriteTimeUtcTicks = writeTimeUtcTicks;
            }

            public static Stamp? Of(string path)
            {
                try
                {
                    var info = new FileInfo(path);
                    return new Stamp(info.Length, info.LastWriteTimeUtc.Ticks);
                }
                catch { return null; }
            }

            public bool Matches(Stamp other) => Length == other.Length && WriteTimeUtcTicks == other.WriteTimeUtcTicks;
        }

        private sealed class CachePayload
        {
            [JsonPropertyName("stamp")] public Stamp Stamp { get; set; }

            [JsonPropertyName("manifest")] public SkinManifest? Manifest { get; set; }
        }

        private static string DefaultCachePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ArdysaModsTools", "data", "skinstate.amtx");

        private (Stamp stamp, SkinManifest? manifest)? ReadCache()
        {
            try
            {
                if (!File.Exists(_cachePath)) return null;

                byte[] blob = File.ReadAllBytes(_cachePath);
                int headerLen = CacheMagic.Length + 1 + NonceLen + TagLen;
                if (blob.Length < headerLen) return null;
                if (!blob.AsSpan(0, CacheMagic.Length).SequenceEqual(CacheMagic)) return null;
                if (blob[CacheMagic.Length] != CacheVersion) return null;

                var nonce = blob.AsSpan(CacheMagic.Length + 1, NonceLen);
                var tag = blob.AsSpan(CacheMagic.Length + 1 + NonceLen, TagLen);
                var cipher = blob.AsSpan(headerLen);
                var plain = new byte[cipher.Length];

                using (var aes = new AesGcm(EmbeddedAssetKey.DeriveKey(CacheKeyLabel), TagLen))
                    aes.Decrypt(nonce, cipher, tag, plain);

                var payload = JsonSerializer.Deserialize<CachePayload>(plain);
                return payload is null ? null : (payload.Stamp, payload.Manifest);
            }
            catch
            {
                return null;
            }
        }

        private void WriteCache(Stamp stamp, SkinManifest? manifest)
        {
            try
            {
                byte[] plain = JsonSerializer.SerializeToUtf8Bytes(new CachePayload { Stamp = stamp, Manifest = manifest });

                var nonce = RandomNumberGenerator.GetBytes(NonceLen);
                var tag = new byte[TagLen];
                var cipher = new byte[plain.Length];

                using (var aes = new AesGcm(EmbeddedAssetKey.DeriveKey(CacheKeyLabel), TagLen))
                    aes.Encrypt(nonce, plain, cipher, tag);

                Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);

                using var ms = new MemoryStream();
                ms.Write(CacheMagic);
                ms.WriteByte(CacheVersion);
                ms.Write(nonce);
                ms.Write(tag);
                ms.Write(cipher);

                string tmp = _cachePath + ".tmp";
                File.WriteAllBytes(tmp, ms.ToArray());
                File.Move(tmp, _cachePath, overwrite: true);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"SkinManifestReader: could not write sidecar - {ex.Message}");
            }
        }

        #endregion
    }
}
