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
namespace ArdysaModsTools.Core.Models
{
    public record SteamAppState
    {
        public bool ManifestFound { get; init; }

        public long StateFlags { get; init; }

        public long BytesToDownload { get; init; }

        public long BytesDownloaded { get; init; }

        private const long StateUpdateRequired = 2;
        private const long StateFullyInstalled = 4;
        private const long StateFilesMissing = 32;
        private const long StateFilesCorrupt = 128;
        private const long StateUpdateRunning = 256;
        private const long StateUpdatePaused = 512;
        private const long StateUpdateStarted = 1024;
        private const long StateReconfiguring = 65536;

        private const long BusyMask =
            StateUpdateRequired | StateFilesMissing | StateFilesCorrupt |
            StateUpdateRunning | StateUpdatePaused | StateUpdateStarted | StateReconfiguring;

        public bool IsUpdatePending => ManifestFound && ((StateFlags & BusyMask) != 0 || BytesToDownload > BytesDownloaded);

        public bool IsSettled => ManifestFound && (StateFlags & StateFullyInstalled) != 0 && !IsUpdatePending;

        public int? DownloadPercent =>
            BytesToDownload > 0 && BytesDownloaded >= 0 && BytesDownloaded <= BytesToDownload
                ? (int)(BytesDownloaded * 100 / BytesToDownload)
                : null;

        public static SteamAppState Unknown { get; } = new();
    }
}
