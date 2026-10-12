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
using System.Net;
using System.Net.Http;
using ArdysaModsTools.Core.Exceptions;

namespace ArdysaModsTools.Core.Services.Cdn
{
    public static class CdnAuthFailure
    {
        public const string Reason = "CDN rejected this build's request signature";

        public static bool IsUnauthorized(Exception? ex) =>
            ex is HttpRequestException { StatusCode: HttpStatusCode.Unauthorized };

        public static DownloadException ToException(string? url = null) =>
            new(ErrorCodes.DL_CDN_UNAUTHORIZED, Reason, url);
    }
}
