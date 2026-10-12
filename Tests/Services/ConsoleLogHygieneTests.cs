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
using System.Text.RegularExpressions;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Services;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    [NonParallelizable]
    public class ConsoleLogHygieneTests
    {
        private string _logFile = null!;
        private string _originalLogFile = null!;
        private Action<string>? _originalHook;
        private Logger _logger = null!;
        private List<string> _console = null!;

        [SetUp]
        public void Setup()
        {
            _originalLogFile = FallbackLogger.LogFilePath;
            _originalHook = FallbackLogger.UserLogger;
            _logFile = Path.Combine(Path.GetTempPath(), "AmtConsoleHygiene_" + Guid.NewGuid().ToString("N") + ".log");
            FallbackLogger.SetLogFile(_logFile);

            _console = new List<string>();
            _logger = new Logger((message, _) => _console.Add(message));
            FallbackLogger.UserLogger = m => _logger.Log(m);
        }

        [TearDown]
        public void TearDown()
        {
            FallbackLogger.SetLogFile(_originalLogFile);
            FallbackLogger.UserLogger = _originalHook;
            try { File.Delete(_logFile); } catch { }
        }

        private string FileLog() => File.Exists(_logFile) ? File.ReadAllText(_logFile) : string.Empty;


        private static TestCaseData Entry(string name, Action<Logger, string> send) =>
            new TestCaseData(send).SetArgDisplayNames(name);

        private static IEnumerable<TestCaseData> InfoEntries()
        {
            yield return Entry("Log(string)", (l, m) => l.Log(m));
            yield return Entry("Log(Info)", (l, m) => l.Log(m, LogLevel.Info));
            yield return Entry("LogCategorized(default)", (l, m) => l.LogCategorized(m, "default"));
            yield return Entry("LogCategorized(success)", (l, m) => l.LogCategorized(m, "success"));
            yield return Entry("FallbackLogger.Log", (_, m) => FallbackLogger.Log(m));
        }

        private static IEnumerable<TestCaseData> ProblemEntries()
        {
            yield return Entry("Log(Warning)", (l, m) => l.Log(m, LogLevel.Warning));
            yield return Entry("Log(Error)", (l, m) => l.Log(m, LogLevel.Error));
            yield return Entry("LogWarning", (l, m) => l.LogWarning(m));
            yield return Entry("LogError", (l, m) => l.LogError(m));
            yield return Entry("LogError(ex)", (l, m) => l.LogError(m, new IOException("disk")));
            yield return Entry("LogCategorized(error)", (l, m) => l.LogCategorized(m, "error"));
            yield return Entry("LogCategorized(warning)", (l, m) => l.LogCategorized(m, "warning"));
        }

        private static IEnumerable<TestCaseData> AllEntries() => InfoEntries().Concat(ProblemEntries());

        [TestCaseSource(nameof(AllEntries))]
        public void DiagnosticTaggedLine_NeverReachesTheConsole_ButStaysInTheFile(Action<Logger, string> send)
        {
            send(_logger, "[DIAG-BTN] PushButtons: patch=True");
            send(_logger, "=== VPK RECOMPILATION DIAGNOSTICS ===");

            Assert.That(_console, Is.Empty);
            Assert.That(FileLog(), Does.Contain("PushButtons: patch=True"));
        }

        [TestCaseSource(nameof(InfoEntries))]
        public void ComponentTaggedInfoLine_NeverReachesTheConsole_ButStaysInTheFile(Action<Logger, string> send)
        {
            send(_logger, "[CacheCleaningService] Cleaned 3 files");
            send(_logger, "[PatchWatcher] Watcher active");
            send(_logger, "[ActiveProcessSentry] Threat detected: 'x64dbg'");

            Assert.That(_console, Is.Empty);
            Assert.That(FileLog(), Does.Contain("Cleaned 3 files").And.Contain("Threat detected"));
        }

        [TestCaseSource(nameof(ProblemEntries))]
        public void ComponentTaggedProblem_ReachesTheConsole_WithoutTheClassName(Action<Logger, string> send)
        {
            send(_logger, "[HeroDatabaseService] load failed");

            Assert.That(_console, Has.Count.EqualTo(1));
            Assert.That(_console[0], Does.Contain("load failed"));
            Assert.That(_console[0], Does.Not.Contain("HeroDatabaseService"));
            Assert.That(FileLog(), Does.Contain("[HeroDatabaseService] load failed"));
        }

        [TestCaseSource(nameof(AllEntries))]
        public void StackTrace_NeverReachesTheConsole_ButStaysInTheFile(Action<Logger, string> send)
        {
            send(_logger, "Install boom\n   at ArdysaModsTools.Core.Foo.Bar()\r\n   at Baz()");

            Assert.That(_console, Has.Count.EqualTo(1));
            Assert.That(_console[0], Does.Contain("Install boom"));
            Assert.That(_console[0], Does.Not.Contain("   at ").And.Not.Contain("\n"));
            Assert.That(FileLog(), Does.Contain("at ArdysaModsTools.Core.Foo.Bar()"));
        }

        [Test]
        public void DebugLine_NeverReachesTheConsole()
        {
            _logger.LogDebug("[PatchWatcher] Current version 6234");
            _logger.Log("anything", LogLevel.Debug);
            FallbackLogger.LogFileOnly("file only");

            Assert.That(_console, Is.Empty);
        }

        [Test]
        public void UserActionableLines_StillReachTheConsole()
        {
            _logger.Log("[STATUS] Ready");
            _logger.Log("[PATCH] Patching gameinfo...");
            _logger.Log("[ModsPack] Update available");
            _logger.Log("Checking for updates...");
            _logger.LogError("Install failed");
            _logger.LogWarning("Dota 2 is running");
            _logger.LogCategorized("[NET] Offline", "warning");
            FallbackLogger.Log("AdminHelper: User declined UAC elevation prompt.");

            Assert.That(_console, Has.Count.EqualTo(8));
        }


        private const string ConsoleCall = @"(?<![\w])(?:Log|LogInfo|LogWarning|LogError|LogCategorized|log)\(";

        private static readonly Regex DiagnosticOnConsole = new(
            ConsoleCall + @"\s*\$?@?""(\[DIAG|=== [^""]*DIAGNOSTIC)", RegexOptions.Compiled);

        private static readonly Regex StackTraceOnConsole = new(
            ConsoleCall + @".*(\.StackTrace\b|\b(ex|e|exception|err)\.ToString\(\))", RegexOptions.Compiled);

        private static readonly Regex ComponentTagOnConsole = new(
            @"(?<![\w])(?:Log|log)\(\s*\$?@?""" + Logger.ComponentTagPattern, RegexOptions.Compiled);

        private static readonly Regex AnyConsoleCall = new(ConsoleCall, RegexOptions.Compiled);

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "build.txt"))) dir = dir.Parent;
            Assert.That(dir, Is.Not.Null, "could not locate the repository root");
            return dir!.FullName;
        }

        private static List<string> Offenders(Regex rule, params string[] roots)
        {
            string root = RepoRoot();
            var files = new List<string>();
            foreach (var p in roots.Select(r => Path.Combine(root, r)))
            {
                if (Directory.Exists(p)) files.AddRange(Directory.EnumerateFiles(p, "*.cs", SearchOption.AllDirectories));
                else if (File.Exists(p)) files.Add(p);
            }

            var offenders = new List<string>();
            foreach (var file in files)
            {
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string t = lines[i].TrimStart();
                    if (t.StartsWith("//") || t.StartsWith("*") || t.StartsWith("///")) continue;
                    if (rule.IsMatch(lines[i]))
                        offenders.Add($"{Path.GetRelativePath(root, file)}:{i + 1}");
                }
            }
            return offenders;
        }

        private static readonly string[] AppSource = { "Core", "UI", "Helpers", "Program.cs" };

        [Test]
        public void NoSourceFile_SendsATaggedDiagnosticToTheConsole()
        {
            var offenders = Offenders(DiagnosticOnConsole, AppSource);
            Assert.That(offenders, Is.Empty, "use LogDebug for diagnostics: " + string.Join(", ", offenders));
        }

        [Test]
        public void NoSourceFile_SendsAStackTraceToTheConsole()
        {
            var offenders = Offenders(StackTraceOnConsole, AppSource);
            Assert.That(offenders, Is.Empty,
                "stack traces go to LogDebug / FallbackLogger.LogFileOnly: " + string.Join(", ", offenders));
        }

        [Test]
        public void NoSourceFile_SendsAComponentInternalLineToTheConsole()
        {
            var offenders = Offenders(ComponentTagOnConsole, AppSource);
            Assert.That(offenders, Is.Empty,
                "a [ClassName] line is an internal — use LogDebug / FallbackLogger.LogFileOnly: " + string.Join(", ", offenders));
        }

        [Test]
        public void SecurityServices_NeverWriteToTheConsole()
        {
            var offenders = Offenders(AnyConsoleCall, Path.Combine("Core", "Services", "Security"));
            Assert.That(offenders, Is.Empty,
                "Security logs file-only (LogDebug / FallbackLogger.LogFileOnly): " + string.Join(", ", offenders));
        }

        [Test]
        public void ComponentTagPattern_MatchesInternalsButNotUserFacingTags()
        {
            var tag = new Regex("^" + Logger.ComponentTagPattern);
            Assert.That(new[] { "[CacheCleaningService]", "[UtilitiesPresenter]", "[PatchWatcher]", "[ActiveProcessSentry]", "[UtilitiesFormWebView]" },
                Has.All.Matches<string>(s => tag.IsMatch(s)));
            Assert.That(new[] { "[STATUS]", "[PATCH]", "[ModsPack]", "[NET]", "[PLAY]", "[SYNC]", "[DEV]" },
                Has.None.Matches<string>(s => tag.IsMatch(s)));
        }
    }
}
