/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */
using System;
using System.IO;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Services;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    [NonParallelizable]
    public class InstallFailureDiagnosticsTests
    {
        private string _dir = null!;
        private string _logFile = null!;

        [SetUp]
        public void Setup()
        {
            _dir = Path.Combine(Path.GetTempPath(), "AmtDiag_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _logFile = Path.Combine(_dir, "fallback.log");
            FallbackLogger.SetLogFile(_logFile);
        }

        [TearDown]
        public void TearDown()
        {
            FallbackLogger.SetLogFile(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ardysa_fallback.log"));
            try { Directory.Delete(_dir, true); } catch { }
        }

        [Test]
        public void Logger_ErrorLine_ReachesTheFallbackFile()
        {
            IAppLogger logger = new Logger((_, _) => { });

            logger.LogError("package swap exploded");

            Assert.That(File.ReadAllText(_logFile), Does.Contain("package swap exploded"));
        }

        [Test]
        public void Logger_InfoLine_ReachesTheFallbackFile()
        {
            IAppLogger logger = new Logger((_, _) => { });

            logger.Log("Installation completed successfully.");

            Assert.That(File.ReadAllText(_logFile), Does.Contain("Installation completed successfully."));
        }

        [Test]
        public void Logger_StillWritesToTheConsoleSink()
        {
            string? seen = null;
            IAppLogger logger = new Logger((text, _) => seen = text);

            logger.Log("hello console");

            Assert.That(seen, Is.EqualTo("hello console"));
        }

        [Test]
        public void HasRoomToBuild_WhenTheDriveCannotHoldTheRebuild_FailsWithTheNumbers()
        {
            long free = new DriveInfo(Path.GetPathRoot(_dir)!).AvailableFreeSpace;
            string huge = Path.Combine(_dir, "huge.vpk");
            using (var fs = new FileStream(huge, FileMode.CreateNew))
                fs.SetLength(free / 2);

            bool ok = ModsPackDataService.HasRoomToBuild(_dir, huge, out string error);

            Assert.Multiple(() =>
            {
                Assert.That(ok, Is.False);
                Assert.That(error, Does.Contain("Not enough free disk space"));
                Assert.That(error, Does.Contain("GB"));
            });
        }

        [Test]
        public void HasRoomToBuild_ForAnOrdinaryPackage_Passes()
        {
            string small = Path.Combine(_dir, "small.vpk");
            File.WriteAllBytes(small, new byte[1024]);

            Assert.That(ModsPackDataService.HasRoomToBuild(_dir, small, out _), Is.True);
        }

        [Test]
        public void HasRoomToBuild_WhenItCannotMeasure_Passes()
        {
            Assert.That(
                ModsPackDataService.HasRoomToBuild(_dir, Path.Combine(_dir, "missing.vpk"), out _),
                Is.True);
        }
    }
}
