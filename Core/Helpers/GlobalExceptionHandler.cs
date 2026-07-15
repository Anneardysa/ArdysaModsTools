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
using System.Windows.Forms;
using ArdysaModsTools.Core.Exceptions;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Localization;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Helpers;

namespace ArdysaModsTools.Core.Helpers
{
    public static class GlobalExceptionHandler
    {
        private static IAppLogger? _logger;
        private static bool _initialized;

        public static void Initialize(IAppLogger logger, bool wireEvents = true)
        {
            _logger = logger;
            
            if (_initialized || !wireEvents) return;
            _initialized = true;
            
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        public static bool Handle(Exception ex, bool showDialog = true)
        {
            if (ex == null) return false;

            if (ex is OperationCanceledException)
            {
                _logger?.Log("Operation was cancelled.");
                return true;
            }

            if (ex is ArdysaException ardysaEx)
            {
                return HandleArdysaException(ardysaEx, showDialog);
            }

            return HandleGenericException(ex, showDialog);
        }

        private static bool HandleArdysaException(ArdysaException ex, bool showDialog)
        {
            string errorCode = ex.ErrorCode;
            string message = ex.Message;

            _logger?.Log($"Error [{errorCode}]: {message}");
            FallbackLogger.Log($"[{errorCode}] {message}\n{ex.StackTrace}");

            if (!showDialog) return true;

            string userMessage = GetUserFriendlyMessage(ex);
            string title = GetErrorTitle(ex);

            UIHelpers.ShowError(userMessage, title);
            return true;
        }

        private static bool HandleGenericException(Exception ex, bool showDialog)
        {
            _logger?.Log($"Error: {ex.Message}");
            FallbackLogger.Log($"[UNEXPECTED] {ex.Message}\n{ex.StackTrace}");

            if (!showDialog) return true;

            UIHelpers.ShowError(Loc.T("error.unexpected"), Loc.T("common.error"));
            return true;
        }

        private static string GetUserFriendlyMessage(ArdysaException ex)
        {
            return ex switch
            {
                VpkException vpkEx => vpkEx.ErrorCode switch
                {
                    ErrorCodes.VPK_FILE_NOT_FOUND => Loc.T("error.vpk.fileNotFound"),
                    ErrorCodes.VPK_INVALID_FORMAT => Loc.T("error.vpk.invalidFormat"),
                    ErrorCodes.VPK_EXTRACT_FAILED => Loc.T("error.vpk.extractFailed"),
                    ErrorCodes.VPK_RECOMPILE_FAILED => Loc.T("error.vpk.recompileFailed"),
                    _ => Loc.T("error.vpk.generic", new { message = ex.Message })
                },

                DownloadException dlEx => dlEx.ErrorCode switch
                {
                    ErrorCodes.DL_NETWORK_ERROR => Loc.T("error.dl.network"),
                    ErrorCodes.DL_TIMEOUT => Loc.T("error.dl.timeout"),
                    ErrorCodes.DL_HASH_MISMATCH => Loc.T("error.dl.hashMismatch"),
                    ErrorCodes.DL_SERVER_ERROR => Loc.T("error.dl.server"),
                    _ => Loc.T("error.dl.generic", new { message = ex.Message })
                },

                PatchException patchEx => patchEx.ErrorCode switch
                {
                    ErrorCodes.PATCH_BLOCK_NOT_FOUND => Loc.T("error.patch.blockNotFound"),
                    ErrorCodes.PATCH_WRITE_FAILED => Loc.T("error.patch.writeFailed"),
                    ErrorCodes.PATCH_SIGNATURE_FAILED => Loc.T("error.patch.signatureFailed"),
                    _ => Loc.T("error.patch.generic", new { message = ex.Message })
                },

                ConfigurationException cfgEx => cfgEx.ErrorCode switch
                {
                    ErrorCodes.CFG_READ_FAILED => Loc.T("error.cfg.readFailed"),
                    ErrorCodes.CFG_WRITE_FAILED => Loc.T("error.cfg.writeFailed"),
                    ErrorCodes.CFG_INVALID_PATH => Loc.T("error.cfg.invalidPath"),
                    _ => Loc.T("error.cfg.generic", new { message = ex.Message })
                },

                GenerationException genEx => genEx.ErrorCode switch
                {
                    ErrorCodes.GEN_HERO_NOT_FOUND => Loc.T("error.gen.heroNotFound"),
                    ErrorCodes.GEN_INDEX_NOT_FOUND => Loc.T("error.gen.indexNotFound"),
                    ErrorCodes.GEN_MERGE_FAILED => Loc.T("error.gen.mergeFailed"),
                    _ => Loc.T("error.gen.generic", new { message = ex.Message })
                },

                _ => ex.Message
            };
        }

        private static string GetErrorTitle(ArdysaException ex)
        {
            return ex switch
            {
                VpkException => Loc.T("error.title.vpk"),
                DownloadException => Loc.T("error.title.download"),
                PatchException => Loc.T("error.title.patch"),
                ConfigurationException => Loc.T("error.title.config"),
                GenerationException => Loc.T("error.title.generation"),
                _ => Loc.T("common.error")
            };
        }

        private static void OnThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            Handle(e.Exception, showDialog: true);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                Handle(ex, showDialog: true);
            }
        }
    }
}

