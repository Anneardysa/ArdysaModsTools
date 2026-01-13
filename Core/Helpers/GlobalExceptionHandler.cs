using System;
using System.Windows.Forms;
using ArdysaModsTools.Core.Exceptions;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Helpers;

namespace ArdysaModsTools.Core.Helpers
{
    /// <summary>
    /// Centralized exception handler for consistent error handling across the application.
    /// </summary>
    public static class GlobalExceptionHandler
    {
        private static ILogger? _logger;

        /// <summary>
        /// Initializes the global exception handler with a logger.
        /// </summary>
        public static void Initialize(ILogger logger)
        {
            _logger = logger;
            
            // Wire up global exception handlers
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        /// <summary>
        /// Handles an exception with appropriate logging and user notification.
        /// </summary>
        /// <param name="ex">The exception to handle.</param>
        /// <param name="showDialog">Whether to show a dialog to the user.</param>
        /// <returns>True if the exception was handled, false otherwise.</returns>
        public static bool Handle(Exception ex, bool showDialog = true)
        {
            if (ex == null) return false;

            // Handle cancellation silently
            if (ex is OperationCanceledException)
            {
                _logger?.Log("Operation was cancelled.");
                return true;
            }

            // Handle ArdysaException with structured error info
            if (ex is ArdysaException ardysaEx)
            {
                return HandleArdysaException(ardysaEx, showDialog);
            }

            // Handle generic exceptions
            return HandleGenericException(ex, showDialog);
        }

        /// <summary>
        /// Handles an ArdysaException with error code and structured logging.
        /// </summary>
        private static bool HandleArdysaException(ArdysaException ex, bool showDialog)
        {
            string errorCode = ex.ErrorCode;
            string message = ex.Message;

            // Log with error code
            _logger?.Log($"Error [{errorCode}]: {message}");
            FallbackLogger.Log($"[{errorCode}] {message}\n{ex.StackTrace}");

            if (!showDialog) return true;

            // Show user-friendly message based on exception type
            string userMessage = GetUserFriendlyMessage(ex);
            string title = GetErrorTitle(ex);

            UIHelpers.ShowError(userMessage, title);
            return true;
        }

        /// <summary>
        /// Handles a generic exception with logging.
        /// </summary>
        private static bool HandleGenericException(Exception ex, bool showDialog)
        {
            _logger?.Log($"Error: {ex.Message}");
            FallbackLogger.Log($"[UNEXPECTED] {ex.Message}\n{ex.StackTrace}");

            if (!showDialog) return true;

            UIHelpers.ShowError(
                "An unexpected error occurred. Please check the log for details.",
                "Error");
            return true;
        }

        /// <summary>
        /// Gets a user-friendly error message for the exception.
        /// </summary>
        private static string GetUserFriendlyMessage(ArdysaException ex)
        {
            return ex switch
            {
                VpkException vpkEx => vpkEx.ErrorCode switch
                {
                    ErrorCodes.VPK_FILE_NOT_FOUND => "The VPK file could not be found.",
                    ErrorCodes.VPK_INVALID_FORMAT => "The VPK file format is invalid.",
                    ErrorCodes.VPK_EXTRACT_FAILED => "Failed to extract VPK contents.",
                    ErrorCodes.VPK_RECOMPILE_FAILED => "Failed to recompile VPK file.",
                    _ => $"VPK operation failed: {ex.Message}"
                },

                DownloadException dlEx => dlEx.ErrorCode switch
                {
                    ErrorCodes.DL_NETWORK_ERROR => "Network error. Please check your internet connection.",
                    ErrorCodes.DL_TIMEOUT => "Download timed out. Please try again.",
                    ErrorCodes.DL_HASH_MISMATCH => "Downloaded file verification failed. Please try again.",
                    ErrorCodes.DL_SERVER_ERROR => "Server error. Please try again later.",
                    _ => $"Download failed: {ex.Message}"
                },

                PatchException patchEx => patchEx.ErrorCode switch
                {
                    ErrorCodes.PATCH_BLOCK_NOT_FOUND => "Required game files not found.",
                    ErrorCodes.PATCH_WRITE_FAILED => "Failed to write patch files. Check file permissions.",
                    ErrorCodes.PATCH_SIGNATURE_FAILED => "Failed to patch game signatures.",
                    _ => $"Patch operation failed: {ex.Message}"
                },

                ConfigurationException cfgEx => cfgEx.ErrorCode switch
                {
                    ErrorCodes.CFG_READ_FAILED => "Failed to load configuration.",
                    ErrorCodes.CFG_WRITE_FAILED => "Failed to save configuration.",
                    ErrorCodes.CFG_INVALID_PATH => "Invalid path specified.",
                    _ => $"Configuration error: {ex.Message}"
                },

                GenerationException genEx => genEx.ErrorCode switch
                {
                    ErrorCodes.GEN_HERO_NOT_FOUND => "Hero data not found.",
                    ErrorCodes.GEN_INDEX_NOT_FOUND => "Required asset files are missing.",
                    ErrorCodes.GEN_MERGE_FAILED => "Failed to merge hero assets.",
                    _ => $"Generation failed: {ex.Message}"
                },

                _ => ex.Message
            };
        }

        /// <summary>
        /// Gets an appropriate error dialog title for the exception type.
        /// </summary>
        private static string GetErrorTitle(ArdysaException ex)
        {
            return ex switch
            {
                VpkException => "VPK Error",
                DownloadException => "Download Error",
                PatchException => "Patch Error",
                ConfigurationException => "Configuration Error",
                GenerationException => "Generation Error",
                _ => "Error"
            };
        }

        /// <summary>
        /// Handles thread exceptions from Windows Forms.
        /// </summary>
        private static void OnThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            Handle(e.Exception, showDialog: true);
        }

        /// <summary>
        /// Handles unhandled domain exceptions.
        /// </summary>
        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                Handle(ex, showDialog: true);
            }
        }
    }
}
