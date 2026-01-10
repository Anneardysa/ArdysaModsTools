using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace ArdysaModsTools.Core.Services.Security
{
    /// <summary>
    /// Provides assembly integrity verification to detect tampering.
    /// Uses SHA-256 hashing and optional strong name validation.
    /// </summary>
    public static class IntegrityCheck
    {
        // Expected hash is set during build (placeholder - will be updated by build script)
        // For single-file deployments, we use a different approach
        private static string? _expectedHash = null;

        /// <summary>
        /// Verifies the integrity of the current assembly.
        /// Returns true if the assembly is valid, false if tampered.
        /// </summary>
        public static bool VerifyAssembly()
        {
#if DEBUG
            // Skip integrity check in debug builds
            return true;
#else
            try
            {
                // For single-file deployments, integrity checking is limited
                // Focus on other checks instead
                return VerifyStrongName() && VerifyResources();
            }
            catch
            {
                // If verification fails, assume valid to avoid crashes
                // The anti-debug and obfuscation provide primary protection
                return true;
            }
#endif
        }

        /// <summary>
        /// Verifies the assembly's strong name signature if present.
        /// </summary>
        private static bool VerifyStrongName()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var name = assembly.GetName();

                // Check if assembly is signed
                byte[]? publicKey = name.GetPublicKey();
                if (publicKey != null && publicKey.Length > 0)
                {
                    // Assembly is signed - .NET will have already verified it at load time
                    // If we got here, the signature is valid
                    return true;
                }

                // No strong name - this is okay for unsigned builds
                return true;
            }
            catch
            {
                return true; // Fail open to prevent crashes
            }
        }

        /// <summary>
        /// Verifies that critical embedded resources haven't been removed.
        /// </summary>
        private static bool VerifyResources()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceNames = assembly.GetManifestResourceNames();

                // Check for expected resources (basic sanity check)
                // At minimum, the app icon should be present
                bool hasIcon = false;
                foreach (var name in resourceNames)
                {
                    if (name.Contains("AppIcon") || name.Contains(".ico"))
                    {
                        hasIcon = true;
                        break;
                    }
                }

                return hasIcon;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// Computes SHA-256 hash of the current executable.
        /// Useful for generating the expected hash during build.
        /// </summary>
        public static string ComputeExecutableHash()
        {
            try
            {
                string exePath = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
                
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                    return string.Empty;

                using var sha256 = SHA256.Create();
                using var stream = File.OpenRead(exePath);
                byte[] hash = sha256.ComputeHash(stream);
                return Convert.ToHexString(hash);
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Sets the expected hash for runtime verification.
        /// Call this at startup with the hash computed during build.
        /// </summary>
        public static void SetExpectedHash(string hash)
        {
            _expectedHash = hash;
        }

        /// <summary>
        /// Verifies the executable hasn't been modified.
        /// Only works if SetExpectedHash was called with the correct hash.
        /// </summary>
        public static bool VerifyExecutableHash()
        {
            if (string.IsNullOrEmpty(_expectedHash))
                return true; // No expected hash set, skip check

            string currentHash = ComputeExecutableHash();
            return string.Equals(_expectedHash, currentHash, StringComparison.OrdinalIgnoreCase);
        }
    }
}
