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

namespace ArdysaModsTools.Core.Services
{
    internal static class InstallErrorMessage
    {
        private const int SharingViolation = unchecked((int)0x80070020);
        private const int LockViolation    = unchecked((int)0x80070021);
        private const int UserMappedFile   = unchecked((int)0x800704C8);
        private const int HandleDiskFull   = unchecked((int)0x80070027);
        private const int DiskFull         = unchecked((int)0x80070070);

        public static string Describe(Exception ex) => ex switch
        {
            IOException io when io.HResult == DiskFull || io.HResult == HandleDiskFull
                => "Not enough disk space — free some space and try again.",
            IOException io when io.HResult == SharingViolation || io.HResult == LockViolation
                                || io.HResult == UserMappedFile
                => "A game file is still in use — close Dota 2 and Steam completely, then try again.",
            UnauthorizedAccessException
                => "Access denied — close Dota 2 or allow AMT in your antivirus, then try again.",
            HttpRequestException
                => "A required download failed — check your internet connection and try again.",
            IOException
                => "A file could not be read or written — close Dota 2, check free disk space, and try again.",
            _ => "Unexpected error — please try again."
        };
    }
}
