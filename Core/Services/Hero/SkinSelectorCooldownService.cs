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
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Services.Config;
using Microsoft.Win32;

namespace ArdysaModsTools.Core.Services.Hero
{
    public sealed class SkinSelectorCooldownService : ISkinSelectorCooldownService
    {
        private static readonly TimeSpan DefaultCooldownDuration = TimeSpan.FromMinutes(10);
        private const int DefaultMaxDailyGenerations = 0;
        private const string Salt = "AMT_SKIN_SELECTOR_COOLDOWN_PROTECTION_V2_PRO";
        private const string RegistrySubKey = @"Software\ArdysaModsTools\Security";
        private const string RegistryValueName = "StateToken";

        private readonly IConfigService _configService;
        private readonly Func<DateTime> _clock;
        private readonly Func<long> _tickCountProvider;
        private readonly Func<bool> _isDevMode;
        private readonly string _shadowFilePath;
        private readonly string _dpapiFilePath;
        private readonly string _registrySubKey;
        private readonly TimeSpan _cooldownDuration;
        private readonly int _maxDailyGenerations;
        private readonly object _lock = new();

        private static TimeSpan _serverTimeOffset = TimeSpan.Zero;
        private static DateTime _lastServerTimeSyncUtc = DateTime.MinValue;
        private static readonly TimeSpan ServerSyncInterval = TimeSpan.FromMinutes(15);

        private long? _lastGenerationSessionTick;
        private int _sessionGenerationCount = 0;
        private string? _cachedHwid;

        public TimeSpan CooldownDuration => _cooldownDuration;

        public int MaxDailyGenerations => _maxDailyGenerations;

        public SkinSelectorCooldownService(
            IConfigService configService,
            Func<DateTime>? clock = null,
            Func<long>? tickCountProvider = null,
            string? shadowFilePath = null,
            string? dpapiFilePath = null,
            string? registrySubKey = null,
            TimeSpan? cooldownDuration = null,
            int maxDailyGenerations = DefaultMaxDailyGenerations,
            Func<bool>? isDevMode = null)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _clock = clock ?? (() => DateTime.UtcNow + _serverTimeOffset);
            _tickCountProvider = tickCountProvider ?? (() => Environment.TickCount64);
            _cooldownDuration = cooldownDuration ?? DefaultCooldownDuration;
            _maxDailyGenerations = maxDailyGenerations > 0 ? maxDailyGenerations : 0;
            _isDevMode = isDevMode != null ? isDevMode : () => EnvironmentConfig.IsDevMode;
            _registrySubKey = registrySubKey ?? RegistrySubKey;

            _shadowFilePath = shadowFilePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ArdysaModsTools",
                ".sst");

            _dpapiFilePath = dpapiFilePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ArdysaModsTools",
                ".qst");
        }

        private sealed class StateSnapshot
        {
            public string? DateUtc { get; set; }
            public int DailyCount { get; set; }
            public string? TimestampUtc { get; set; }
            public string? Signature { get; set; }
        }

        public bool IsOnCooldown(out TimeSpan remaining) => IsOnCooldown(out remaining, out _);

        public bool IsOnCooldown(out TimeSpan remaining, out SkinSelectorLockReason reason)
        {
            lock (_lock)
            {
                if (_isDevMode())
                {
                    remaining = TimeSpan.Zero;
                    reason = SkinSelectorLockReason.None;
                    return false;
                }

                var now = _clock();
                var currentTick = _tickCountProvider();
                var todayDateUtc = now.ToUniversalTime().ToString("yyyy-MM-dd");

                var reconciled = LoadAndReconcileStores(now, currentTick, out var hasTamper);

                if (hasTamper)
                {
                    remaining = _cooldownDuration;
                    reason = SkinSelectorLockReason.ClockAnomaly;
                    return true;
                }

                var effectiveDate = reconciled.DateUtc ?? todayDateUtc;
                var effectiveCount = reconciled.DailyCount;
                DateTime? resolvedTime = null;

                if (!string.IsNullOrEmpty(reconciled.TimestampUtc) &&
                    DateTime.TryParse(reconciled.TimestampUtc, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var parsedDt))
                {
                    resolvedTime = DateTime.SpecifyKind(parsedDt, DateTimeKind.Utc);
                }

                if (string.CompareOrdinal(todayDateUtc, effectiveDate) > 0)
                {
                    effectiveCount = 0;
                    effectiveDate = todayDateUtc;
                }
                else if (string.CompareOrdinal(todayDateUtc, effectiveDate) < 0)
                {
                    remaining = _cooldownDuration;
                    reason = SkinSelectorLockReason.ClockAnomaly;
                    return true;
                }

                if (_maxDailyGenerations > 0 && effectiveCount >= _maxDailyGenerations)
                {
                    remaining = GetRemainingUntilNextDayUtc(now);
                    reason = SkinSelectorLockReason.DailyLimitReached;
                    return true;
                }

                if (_lastGenerationSessionTick.HasValue)
                {
                    var elapsedMs = currentTick - _lastGenerationSessionTick.Value;
                    if (elapsedMs >= 0 && elapsedMs < _cooldownDuration.TotalMilliseconds)
                    {
                        var remainingMs = _cooldownDuration.TotalMilliseconds - elapsedMs;
                        remaining = TimeSpan.FromMilliseconds(remainingMs);
                        reason = SkinSelectorLockReason.Cooldown;
                        return true;
                    }
                }

                if (resolvedTime.HasValue)
                {
                    var lastGenUtc = resolvedTime.Value;

                    if (now < lastGenUtc - TimeSpan.FromMinutes(1))
                    {
                        remaining = _cooldownDuration;
                        reason = SkinSelectorLockReason.ClockAnomaly;
                        return true;
                    }

                    var elapsed = now - lastGenUtc;
                    if (elapsed < _cooldownDuration && elapsed >= TimeSpan.Zero)
                    {
                        remaining = _cooldownDuration - elapsed;
                        reason = SkinSelectorLockReason.Cooldown;
                        return true;
                    }
                }

                remaining = TimeSpan.Zero;
                reason = SkinSelectorLockReason.None;
                return false;
            }
        }

        public SkinSelectorCooldownStatus GetStatus()
        {
            lock (_lock)
            {
                var active = IsOnCooldown(out var remaining, out var reason);
                var now = _clock();
                var todayDateUtc = now.ToUniversalTime().ToString("yyyy-MM-dd");

                var reconciled = LoadAndReconcileStores(now, _tickCountProvider(), out _);
                int dailyUsed = reconciled.DailyCount;

                if (string.CompareOrdinal(todayDateUtc, reconciled.DateUtc ?? todayDateUtc) > 0)
                {
                    dailyUsed = 0;
                }

                var totalDuration = reason == SkinSelectorLockReason.DailyLimitReached
                    ? TimeSpan.FromHours(24)
                    : _cooldownDuration;

                return new SkinSelectorCooldownStatus
                {
                    IsActive = active,
                    Remaining = remaining,
                    TotalDuration = totalDuration,
                    LastGenerationTimeUtc = _configService.SkinSelectorLastGenerationTimeUtc,
                    DailyGenerationsUsed = dailyUsed,
                    DailyGenerationsMax = _maxDailyGenerations,
                    IsDailyLimitReached = _maxDailyGenerations > 0 && dailyUsed >= _maxDailyGenerations,
                    LockReason = reason
                };
            }
        }

        public void RecordGeneration()
        {
            lock (_lock)
            {
                var now = _clock();
                var currentTick = _tickCountProvider();
                var todayDateUtc = now.ToUniversalTime().ToString("yyyy-MM-dd");

                var reconciled = LoadAndReconcileStores(now, currentTick, out _);
                int currentCount = reconciled.DailyCount;

                if (string.CompareOrdinal(todayDateUtc, reconciled.DateUtc ?? todayDateUtc) > 0)
                {
                    currentCount = 0;
                }

                int newCount = currentCount + 1;
                _sessionGenerationCount++;
                _lastGenerationSessionTick = currentTick;

                var signature = ComputeSignature(todayDateUtc, newCount, now);

                var snapshot = new StateSnapshot
                {
                    DateUtc = todayDateUtc,
                    DailyCount = newCount,
                    TimestampUtc = now.ToUniversalTime().ToString("o"),
                    Signature = signature
                };

                SaveAllStores(snapshot, now);
            }
        }

        public void ResetCooldown()
        {
            lock (_lock)
            {
                _lastGenerationSessionTick = null;
                _sessionGenerationCount = 0;

                _configService.SkinSelectorLastGenerationTimeUtc = null;
                _configService.SkinSelectorDailyGenerationCount = 0;
                _configService.SkinSelectorDailyQuotaDateUtc = null;
                _configService.SkinSelectorCooldownSignature = null;

                try { if (File.Exists(_shadowFilePath)) File.Delete(_shadowFilePath); } catch { }

                try { if (File.Exists(_dpapiFilePath)) File.Delete(_dpapiFilePath); } catch { }

                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(_registrySubKey, writable: true);
                    key?.DeleteValue(RegistryValueName, throwOnMissingValue: false);
                }
                catch { }
            }
        }

        public static async Task CalibrateServerTimeAsync(HttpClient? httpClient = null, CancellationToken ct = default)
        {
            if (DateTime.UtcNow - _lastServerTimeSyncUtc < ServerSyncInterval)
                return;

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(3));

                var client = httpClient ?? ArdysaModsTools.Helpers.HttpClientProvider.Client;
                using var request = new HttpRequestMessage(HttpMethod.Head, "https://cdn.jsdelivr.net/");
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);

                if (response.Headers.Date.HasValue)
                {
                    var serverUtc = response.Headers.Date.Value.UtcDateTime;
                    var localUtc = DateTime.UtcNow;
                    _serverTimeOffset = serverUtc - localUtc;
                    _lastServerTimeSyncUtc = localUtc;
                }
            }
            catch
            {
            }
        }

        #region Cross-Store Reconciliation & Security

        private StateSnapshot LoadAndReconcileStores(DateTime now, long currentTick, out bool hasTamper)
        {
            hasTamper = false;

            var t1 = LoadTier1();
            var t2 = LoadTier2();
            var t3 = LoadTier3();
            var t4 = LoadTier4();

            bool v1 = IsValidSnapshot(t1);
            bool v2 = IsValidSnapshot(t2);
            bool v3 = IsValidSnapshot(t3);
            bool v4 = IsValidSnapshot(t4);

            if ((t1 != null && !v1) || (t2 != null && !v2) || (t3 != null && !v3) || (t4 != null && !v4))
            {
                hasTamper = true;
                var failCount = _maxDailyGenerations > 0 ? _maxDailyGenerations : 0;
                var lockedSnapshot = new StateSnapshot
                {
                    DateUtc = now.ToUniversalTime().ToString("yyyy-MM-dd"),
                    DailyCount = failCount,
                    TimestampUtc = now.ToUniversalTime().ToString("o"),
                    Signature = ComputeSignature(now.ToUniversalTime().ToString("yyyy-MM-dd"), failCount, now)
                };
                SaveAllStores(lockedSnapshot, now);
                return lockedSnapshot;
            }

            int maxDailyCount = 0;
            string? latestDateUtc = null;
            DateTime? latestTimestampUtc = null;

            void CheckStore(StateSnapshot? s, bool valid)
            {
                if (!valid || s == null) return;
                if (s.DailyCount > maxDailyCount)
                    maxDailyCount = s.DailyCount;

                if (!string.IsNullOrEmpty(s.DateUtc))
                {
                    if (latestDateUtc == null || string.CompareOrdinal(s.DateUtc, latestDateUtc) > 0)
                        latestDateUtc = s.DateUtc;
                }

                if (!string.IsNullOrEmpty(s.TimestampUtc) &&
                    DateTime.TryParse(s.TimestampUtc, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var dt))
                {
                    dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                    if (!latestTimestampUtc.HasValue || dt > latestTimestampUtc.Value)
                        latestTimestampUtc = dt;
                }
            }

            CheckStore(t1, v1);
            CheckStore(t2, v2);
            CheckStore(t3, v3);
            CheckStore(t4, v4);

            latestDateUtc ??= now.ToUniversalTime().ToString("yyyy-MM-dd");

            var reconciled = new StateSnapshot
            {
                DateUtc = latestDateUtc,
                DailyCount = maxDailyCount,
                TimestampUtc = latestTimestampUtc?.ToUniversalTime().ToString("o"),
                Signature = latestTimestampUtc.HasValue ? ComputeSignature(latestDateUtc, maxDailyCount, latestTimestampUtc.Value) : null
            };

            bool needsHeal = (t1 == null || t1.DailyCount < maxDailyCount) ||
                             (t2 == null || t2.DailyCount < maxDailyCount) ||
                             (t3 == null || t3.DailyCount < maxDailyCount) ||
                             (t4 == null || t4.DailyCount < maxDailyCount);

            if (needsHeal && (v1 || v2 || v3 || v4) && latestTimestampUtc.HasValue)
            {
                SaveAllStores(reconciled, latestTimestampUtc.Value);
            }

            return reconciled;
        }

        private bool IsValidSnapshot(StateSnapshot? s)
        {
            if (s == null || string.IsNullOrEmpty(s.Signature) || string.IsNullOrEmpty(s.DateUtc) || string.IsNullOrEmpty(s.TimestampUtc))
                return false;

            if (!DateTime.TryParse(s.TimestampUtc, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var parsedDt))
                return false;

            var expected = ComputeSignature(s.DateUtc, s.DailyCount, DateTime.SpecifyKind(parsedDt, DateTimeKind.Utc));
            return string.Equals(s.Signature, expected, StringComparison.OrdinalIgnoreCase);
        }

        private StateSnapshot? LoadTier1()
        {
            try
            {
                var dt = _configService.SkinSelectorLastGenerationTimeUtc;
                var count = _configService.SkinSelectorDailyGenerationCount;
                var date = _configService.SkinSelectorDailyQuotaDateUtc;
                var sig = _configService.SkinSelectorCooldownSignature;

                if (!dt.HasValue && count == 0 && string.IsNullOrEmpty(sig))
                    return null;

                return new StateSnapshot
                {
                    DateUtc = date,
                    DailyCount = count,
                    TimestampUtc = dt?.ToUniversalTime().ToString("o"),
                    Signature = sig
                };
            }
            catch { return null; }
        }

        private StateSnapshot? LoadTier2()
        {
            try
            {
                if (!File.Exists(_shadowFilePath)) return null;
                var json = File.ReadAllText(_shadowFilePath);
                return JsonSerializer.Deserialize<StateSnapshot>(json);
            }
            catch { return null; }
        }

        private StateSnapshot? LoadTier3()
        {
            try
            {
                if (!File.Exists(_dpapiFilePath)) return null;
                var encBytes = File.ReadAllBytes(_dpapiFilePath);
                var plainBytes = ProtectedData.Unprotect(encBytes, Encoding.UTF8.GetBytes(Salt), DataProtectionScope.CurrentUser);
                var json = Encoding.UTF8.GetString(plainBytes);
                return JsonSerializer.Deserialize<StateSnapshot>(json);
            }
            catch { return null; }
        }

        private StateSnapshot? LoadTier4()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(_registrySubKey);
                var raw = key?.GetValue(RegistryValueName) as string;
                if (string.IsNullOrEmpty(raw)) return null;
                return JsonSerializer.Deserialize<StateSnapshot>(raw);
            }
            catch { return null; }
        }

        private void SaveAllStores(StateSnapshot snapshot, DateTime timestampUtc)
        {
            _configService.SkinSelectorLastGenerationTimeUtc = timestampUtc;
            _configService.SkinSelectorDailyGenerationCount = snapshot.DailyCount;
            _configService.SkinSelectorDailyQuotaDateUtc = snapshot.DateUtc;
            _configService.SkinSelectorCooldownSignature = snapshot.Signature;

            var json = JsonSerializer.Serialize(snapshot);

            try
            {
                var dir = Path.GetDirectoryName(_shadowFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var tempPath = _shadowFilePath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _shadowFilePath, overwrite: true);
                File.SetAttributes(_shadowFilePath, FileAttributes.Hidden | FileAttributes.NotContentIndexed);
            }
            catch { }

            try
            {
                var dir = Path.GetDirectoryName(_dpapiFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var plainBytes = Encoding.UTF8.GetBytes(json);
                var encBytes = ProtectedData.Protect(plainBytes, Encoding.UTF8.GetBytes(Salt), DataProtectionScope.CurrentUser);
                var tempPath = _dpapiFilePath + ".tmp";
                File.WriteAllBytes(tempPath, encBytes);
                File.Move(tempPath, _dpapiFilePath, overwrite: true);
                File.SetAttributes(_dpapiFilePath, FileAttributes.Hidden | FileAttributes.System | FileAttributes.NotContentIndexed);
            }
            catch { }

            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(_registrySubKey);
                key.SetValue(RegistryValueName, json, RegistryValueKind.String);
            }
            catch { }
        }

        private string ComputeSignature(string dateUtc, int dailyCount, DateTime timestampUtc)
        {
            var hwid = GetHwid();
            var raw = $"{dateUtc}|{dailyCount}|{timestampUtc.ToUniversalTime():o}|{hwid}|{Salt}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Salt + hwid));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hash);
        }

        private string GetHwid()
        {
            if (_cachedHwid != null) return _cachedHwid;

            try
            {
                var machineGuid = "";
                try
                {
                    machineGuid = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography", "MachineGuid", "") as string ?? "";
                }
                catch { }

                var raw = $"{Environment.MachineName}|{Environment.ProcessorCount}|{Environment.UserName}|{machineGuid}|{Salt}";
                using var sha = SHA256.Create();
                _cachedHwid = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw)));
            }
            catch
            {
                _cachedHwid = "FALLBACK_HWID_" + Environment.MachineName;
            }

            return _cachedHwid;
        }

        private static TimeSpan GetRemainingUntilNextDayUtc(DateTime now)
        {
            var nextMidnightUtc = now.ToUniversalTime().Date.AddDays(1);
            var rem = nextMidnightUtc - now.ToUniversalTime();
            return rem > TimeSpan.Zero ? rem : TimeSpan.FromHours(24);
        }

        #endregion
    }
}
