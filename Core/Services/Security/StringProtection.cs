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
using System.Security.Cryptography;
using System.Text;

namespace ArdysaModsTools.Core.Services.Security
{
    /// <summary>
    /// Provides AES-256 encryption and decryption for sensitive strings.
    /// Uses PBKDF2 for key derivation with machine-specific entropy.
    /// </summary>
    public static class StringProtection
    {
        // Encryption parameters
        private const int KeySize = 256;
        private const int BlockSize = 128;
        private const int Iterations = 10000;
        private const int SaltSize = 16;
        private const int IvSize = 16;

        // Machine-bound entropy (makes decryption harder on different machines)
        private static readonly byte[] MachineEntropy = Encoding.UTF8.GetBytes(
            Environment.MachineName + Environment.UserName + "AMT2.0"
        );

        /// <summary>
        /// Decrypts a Base64-encoded encrypted string.
        /// </summary>
        /// <param name="encryptedBase64">The encrypted string in Base64 format.</param>
        /// <returns>The decrypted plain text.</returns>
        public static string Decrypt(string encryptedBase64)
        {
            if (string.IsNullOrEmpty(encryptedBase64))
                return string.Empty;

            try
            {
                byte[] fullCipher = Convert.FromBase64String(encryptedBase64);

                // Extract salt (first 16 bytes)
                byte[] salt = new byte[SaltSize];
                Array.Copy(fullCipher, 0, salt, 0, SaltSize);

                // Extract IV (next 16 bytes)
                byte[] iv = new byte[IvSize];
                Array.Copy(fullCipher, SaltSize, iv, 0, IvSize);

                // Extract cipher text (remaining bytes)
                int cipherLength = fullCipher.Length - SaltSize - IvSize;
                byte[] cipherText = new byte[cipherLength];
                Array.Copy(fullCipher, SaltSize + IvSize, cipherText, 0, cipherLength);

                // Derive key using PBKDF2
                using var keyDerivation = new Rfc2898DeriveBytes(
                    MachineEntropy, salt, Iterations, HashAlgorithmName.SHA256);
                byte[] key = keyDerivation.GetBytes(KeySize / 8);

                using var aes = Aes.Create();
                aes.KeySize = KeySize;
                aes.BlockSize = BlockSize;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = key;
                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor();
                using var ms = new MemoryStream(cipherText);
                using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                using var reader = new StreamReader(cs, Encoding.UTF8);

                return reader.ReadToEnd();
            }
            catch
            {
                // On decryption failure, return empty (prevents crashes)
                return string.Empty;
            }
        }

        /// <summary>
        /// Encrypts a plain text string to Base64-encoded cipher text.
        /// Used during build/setup to generate encrypted values.
        /// </summary>
        /// <param name="plainText">The plain text to encrypt.</param>
        /// <returns>Base64-encoded encrypted string.</returns>
        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            // Generate random salt and IV
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] iv = RandomNumberGenerator.GetBytes(IvSize);

            // Derive key using PBKDF2
            using var keyDerivation = new Rfc2898DeriveBytes(
                MachineEntropy, salt, Iterations, HashAlgorithmName.SHA256);
            byte[] key = keyDerivation.GetBytes(KeySize / 8);

            using var aes = Aes.Create();
            aes.KeySize = KeySize;
            aes.BlockSize = BlockSize;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = key;
            aes.IV = iv;

            using var encryptor = aes.CreateEncryptor();
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] cipherText = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            // Combine: salt + IV + cipherText
            byte[] result = new byte[SaltSize + IvSize + cipherText.Length];
            Array.Copy(salt, 0, result, 0, SaltSize);
            Array.Copy(iv, 0, result, SaltSize, IvSize);
            Array.Copy(cipherText, 0, result, SaltSize + IvSize, cipherText.Length);

            return Convert.ToBase64String(result);
        }

        /// <summary>
        /// Decrypts using a portable key (not machine-bound).
        /// Use this for values that need to work across different machines.
        /// </summary>
        /// <param name="encryptedBase64">The encrypted string in Base64 format.</param>
        /// <param name="passphrase">The passphrase used for encryption.</param>
        /// <returns>The decrypted plain text.</returns>
        public static string DecryptPortable(string encryptedBase64, string passphrase)
        {
            if (string.IsNullOrEmpty(encryptedBase64))
                return string.Empty;

            try
            {
                byte[] fullCipher = Convert.FromBase64String(encryptedBase64);

                byte[] salt = new byte[SaltSize];
                Array.Copy(fullCipher, 0, salt, 0, SaltSize);

                byte[] iv = new byte[IvSize];
                Array.Copy(fullCipher, SaltSize, iv, 0, IvSize);

                int cipherLength = fullCipher.Length - SaltSize - IvSize;
                byte[] cipherText = new byte[cipherLength];
                Array.Copy(fullCipher, SaltSize + IvSize, cipherText, 0, cipherLength);

                using var keyDerivation = new Rfc2898DeriveBytes(
                    Encoding.UTF8.GetBytes(passphrase), salt, Iterations, HashAlgorithmName.SHA256);
                byte[] key = keyDerivation.GetBytes(KeySize / 8);

                using var aes = Aes.Create();
                aes.KeySize = KeySize;
                aes.BlockSize = BlockSize;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = key;
                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor();
                using var ms = new MemoryStream(cipherText);
                using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                using var reader = new StreamReader(cs, Encoding.UTF8);

                return reader.ReadToEnd();
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Encrypts using a portable key (not machine-bound).
        /// Use this for values that need to work across different machines.
        /// </summary>
        public static string EncryptPortable(string plainText, string passphrase)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] iv = RandomNumberGenerator.GetBytes(IvSize);

            using var keyDerivation = new Rfc2898DeriveBytes(
                Encoding.UTF8.GetBytes(passphrase), salt, Iterations, HashAlgorithmName.SHA256);
            byte[] key = keyDerivation.GetBytes(KeySize / 8);

            using var aes = Aes.Create();
            aes.KeySize = KeySize;
            aes.BlockSize = BlockSize;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = key;
            aes.IV = iv;

            using var encryptor = aes.CreateEncryptor();
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] cipherText = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            byte[] result = new byte[SaltSize + IvSize + cipherText.Length];
            Array.Copy(salt, 0, result, 0, SaltSize);
            Array.Copy(iv, 0, result, SaltSize, IvSize);
            Array.Copy(cipherText, 0, result, SaltSize + IvSize, cipherText.Length);

            return Convert.ToBase64String(result);
        }
    }
}

