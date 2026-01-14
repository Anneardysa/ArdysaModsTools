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
using NUnit.Framework;
using ArdysaModsTools.Core.Exceptions;

namespace ArdysaModsTools.Tests.Exceptions
{
    /// <summary>
    /// Tests for error codes constants.
    /// </summary>
    [TestFixture]
    public class ErrorCodesTests
    {
        [Test]
        public void VpkErrorCodes_FollowNamingConvention()
        {
            // All VPK error codes should start with "VPK_"
            Assert.That(ErrorCodes.VPK_EXTRACT_FAILED, Does.StartWith("VPK_"));
            Assert.That(ErrorCodes.VPK_RECOMPILE_FAILED, Does.StartWith("VPK_"));
            Assert.That(ErrorCodes.VPK_REPLACE_FAILED, Does.StartWith("VPK_"));
            Assert.That(ErrorCodes.VPK_FILE_NOT_FOUND, Does.StartWith("VPK_"));
            Assert.That(ErrorCodes.VPK_INVALID_FORMAT, Does.StartWith("VPK_"));
            Assert.That(ErrorCodes.VPK_TOOL_NOT_FOUND, Does.StartWith("VPK_"));
        }

        [Test]
        public void DownloadErrorCodes_FollowNamingConvention()
        {
            // All download error codes should start with "DL_"
            Assert.That(ErrorCodes.DL_NETWORK_ERROR, Does.StartWith("DL_"));
            Assert.That(ErrorCodes.DL_TIMEOUT, Does.StartWith("DL_"));
            Assert.That(ErrorCodes.DL_INVALID_URL, Does.StartWith("DL_"));
            Assert.That(ErrorCodes.DL_FILE_NOT_FOUND, Does.StartWith("DL_"));
        }

        [Test]
        public void PatchErrorCodes_FollowNamingConvention()
        {
            // All patch error codes should start with "PATCH_"
            Assert.That(ErrorCodes.PATCH_ITEMS_GAME_FAILED, Does.StartWith("PATCH_"));
            Assert.That(ErrorCodes.PATCH_SIGNATURE_FAILED, Does.StartWith("PATCH_"));
            Assert.That(ErrorCodes.PATCH_BLOCK_NOT_FOUND, Does.StartWith("PATCH_"));
        }

        [Test]
        public void GenerationErrorCodes_FollowNamingConvention()
        {
            // All generation error codes should start with "GEN_"
            Assert.That(ErrorCodes.GEN_FAILED, Does.StartWith("GEN_"));
            Assert.That(ErrorCodes.GEN_HERO_NOT_FOUND, Does.StartWith("GEN_"));
            Assert.That(ErrorCodes.GEN_SET_NOT_FOUND, Does.StartWith("GEN_"));
        }

        [Test]
        public void ConfigErrorCodes_FollowNamingConvention()
        {
            // All config error codes should start with "CFG_"
            Assert.That(ErrorCodes.CFG_DOTA_NOT_FOUND, Does.StartWith("CFG_"));
            Assert.That(ErrorCodes.CFG_INVALID_PATH, Does.StartWith("CFG_"));
            Assert.That(ErrorCodes.CFG_STEAM_NOT_FOUND, Does.StartWith("CFG_"));
        }

        [Test]
        public void AllErrorCodes_AreUnique()
        {
            // Get all error code values using reflection
            var errorCodeType = typeof(ErrorCodes);
            var fields = errorCodeType.GetFields(
                System.Reflection.BindingFlags.Public | 
                System.Reflection.BindingFlags.Static);

            var values = fields
                .Where(f => f.FieldType == typeof(string))
                .Select(f => f.GetValue(null) as string)
                .Where(v => v != null)
                .ToList();

            var uniqueValues = values.Distinct().ToList();

            // All error codes should be unique
            Assert.That(values.Count, Is.EqualTo(uniqueValues.Count), 
                "Duplicate error codes detected");
        }

        [Test]
        public void AllErrorCodes_HaveThreeDigitSuffix()
        {
            // All error codes should follow CATEGORY_XXX pattern
            var errorCodeType = typeof(ErrorCodes);
            var fields = errorCodeType.GetFields(
                System.Reflection.BindingFlags.Public | 
                System.Reflection.BindingFlags.Static);

            foreach (var field in fields.Where(f => f.FieldType == typeof(string)))
            {
                var value = field.GetValue(null) as string;
                Assert.That(value, Does.Match(@"^[A-Z]+_\d{3}$"),
                    $"Error code {field.Name} = '{value}' does not follow CATEGORY_XXX pattern");
            }
        }
    }
}

