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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Helpers;

namespace ArdysaModsTools.Core.Services
{
    public static class SkinTesterSandbox
    {
        public const string FolderName = "_amt_skin_sandbox";

        private const string GameDota = "game/dota";
        private const string VpkPattern = "pak01_*.vpk";
        private const int ErrorNotSameDevice = 17;

        private static readonly (string Path, bool Required)[] Copied =
        {
            (DotaPaths.GameInfo, true),
            (DotaPaths.Signatures, true),
            ("game/dota/gameinfo.gi", false),
            (DotaPaths.SteamInf, false),
        };

        public static string Root(string realRoot)
        {
            string real = Full(realRoot);
            return Path.Combine(Path.GetDirectoryName(real) ?? real, FolderName);
        }

        public static string? Prepare(string realRoot, string sandboxRoot, Func<string, string, int>? link = null)
        {
            link ??= CreateHardLink;
            string real = Full(realRoot), sandbox = Full(sandboxRoot);

            if (string.Equals(real, sandbox, StringComparison.OrdinalIgnoreCase))
                return "the sandbox is the Dota folder itself";
            if (IsInside(sandbox, real) || IsInside(real, sandbox))
                return "the sandbox and the Dota folder must not contain each other";
            string leaf = Path.GetFileName(sandbox);
            if (leaf.EndsWith("game", StringComparison.OrdinalIgnoreCase) ||
                leaf.EndsWith("_ArdysaMods", StringComparison.OrdinalIgnoreCase))
                return $"the sandbox folder must not be named like a Dota folder ({leaf})";

            string realDota = Combine(real, GameDota);
            if (!File.Exists(Combine(real, DotaPaths.GameVpk)))
                return $"no Dota install at {real} (game/dota/pak01_dir.vpk missing)";
            foreach (var (path, required) in Copied)
                if (required && !File.Exists(Combine(real, path)))
                    return $"{path} is missing in the Dota install";

            try
            {
                Directory.CreateDirectory(Combine(sandbox, DotaPaths.ModsFolder));
                string sandboxDota = Combine(sandbox, GameDota);
                Directory.CreateDirectory(sandboxDota);

                var vanilla = Directory.GetFiles(realDota, VpkPattern)
                    .ToDictionary(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase);
                foreach (string old in Directory.GetFiles(sandboxDota, VpkPattern))
                {
                    if (vanilla.TryGetValue(Path.GetFileName(old), out string? src) && SameStamp(old, src))
                        continue;
                    File.Delete(old);
                }
                foreach (var (name, src) in vanilla)
                {
                    string dst = Path.Combine(sandboxDota, name);
                    if (File.Exists(dst))
                        continue;
                    int error = link(dst, src);
                    if (error == ErrorNotSameDevice)
                        return "the sandbox is on another drive than Dota - a hardlink needs the same drive";
                    if (error != 0)
                        return $"could not link {name} into the sandbox (Win32 error {error})";
                }

                foreach (var (path, _) in Copied)
                {
                    string src = Combine(real, path);
                    if (!File.Exists(src))
                        continue;
                    string dst = Combine(sandbox, path);
                    Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                    File.Delete(dst);
                    File.Copy(src, dst);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return $"could not prepare the sandbox: {ex.Message}";
            }

            string normalized = PathUtility.NormalizeTargetPath(sandbox);
            if (!string.Equals(Full(normalized), sandbox, StringComparison.OrdinalIgnoreCase))
                return $"the sandbox would be re-rooted to {normalized}";
            return null;
        }

        public static IReadOnlyDictionary<string, (long Length, DateTime WriteUtc)> Snapshot(string realRoot)
        {
            string real = Full(realRoot);
            var files = Directory.Exists(Combine(real, GameDota))
                ? Directory.GetFiles(Combine(real, GameDota), VpkPattern).ToList()
                : new List<string>();
            files.AddRange(Copied.Select(c => Combine(real, c.Path)));
            files.Add(Combine(real, DotaPaths.ModsVpk));
            files.Add(Combine(real, "game/mod/pak01_dir.vpk"));
            return files.Where(File.Exists).ToDictionary(
                p => p, p => (new FileInfo(p).Length, File.GetLastWriteTimeUtc(p)), StringComparer.OrdinalIgnoreCase);
        }

        public static string? VerifyUntouched(IReadOnlyDictionary<string, (long Length, DateTime WriteUtc)> before)
        {
            var changed = before
                .Where(kv => !File.Exists(kv.Key) ||
                             (new FileInfo(kv.Key).Length, File.GetLastWriteTimeUtc(kv.Key)) != kv.Value)
                .Select(kv => Path.GetFileName(kv.Key))
                .ToList();
            return changed.Count == 0 ? null : string.Join(", ", changed);
        }

        private static bool SameStamp(string a, string b) =>
            new FileInfo(a).Length == new FileInfo(b).Length &&
            File.GetLastWriteTimeUtc(a) == File.GetLastWriteTimeUtc(b);

        private static string Full(string path) =>
            Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        private static string Combine(string root, string relative) =>
            Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

        private static bool IsInside(string child, string parent) =>
            child.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

        private static int CreateHardLink(string newPath, string existingPath) =>
            NativeMethods.CreateHardLink(newPath, existingPath, IntPtr.Zero) ? 0 : Marshal.GetLastWin32Error();

        private static class NativeMethods
        {
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CreateHardLink(string fileName, string existingFileName, IntPtr securityAttributes);
        }
    }
}
