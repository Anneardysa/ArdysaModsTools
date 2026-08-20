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
using System.Threading.Tasks;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Security;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class ProtectedVpkEngineReadabilityTests
    {
        private string _root = null!;
        private string _hlExtractPath = null!;
        private string _vpkExePath = null!;

        [SetUp]
        public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "AmtEngineProof_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _hlExtractPath = Path.Combine(baseDir, "HLExtract.exe");
            _vpkExePath = Path.Combine(baseDir, "vpk.exe");
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
        }

        [Test]
        public async Task EncryptedContainer_IsUnreadableByTheRealVpkTool_WhilePlaintextVpkIsReadable()
        {
            if (!File.Exists(_hlExtractPath) || !File.Exists(_vpkExePath))
            {
                Assert.Ignore("HLExtract.exe / vpk.exe not present in the test output — tools/ missing.");
                return;
            }

            string packDir = Path.Combine(_root, "pak");
            Directory.CreateDirectory(packDir);
            File.WriteAllText(Path.Combine(packDir, "marker.txt"), "real-asset-content");

            int exit = await RunToolAsync(_vpkExePath, "pak", workingDir: _root);
            string builtVpk = Path.Combine(_root, "pak.vpk");
            if (exit != 0 || !File.Exists(builtVpk))
            {
                Assert.Ignore($"vpk.exe could not build a VPK on this machine (exit {exit}).");
                return;
            }

            string realVpk = Path.Combine(_root, "out", "pak01_dir.vpk");
            Directory.CreateDirectory(Path.GetDirectoryName(realVpk)!);
            File.Copy(builtVpk, realVpk);

            byte[] head = File.ReadAllBytes(realVpk).Take(4).ToArray();
            Assert.That(head, Is.EqualTo(new byte[] { 0x34, 0x12, 0xAA, 0x55 }),
                "vpk.exe must produce the real Source 2 VPK signature (0x55AA1234 little-endian)");

            var extractor = new VpkExtractorService();

            string extractGood = Path.Combine(_root, "extract_good");
            Directory.CreateDirectory(extractGood);
            bool goodOk = await extractor.ExtractAsync(
                _hlExtractPath, realVpk, extractGood, _ => { }, requireItemsGame: false);

            Assert.That(goodOk, Is.True, "positive control failed — HLExtract could not read a real VPK");
            Assert.That(File.Exists(Path.Combine(extractGood, "marker.txt")), Is.True,
                "positive control failed — real asset content missing after extraction");

            byte[] plaintext = File.ReadAllBytes(realVpk);
            byte[] container = AssetCipher.Encrypt(plaintext, "local/protected/pak01_dir.vpk");
            string encryptedVpk = Path.Combine(_root, "encrypted_pak01_dir.vpk");
            File.WriteAllBytes(encryptedVpk, container);

            string extractBad = Path.Combine(_root, "extract_bad");
            Directory.CreateDirectory(extractBad);
            bool badOk = await extractor.ExtractAsync(
                _hlExtractPath, encryptedVpk, extractBad, _ => { }, requireItemsGame: false);

            Assert.Multiple(() =>
            {
                Assert.That(badOk, Is.False,
                    "HLExtract must fail against an AME1-encrypted container — the same VPK-format " +
                    "reader family the engine's search-path mounter uses cannot see a valid VPK header " +
                    "here, so the engine cannot mount it either");
                bool anyContentRecovered = Directory.Exists(extractBad)
                    && Directory.EnumerateFileSystemEntries(extractBad, "*", SearchOption.AllDirectories).Any();
                Assert.That(anyContentRecovered, Is.False,
                    "no asset content should be recoverable from the encrypted container");
            });
        }

        private static async Task<int> RunToolAsync(string exe, string arguments, string workingDir)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = arguments,
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = Process.Start(psi)!;
            await p.WaitForExitAsync().ConfigureAwait(false);
            return p.ExitCode;
        }
    }
}
