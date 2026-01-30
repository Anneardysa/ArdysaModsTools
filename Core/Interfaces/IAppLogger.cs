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

namespace ArdysaModsTools.Core.Interfaces
{
    /// <summary>
    /// Log severity levels for categorizing messages.
    /// </summary>
    public enum LogLevel
    {
        /// <summary>Detailed diagnostic information for debugging.</summary>
        Debug,
        
        /// <summary>General informational messages.</summary>
        Info,
        
        /// <summary>Warnings that don't stop execution but indicate potential issues.</summary>
        Warning,
        
        /// <summary>Errors that may affect functionality.</summary>
        Error
    }

    /// <summary>
    /// Abstracted logging interface for dependency injection and testability.
    /// Decouples logging from UI controls (RichTextBox, RetroTerminal).
    /// </summary>
    /// <remarks>
    /// Implementations:
    /// - <see cref="Core.Services.Logger"/>: UI-bound logger for MainForm
    /// - <see cref="Core.Services.NullLogger"/>: No-op logger for DI defaults
    /// - TestLogger: Captures messages for test assertions
    /// </remarks>
    public interface IAppLogger
    {
        /// <summary>
        /// Logs a message with the specified severity level.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="level">Severity level (default: Info).</param>
        void Log(string message, LogLevel level = LogLevel.Info);

        /// <summary>
        /// Logs an error message with optional exception details.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="ex">Optional exception for additional context.</param>
        void LogError(string message, Exception? ex = null);

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The warning message.</param>
        void LogWarning(string message);

        /// <summary>
        /// Logs a debug message (typically only shown in development).
        /// </summary>
        /// <param name="message">The debug message.</param>
        void LogDebug(string message);

        /// <summary>
        /// Flushes any buffered log messages to the output.
        /// Used when logger buffers messages before UI is ready.
        /// </summary>
        void FlushBufferedLogs();
    }
}
