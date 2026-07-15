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
using System.Net;
using System.Security.Cryptography;
using System.Text;
using ArdysaModsTools.Core.Services.Update;
using ArdysaModsTools.Core.Services.Update.Models;
using ArdysaModsTools.Updater;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    [Category("Integration")]
    public class DeltaUpdateEndToEndTests
    {
        private string _root = null!;
        private string _installDir = null!;
        private string _newRelease = null!;
        private string _serverRoot = null!;
        private string _stagingRoot = null!;

        private HttpListener _listener = null!;
        private string _baseUrl = null!;
        private CancellationTokenSource _serverCts = null!;

        private const string OldVersion = "1.0.0";
        private const string NewVersion = "1.1.0";

        [SetUp]
        public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), $"AMT_E2E_{Guid.NewGuid():N}");
            _installDir = Path.Combine(_root, "install");
            _newRelease = Path.Combine(_root, "new-release");
            _serverRoot = Path.Combine(_root, "cdn");
            _stagingRoot = Path.Combine(_root, "staging");

            Directory.CreateDirectory(_installDir);
            Directory.CreateDirectory(_newRelease);
            Directory.CreateDirectory(_serverRoot);

            StartServer();
        }

        [TearDown]
        public void TearDown()
        {
            try { _serverCts.Cancel(); _listener.Stop(); _listener.Close(); } catch { }
            _serverCts.Dispose();
            try { Directory.Delete(_root, true); } catch { }
        }


        private void StartServer()
        {
            _serverCts = new CancellationTokenSource();

            for (int port = 8100; port < 8200; port++)
            {
                try
                {
                    _baseUrl = $"http://127.0.0.1:{port}/";
                    _listener = new HttpListener();
                    _listener.Prefixes.Add(_baseUrl);
                    _listener.Start();
                    break;
                }
                catch (HttpListenerException) {  }
            }

            _ = Task.Run(async () =>
            {
                while (!_serverCts.IsCancellationRequested)
                {
                    HttpListenerContext ctx;
                    try { ctx = await _listener.GetContextAsync(); } catch { return; }

                    try
                    {
                        string rel = Uri.UnescapeDataString(ctx.Request.Url!.AbsolutePath.TrimStart('/'));
                        string path = Path.Combine(_serverRoot, rel.Replace('/', Path.DirectorySeparatorChar));

                        if (File.Exists(path))
                        {
                            byte[] body = await File.ReadAllBytesAsync(path);
                            ctx.Response.StatusCode = 200;
                            ctx.Response.ContentLength64 = body.Length;
                            ctx.Response.Headers["Accept-Ranges"] = "bytes";

                            if (!ctx.Request.HttpMethod.Equals("HEAD", StringComparison.OrdinalIgnoreCase))
                                await ctx.Response.OutputStream.WriteAsync(body);
                        }
                        else
                        {
                            ctx.Response.StatusCode = 404;
                        }
                    }
                    catch { ctx.Response.StatusCode = 500; }

                    try { ctx.Response.Close(); } catch { }
                }
            });
        }

        private string Publish(string version, string treeRoot)
        {
            string releaseDir = Path.Combine(_serverRoot, "releases", version);
            string filesDir = Path.Combine(releaseDir, "files");
            Directory.CreateDirectory(filesDir);

            var entries = new List<string>();

            foreach (var file in Directory.EnumerateFiles(treeRoot, "*", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(treeRoot, file).Replace('\\', '/');
                string dest = Path.Combine(filesDir, rel.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(file, dest, overwrite: true);

                byte[] bytes = File.ReadAllBytes(file);
                entries.Add($"    \"{rel}\": {{ \"sha256\": \"{Convert.ToHexString(SHA256.HashData(bytes))}\", \"size\": {bytes.Length} }}");
            }

            string json = "{\n  \"version\": 1,\n  \"algorithm\": \"SHA-256\",\n  \"assets\": {\n"
                        + string.Join(",\n", entries) + "\n  }\n}";
            File.WriteAllText(Path.Combine(releaseDir, "files.json"), json);

            return $"{_baseUrl}releases/{version}/files.json";
        }

        private static void Write(string root, string relPath, string content)
        {
            string full = Path.Combine(root, relPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }

        private static string Read(string root, string relPath) =>
            File.ReadAllText(Path.Combine(root, relPath.Replace('/', Path.DirectorySeparatorChar)));

        private static bool Exists(string root, string relPath) =>
            File.Exists(Path.Combine(root, relPath.Replace('/', Path.DirectorySeparatorChar)));


        [Test]
        public async Task FullDeltaUpdate_DownloadsOnlyWhatChanged_AndAppliesIt()
        {
            Write(_installDir, "ArdysaModsTools.exe", "app v1");
            Write(_installDir, "ArdysaModsTools.dll", "logic v1");
            Write(_installDir, "System.Private.CoreLib.dll", "the runtime, unchanged across releases");
            Write(_installDir, "Assets/Locales/en.json", "{ \"hello\": \"v1\" }");
            Write(_installDir, "Assets/Locales/id.json", "{ \"dropped\": \"in v1.1\" }");
            Write(_installDir, DeltaUpdateService.UpdaterRelPath, "applier v1");

            string oldTree = Path.Combine(_root, "old-release");
            Directory.CreateDirectory(oldTree);
            foreach (var f in Directory.EnumerateFiles(_installDir, "*", SearchOption.AllDirectories))
                Write(oldTree, Path.GetRelativePath(_installDir, f).Replace('\\', '/'), File.ReadAllText(f));

            Publish(OldVersion, oldTree);

            Write(_newRelease, "ArdysaModsTools.exe", "app v2 — bigger now");
            Write(_newRelease, "ArdysaModsTools.dll", "logic v2");
            Write(_newRelease, "System.Private.CoreLib.dll", "the runtime, unchanged across releases");
            Write(_newRelease, "Assets/Locales/en.json", "{ \"hello\": \"v2\" }");
            Write(_newRelease, "Assets/Html/new_page.html", "<p>added in v1.1</p>");
            Write(_newRelease, DeltaUpdateService.UpdaterRelPath, "applier v1");

            string manifestUrl = Publish(NewVersion, _newRelease);

            var service = new DeltaUpdateService(
                new ArdysaModsTools.Core.Services.Logger((_, _) => { }), _installDir, _stagingRoot);

            var info = new UpdateInfo
            {
                Version = NewVersion,
                CurrentVersion = OldVersion,
                FilesManifestUrl = manifestUrl,
                IsUpdateAvailable = true
            };

            DeltaPlan? plan = await service.PrepareAsync(info);

            Assert.That(plan, Is.Not.Null, "a delta must be possible");

            var changed = plan!.Files.Select(f => f.RelPath).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(changed, Does.Contain("ArdysaModsTools.exe"));
                Assert.That(changed, Does.Contain("ArdysaModsTools.dll"));
                Assert.That(changed, Does.Contain("Assets/Locales/en.json"));
                Assert.That(changed, Does.Contain("Assets/Html/new_page.html"));

                Assert.That(changed, Does.Not.Contain("System.Private.CoreLib.dll"));

                Assert.That(changed, Does.Contain(DeltaUpdateService.UpdaterRelPath));

                Assert.That(plan.Deletions, Is.EqualTo(new[] { "Assets/Locales/id.json" }));
            });

            await service.StageAsync(plan);

            Assert.That(File.Exists(Path.Combine(plan.StagingDir, DeltaUpdateService.StagedOkMarker)), Is.True,
                ".staged-ok is written only after every file downloaded AND verified");

            var result = ApplyEngine.Run(plan.StagingDir, waitPid: 0, log: _ => { });

            Assert.That(result.Success, Is.True, result.Message);

            Assert.Multiple(() =>
            {
                Assert.That(Read(_installDir, "ArdysaModsTools.exe"), Is.EqualTo("app v2 — bigger now"));
                Assert.That(Read(_installDir, "ArdysaModsTools.dll"), Is.EqualTo("logic v2"));
                Assert.That(Read(_installDir, "Assets/Locales/en.json"), Is.EqualTo("{ \"hello\": \"v2\" }"));
                Assert.That(Read(_installDir, "Assets/Html/new_page.html"), Is.EqualTo("<p>added in v1.1</p>"));
                Assert.That(Read(_installDir, "System.Private.CoreLib.dll"), Is.EqualTo("the runtime, unchanged across releases"));
                Assert.That(Exists(_installDir, "Assets/Locales/id.json"), Is.False, "dropped file must be gone");

                Assert.That(Directory.EnumerateFiles(_installDir, "*", SearchOption.AllDirectories)
                        .Any(f => f.EndsWith(".amtbak") || f.EndsWith(".amtnew") || f.EndsWith(".amt-update-in-progress")),
                    Is.False, "no leftovers");
            });
        }

        [Test]
        public async Task Prepare_WhenTheServerHasNoManifest_ReturnsNullSoTheFullDownloadIsOffered()
        {
            Write(_installDir, "ArdysaModsTools.exe", "app v1");

            var service = new DeltaUpdateService(
                new ArdysaModsTools.Core.Services.Logger((_, _) => { }), _installDir, _stagingRoot);

            var plan = await service.PrepareAsync(new UpdateInfo
            {
                Version = NewVersion,
                CurrentVersion = OldVersion,
                FilesManifestUrl = $"{_baseUrl}releases/{NewVersion}/files.json",
                IsUpdateAvailable = true
            });

            Assert.That(plan, Is.Null);
        }

        [Test]
        public async Task Stage_WhenTheServerServesTamperedBytes_RefusesToStage()
        {
            Write(_installDir, "ArdysaModsTools.exe", "app v1");
            Write(_installDir, DeltaUpdateService.UpdaterRelPath, "applier v1");

            Write(_newRelease, "ArdysaModsTools.exe", "app v2");
            Write(_newRelease, DeltaUpdateService.UpdaterRelPath, "applier v1");
            string manifestUrl = Publish(NewVersion, _newRelease);

            File.WriteAllText(
                Path.Combine(_serverRoot, "releases", NewVersion, "files", "ArdysaModsTools.exe"),
                "TAMPERED — not what the manifest says");

            var service = new DeltaUpdateService(
                new ArdysaModsTools.Core.Services.Logger((_, _) => { }), _installDir, _stagingRoot);

            var plan = await service.PrepareAsync(new UpdateInfo
            {
                Version = NewVersion,
                CurrentVersion = OldVersion,
                FilesManifestUrl = manifestUrl,
                IsUpdateAvailable = true
            });

            Assert.That(plan, Is.Not.Null);
            Assert.ThrowsAsync<ArdysaModsTools.Core.Exceptions.DownloadException>(
                async () => await service.StageAsync(plan!));

            Assert.That(File.Exists(Path.Combine(plan!.StagingDir, DeltaUpdateService.StagedOkMarker)), Is.False,
                "a rejected download must never produce an appliable staging folder");
            Assert.That(Read(_installDir, "ArdysaModsTools.exe"), Is.EqualTo("app v1"), "install untouched");
        }
    }
}
