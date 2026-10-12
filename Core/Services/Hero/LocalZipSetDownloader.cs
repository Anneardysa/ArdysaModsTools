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
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services.Security;

namespace ArdysaModsTools.Core.Services
{
    public sealed class LocalZipSetDownloader : IHeroSetDownloader
    {
        private readonly IReadOnlyDictionary<string, string> _localZips;

        public LocalZipSetDownloader(IReadOnlyDictionary<string, string> localZips)
            => _localZips = localZips ?? throw new ArgumentNullException(nameof(localZips));

        public async Task<string> DownloadAndExtractAsync(
            string heroId,
            string setName,
            string zipUrl,
            Action<string> log,
            CancellationToken ct = default,
            IProgress<SpeedMetrics>? speedProgress = null,
            Action? onEncryptedDetected = null)
        {
            if (!_localZips.TryGetValue(zipUrl, out var zip) || !File.Exists(zip))
                throw new FileNotFoundException($"No local zip for {zipUrl}", zip);

            var root = Path.Combine(SafeTempPathHelper.GetSafeTempPath(), "ArdysaSelectHero", "HeroSets");
            Directory.CreateDirectory(root);
            SafeTempPathHelper.HideDirectory(root);
            var invalid = Path.GetInvalidFileNameChars();
            var safeHero = new string(heroId.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            var work = Path.Combine(root, $"{safeHero}_{Path.GetFileNameWithoutExtension(zipUrl)}");
            if (Directory.Exists(work)) Directory.Delete(work, true);
            Directory.CreateDirectory(work);

            log($"Local zip: {zip}");
            if (AssetCipher.IsEncrypted(zip))
            {
                onEncryptedDetected?.Invoke();
                var assetPath = CdnConfig.ExtractAssetPath(zipUrl)
                    ?? throw new InvalidOperationException($"No asset path in {zipUrl} to decrypt {zip} with.");
                await AssetCipher.ExtractEncryptedToDirectoryAsync(zip, assetPath, work, poisonInFlight: true, ct)
                    .ConfigureAwait(false);
            }
            else
            {
                await Task.Run(() => ZipFile.ExtractToDirectory(zip, work, overwriteFiles: true), ct)
                    .ConfigureAwait(false);
            }
            return work;
        }
    }
}
