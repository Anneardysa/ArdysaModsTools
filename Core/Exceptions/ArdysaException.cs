using System;

namespace ArdysaModsTools.Core.Exceptions
{
    /// <summary>
    /// Base exception for all ArdysaModsTools errors.
    /// Provides consistent error handling with error codes for debugging and logging.
    /// </summary>
    public class ArdysaException : Exception
    {
        /// <summary>
        /// Gets the error code associated with this exception.
        /// Error codes follow the pattern: CATEGORY_XXX (e.g., VPK_001, DL_002)
        /// </summary>
        public string ErrorCode { get; }

        /// <summary>
        /// Creates a new ArdysaException with the specified error code and message.
        /// </summary>
        /// <param name="errorCode">Error code from <see cref="ErrorCodes"/> class</param>
        /// <param name="message">Human-readable error message</param>
        public ArdysaException(string errorCode, string message)
            : base($"[{errorCode}] {message}")
        {
            ErrorCode = errorCode;
        }

        /// <summary>
        /// Creates a new ArdysaException with the specified error code, message, and inner exception.
        /// </summary>
        /// <param name="errorCode">Error code from <see cref="ErrorCodes"/> class</param>
        /// <param name="message">Human-readable error message</param>
        /// <param name="innerException">The exception that caused this error</param>
        public ArdysaException(string errorCode, string message, Exception innerException)
            : base($"[{errorCode}] {message}", innerException)
        {
            ErrorCode = errorCode;
        }
    }

    /// <summary>
    /// Exception thrown when VPK operations fail (extraction, recompilation, replacement).
    /// </summary>
    public class VpkException : ArdysaException
    {
        /// <summary>
        /// Creates a new VpkException.
        /// </summary>
        /// <param name="errorCode">VPK error code (VPK_XXX)</param>
        /// <param name="message">Description of the VPK error</param>
        public VpkException(string errorCode, string message)
            : base(errorCode, message) { }

        /// <summary>
        /// Creates a new VpkException with an inner exception.
        /// </summary>
        public VpkException(string errorCode, string message, Exception innerException)
            : base(errorCode, message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when download operations fail (network errors, timeouts, invalid URLs).
    /// </summary>
    public class DownloadException : ArdysaException
    {
        /// <summary>
        /// Gets the URL that failed to download, if available.
        /// </summary>
        public string? FailedUrl { get; }

        /// <summary>
        /// Creates a new DownloadException.
        /// </summary>
        /// <param name="errorCode">Download error code (DL_XXX)</param>
        /// <param name="message">Description of the download error</param>
        /// <param name="url">The URL that failed to download</param>
        public DownloadException(string errorCode, string message, string? url = null)
            : base(errorCode, message)
        {
            FailedUrl = url;
        }

        /// <summary>
        /// Creates a new DownloadException with an inner exception.
        /// </summary>
        public DownloadException(string errorCode, string message, Exception innerException, string? url = null)
            : base(errorCode, message, innerException)
        {
            FailedUrl = url;
        }
    }

    /// <summary>
    /// Exception thrown when patching operations fail (items_game.txt, signatures, etc.).
    /// </summary>
    public class PatchException : ArdysaException
    {
        /// <summary>
        /// Gets the file that failed to patch, if available.
        /// </summary>
        public string? TargetFile { get; }

        /// <summary>
        /// Creates a new PatchException.
        /// </summary>
        /// <param name="errorCode">Patch error code (PATCH_XXX)</param>
        /// <param name="message">Description of the patch error</param>
        /// <param name="targetFile">The file that failed to patch</param>
        public PatchException(string errorCode, string message, string? targetFile = null)
            : base(errorCode, message)
        {
            TargetFile = targetFile;
        }

        /// <summary>
        /// Creates a new PatchException with an inner exception.
        /// </summary>
        public PatchException(string errorCode, string message, Exception innerException, string? targetFile = null)
            : base(errorCode, message, innerException)
        {
            TargetFile = targetFile;
        }
    }

    /// <summary>
    /// Exception thrown when configuration or detection operations fail.
    /// </summary>
    public class ConfigurationException : ArdysaException
    {
        /// <summary>
        /// Creates a new ConfigurationException.
        /// </summary>
        /// <param name="errorCode">Configuration error code (CFG_XXX)</param>
        /// <param name="message">Description of the configuration error</param>
        public ConfigurationException(string errorCode, string message)
            : base(errorCode, message) { }

        /// <summary>
        /// Creates a new ConfigurationException with an inner exception.
        /// </summary>
        public ConfigurationException(string errorCode, string message, Exception innerException)
            : base(errorCode, message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when hero set generation operations fail.
    /// </summary>
    public class GenerationException : ArdysaException
    {
        /// <summary>
        /// Gets the hero name that failed, if applicable.
        /// </summary>
        public string? HeroName { get; }

        /// <summary>
        /// Gets the set name that failed, if applicable.
        /// </summary>
        public string? SetName { get; }

        /// <summary>
        /// Creates a new GenerationException.
        /// </summary>
        /// <param name="errorCode">Generation error code (GEN_XXX)</param>
        /// <param name="message">Description of the generation error</param>
        /// <param name="heroName">The hero that failed to generate</param>
        /// <param name="setName">The set that failed to generate</param>
        public GenerationException(string errorCode, string message, string? heroName = null, string? setName = null)
            : base(errorCode, message)
        {
            HeroName = heroName;
            SetName = setName;
        }

        /// <summary>
        /// Creates a new GenerationException with an inner exception.
        /// </summary>
        public GenerationException(string errorCode, string message, Exception innerException, string? heroName = null, string? setName = null)
            : base(errorCode, message, innerException)
        {
            HeroName = heroName;
            SetName = setName;
        }
    }
}
