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

namespace ArdysaModsTools.Core.Exceptions
{
    public class ArdysaException : Exception
    {
        public string ErrorCode { get; }

        public ArdysaException(string errorCode, string message)
            : base($"[{errorCode}] {message}")
        {
            ErrorCode = errorCode;
        }

        public ArdysaException(string errorCode, string message, Exception innerException)
            : base($"[{errorCode}] {message}", innerException)
        {
            ErrorCode = errorCode;
        }
    }

    public class VpkException : ArdysaException
    {
        public VpkException(string errorCode, string message)
            : base(errorCode, message) { }

        public VpkException(string errorCode, string message, Exception innerException)
            : base(errorCode, message, innerException) { }
    }

    public class DownloadException : ArdysaException
    {
        public string? FailedUrl { get; }

        public DownloadException(string errorCode, string message, string? url = null)
            : base(errorCode, message)
        {
            FailedUrl = url;
        }

        public DownloadException(string errorCode, string message, Exception innerException, string? url = null)
            : base(errorCode, message, innerException)
        {
            FailedUrl = url;
        }
    }

    public class PatchException : ArdysaException
    {
        public string? TargetFile { get; }

        public PatchException(string errorCode, string message, string? targetFile = null)
            : base(errorCode, message)
        {
            TargetFile = targetFile;
        }

        public PatchException(string errorCode, string message, Exception innerException, string? targetFile = null)
            : base(errorCode, message, innerException)
        {
            TargetFile = targetFile;
        }
    }

    public class ConfigurationException : ArdysaException
    {
        public ConfigurationException(string errorCode, string message)
            : base(errorCode, message) { }

        public ConfigurationException(string errorCode, string message, Exception innerException)
            : base(errorCode, message, innerException) { }
    }

    public class GenerationException : ArdysaException
    {
        public string? HeroName { get; }

        public string? SetName { get; }

        public GenerationException(string errorCode, string message, string? heroName = null, string? setName = null)
            : base(errorCode, message)
        {
            HeroName = heroName;
            SetName = setName;
        }

        public GenerationException(string errorCode, string message, Exception innerException, string? heroName = null, string? setName = null)
            : base(errorCode, message, innerException)
        {
            HeroName = heroName;
            SetName = setName;
        }
    }
}

