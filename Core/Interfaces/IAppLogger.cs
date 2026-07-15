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
    public enum LogLevel
    {
        Debug,
        
        Info,
        
        Warning,
        
        Error
    }

    public interface IAppLogger
    {
        void Log(string message, LogLevel level = LogLevel.Info);

        void LogError(string message, Exception? ex = null);

        void LogWarning(string message);

        void LogDebug(string message);

        void FlushBufferedLogs();
    }
}
