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
using System.IO;
using Microsoft.Win32;
using Moq;
using NUnit.Framework;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Services.Hero;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class SkinSelectorCooldownServiceTests
    {
        private Mock<IConfigService> _config = null!;
        private string _tempDir = null!;
        private string _shadowFile = null!;
        private string _dpapiFile = null!;
        private string _registryKey = null!;
        private DateTime _currentTime;
        private long _currentTick;

        [SetUp]
        public void Setup()
        {
            _config = new Mock<IConfigService>();
            _tempDir = Path.Combine(Path.GetTempPath(), "amt_cooldown_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _shadowFile = Path.Combine(_tempDir, ".sst");
            _dpapiFile = Path.Combine(_tempDir, ".qst");
            _registryKey = @"Software\ArdysaModsTools\TestSec_" + Guid.NewGuid().ToString("N");

            _currentTime = new DateTime(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc);
            _currentTick = 1000000L;

            DateTime? storedTime = null;
            int storedCount = 0;
            string? storedDate = null;
            string? storedSig = null;

            _config.SetupProperty(c => c.SkinSelectorLastGenerationTimeUtc, storedTime);
            _config.SetupProperty(c => c.SkinSelectorDailyGenerationCount, storedCount);
            _config.SetupProperty(c => c.SkinSelectorDailyQuotaDateUtc, storedDate);
            _config.SetupProperty(c => c.SkinSelectorCooldownSignature, storedSig);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, true);
            }
            catch
            {
            }

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\ArdysaModsTools", writable: true);
                var subKeyName = _registryKey.Substring(_registryKey.LastIndexOf('\\') + 1);
                key?.DeleteSubKeyTree(subKeyName, throwOnMissingSubKey: false);
            }
            catch
            {
            }
        }

        private SkinSelectorCooldownService CreateService(
            TimeSpan? duration = null,
            int maxDaily = 0,
            Func<bool>? isDevMode = null,
            Func<ArdysaModsTools.Core.Models.FeatureAccessConfig?>? remoteConfig = null)
        {
            return new SkinSelectorCooldownService(
                _config.Object,
                clock: () => _currentTime,
                tickCountProvider: () => _currentTick,
                shadowFilePath: _shadowFile,
                dpapiFilePath: _dpapiFile,
                registrySubKey: _registryKey,
                cooldownDuration: duration,
                maxDailyGenerations: maxDaily,
                isDevMode: isDevMode,
                remoteConfigProvider: remoteConfig);
        }

        #region Cooldown Duration Tests (10 Minutes)

        [Test]
        public void DefaultCooldownDuration_Is10Minutes()
        {
            var service = CreateService();
            Assert.That(service.CooldownDuration, Is.EqualTo(TimeSpan.FromMinutes(10)));
            Assert.That(service.MaxDailyGenerations, Is.EqualTo(0));
        }

        [Test]
        public void IsOnCooldown_NoPriorGeneration_ReturnsFalse()
        {
            var service = CreateService();

            var isOnCooldown = service.IsOnCooldown(out var remaining, out var reason);

            Assert.That(isOnCooldown, Is.False);
            Assert.That(remaining, Is.EqualTo(TimeSpan.Zero));
            Assert.That(reason, Is.EqualTo(SkinSelectorLockReason.None));
        }

        [Test]
        public void RecordGeneration_Enforces10MinuteCooldown()
        {
            var service = CreateService();

            service.RecordGeneration();

            Assert.That(_config.Object.SkinSelectorLastGenerationTimeUtc, Is.EqualTo(_currentTime));
            Assert.That(_config.Object.SkinSelectorDailyGenerationCount, Is.EqualTo(1));
            Assert.That(_config.Object.SkinSelectorDailyQuotaDateUtc, Is.EqualTo("2026-08-21"));
            Assert.That(File.Exists(_shadowFile), Is.True);

            var onCooldown = service.IsOnCooldown(out var remaining, out var reason);
            Assert.That(onCooldown, Is.True);
            Assert.That(remaining, Is.EqualTo(TimeSpan.FromMinutes(10)));
            Assert.That(reason, Is.EqualTo(SkinSelectorLockReason.Cooldown));
        }

        [Test]
        public void IsOnCooldown_After4Minutes_ReturnsTrueWith6MinutesRemaining()
        {
            var service = CreateService();
            service.RecordGeneration();

            _currentTime = _currentTime.AddMinutes(4);
            _currentTick += (long)TimeSpan.FromMinutes(4).TotalMilliseconds;

            var onCooldown = service.IsOnCooldown(out var remaining, out var reason);

            Assert.That(onCooldown, Is.True);
            Assert.That(remaining.TotalMinutes, Is.EqualTo(6).Within(0.01));
            Assert.That(reason, Is.EqualTo(SkinSelectorLockReason.Cooldown));
        }

        [Test]
        public void IsOnCooldown_After10Minutes_ReturnsFalse()
        {
            var service = CreateService();
            service.RecordGeneration();

            _currentTime = _currentTime.AddMinutes(10);
            _currentTick += (long)TimeSpan.FromMinutes(10).TotalMilliseconds;

            var onCooldown = service.IsOnCooldown(out var remaining, out var reason);

            Assert.That(onCooldown, Is.False);
            Assert.That(remaining, Is.EqualTo(TimeSpan.Zero));
            Assert.That(reason, Is.EqualTo(SkinSelectorLockReason.None));
        }

        #endregion

        #region Unlimited Daily Generation Tests (No Daily Quota Limit)

        [Test]
        public void UnlimitedGenerations_AllowsMoreThan5Generations_WhenCooldownElapsed()
        {
            var service = CreateService();

            for (int i = 1; i <= 10; i++)
            {
                service.RecordGeneration();

                Assert.That(service.IsOnCooldown(out _, out var cooldownReason), Is.True);
                Assert.That(cooldownReason, Is.EqualTo(SkinSelectorLockReason.Cooldown));

                _currentTime = _currentTime.AddMinutes(10);
                _currentTick += (long)TimeSpan.FromMinutes(10).TotalMilliseconds;

                Assert.That(service.IsOnCooldown(out var remaining, out var reason), Is.False,
                    $"Generation #{i + 1} should be allowed after 10-minute cooldown");
                Assert.That(remaining, Is.EqualTo(TimeSpan.Zero));
                Assert.That(reason, Is.EqualTo(SkinSelectorLockReason.None));
            }

            var status = service.GetStatus();
            Assert.That(status.IsDailyLimitReached, Is.False);
            Assert.That(status.DailyGenerationsMax, Is.EqualTo(0));
            Assert.That(status.DailyGenerationsUsed, Is.EqualTo(10));
        }

        [Test]
        public void ExplicitDailyQuota_WhenConfigured_EnforcesDailyLimit()
        {
            var service = CreateService(maxDaily: 5);

            for (int i = 1; i <= 5; i++)
            {
                service.RecordGeneration();
                _currentTime = _currentTime.AddMinutes(10);
                _currentTick += (long)TimeSpan.FromMinutes(10).TotalMilliseconds;

                if (i < 5)
                {
                    Assert.That(service.IsOnCooldown(out _), Is.False);
                }
            }

            var onCooldown = service.IsOnCooldown(out var remaining, out var reason);
            Assert.That(onCooldown, Is.True);
            Assert.That(reason, Is.EqualTo(SkinSelectorLockReason.DailyLimitReached));

            var status = service.GetStatus();
            Assert.That(status.IsDailyLimitReached, Is.True);
            Assert.That(status.DailyGenerationsUsed, Is.EqualTo(5));
            Assert.That(status.DailyGenerationsMax, Is.EqualTo(5));
        }

        #endregion

        #region Multi-Tier Persistence & Anti-Tamper Tests

        [Test]
        public void IsOnCooldown_ConfigSignatureTampered_EnforcesFailClosed()
        {
            var service = CreateService();
            service.RecordGeneration();

            _config.Object.SkinSelectorDailyGenerationCount = 0;
            _config.Object.SkinSelectorCooldownSignature = "forged_signature_xyz";

            var onCooldown = service.IsOnCooldown(out _, out var reason);

            Assert.That(onCooldown, Is.True);
            Assert.That(reason, Is.EqualTo(SkinSelectorLockReason.ClockAnomaly));
        }

        [Test]
        public void IsOnCooldown_ConfigDeleted_ShadowAndDpapiRestoreState()
        {
            var service = CreateService();
            service.RecordGeneration();

            _currentTime = _currentTime.AddMinutes(4);
            _currentTick += (long)TimeSpan.FromMinutes(4).TotalMilliseconds;

            _config.Object.SkinSelectorLastGenerationTimeUtc = null;
            _config.Object.SkinSelectorDailyGenerationCount = 0;
            _config.Object.SkinSelectorDailyQuotaDateUtc = null;
            _config.Object.SkinSelectorCooldownSignature = null;

            var onCooldown = service.IsOnCooldown(out var remaining, out var reason);

            Assert.That(onCooldown, Is.True);
            Assert.That(remaining.TotalMinutes, Is.EqualTo(6).Within(0.01));
            Assert.That(reason, Is.EqualTo(SkinSelectorLockReason.Cooldown));
            Assert.That(_config.Object.SkinSelectorDailyGenerationCount, Is.EqualTo(1));
        }

        [Test]
        public void IsOnCooldown_WindowsClockJumpForwardDuringSession_MonotonicGuardEnforcesCooldown()
        {
            var service = CreateService();
            service.RecordGeneration();

            _currentTime = _currentTime.AddHours(1);
            _currentTick += (long)TimeSpan.FromMinutes(2).TotalMilliseconds;

            var onCooldown = service.IsOnCooldown(out var remaining, out var reason);

            Assert.That(onCooldown, Is.True);
            Assert.That(remaining.TotalMinutes, Is.EqualTo(8).Within(0.01));
            Assert.That(reason, Is.EqualTo(SkinSelectorLockReason.Cooldown));
        }

        [Test]
        public void IsOnCooldown_WindowsClockRolledBack_DetectsRollbackAndEnforcesFailClosed()
        {
            var service = CreateService();
            service.RecordGeneration();

            _currentTime = _currentTime.AddDays(-1);

            var freshSessionService = new SkinSelectorCooldownService(
                _config.Object,
                clock: () => _currentTime,
                tickCountProvider: () => 5000L,
                shadowFilePath: _shadowFile,
                dpapiFilePath: _dpapiFile,
                registrySubKey: _registryKey);

            var onCooldown = freshSessionService.IsOnCooldown(out _, out var reason);

            Assert.That(onCooldown, Is.True);
            Assert.That(reason, Is.EqualTo(SkinSelectorLockReason.ClockAnomaly));
        }

        [Test]
        public void ResetCooldown_ClearsAllTiers()
        {
            var service = CreateService();
            service.RecordGeneration();

            Assert.That(service.IsOnCooldown(out _), Is.True);

            service.ResetCooldown();

            Assert.That(_config.Object.SkinSelectorLastGenerationTimeUtc, Is.Null);
            Assert.That(_config.Object.SkinSelectorDailyGenerationCount, Is.EqualTo(0));
            Assert.That(File.Exists(_shadowFile), Is.False);

            Assert.That(service.IsOnCooldown(out var remaining), Is.False);
            Assert.That(remaining, Is.EqualTo(TimeSpan.Zero));
        }

        #endregion

        #region DevMode Tests

        [Test]
        public void IsOnCooldown_WhenDevModeActive_BypassesCooldownAndDailyQuota()
        {
            var isDev = true;
            var service = CreateService(isDevMode: () => isDev);

            for (int i = 0; i < 5; i++)
            {
                service.RecordGeneration();
            }

            var onCooldown = service.IsOnCooldown(out var remaining, out var reason);

            Assert.That(onCooldown, Is.False);
            Assert.That(remaining, Is.EqualTo(TimeSpan.Zero));
            Assert.That(reason, Is.EqualTo(SkinSelectorLockReason.None));

            var status = service.GetStatus();
            Assert.That(status.IsActive, Is.False);
        }

        #endregion

        #region Remote Feature Access / R2 Cooldown Tests

        [Test]
        public void IsOnCooldown_WhenRemoteCooldownDisabled_BypassesCooldown()
        {
            var config = new ArdysaModsTools.Core.Models.FeatureAccessConfig
            {
                SkinSelector = new ArdysaModsTools.Core.Models.FeatureAccess
                {
                    Enabled = true,
                    CooldownEnabled = false
                }
            };

            var service = CreateService(remoteConfig: () => config);
            service.RecordGeneration();

            var onCooldown = service.IsOnCooldown(out var remaining, out var reason);

            Assert.That(onCooldown, Is.False, "Disabled remote cooldown must bypass cooldown");
            Assert.That(remaining, Is.EqualTo(TimeSpan.Zero));
            Assert.That(reason, Is.EqualTo(SkinSelectorLockReason.None));

            var status = service.GetStatus();
            Assert.That(status.IsActive, Is.False);
            Assert.That(status.Remaining, Is.EqualTo(TimeSpan.Zero));
        }

        [Test]
        public void IsOnCooldown_WhenRemoteCooldownZeroSeconds_BypassesCooldown()
        {
            var config = new ArdysaModsTools.Core.Models.FeatureAccessConfig
            {
                SkinSelector = new ArdysaModsTools.Core.Models.FeatureAccess
                {
                    Enabled = true,
                    CooldownEnabled = true,
                    CooldownSeconds = 0
                }
            };

            var service = CreateService(remoteConfig: () => config);
            service.RecordGeneration();

            var onCooldown = service.IsOnCooldown(out var remaining, out var reason);

            Assert.That(onCooldown, Is.False);
            Assert.That(remaining, Is.EqualTo(TimeSpan.Zero));
        }

        [Test]
        public void IsOnCooldown_WhenRemoteCooldownCustomSeconds_UsesRemoteDuration()
        {
            var config = new ArdysaModsTools.Core.Models.FeatureAccessConfig
            {
                SkinSelector = new ArdysaModsTools.Core.Models.FeatureAccess
                {
                    Enabled = true,
                    CooldownEnabled = true,
                    CooldownSeconds = 300
                }
            };

            var service = CreateService(remoteConfig: () => config);
            service.RecordGeneration();

            var onCooldown = service.IsOnCooldown(out var remaining, out var reason);

            Assert.That(onCooldown, Is.True);
            Assert.That(remaining.TotalSeconds, Is.EqualTo(300).Within(1));
            Assert.That(reason, Is.EqualTo(SkinSelectorLockReason.Cooldown));

            var status = service.GetStatus();
            Assert.That(status.IsActive, Is.True);
            Assert.That(status.TotalDuration, Is.EqualTo(TimeSpan.FromSeconds(300)));
        }

        #endregion
    }
}
