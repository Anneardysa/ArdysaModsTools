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
using ArdysaModsTools.Core.Controllers;
using ArdysaModsTools.Core.Exceptions;
using ArdysaModsTools.Core.Models;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class MiscSelfHealTests
    {
        [Test]
        public void ShouldRebuildClean_UnreadablePackage_Recovers()
        {
            var result = new OperationResult
            {
                Success = false,
                Message = "Could not read your existing mod package.",
                ErrorCode = ErrorCodes.VPK_EXTRACT_FAILED
            };

            Assert.That(MiscController.ShouldRebuildClean(result), Is.True);
        }

        [Test]
        public void ShouldRebuildClean_Success_DoesNotRebuild()
        {
            Assert.That(MiscController.ShouldRebuildClean(OperationResult.Ok()), Is.False);
        }

        [Test]
        public void ShouldRebuildClean_UserCanceled_DoesNotRebuild()
        {
            var canceled = new OperationResult
            {
                Success = false,
                WasCanceled = true,
                ErrorCode = ErrorCodes.VPK_EXTRACT_FAILED
            };

            Assert.That(MiscController.ShouldRebuildClean(canceled), Is.False);
        }

        [Test]
        public void ShouldRebuildClean_ConflictResolution_DoesNotRebuild()
        {
            var needsResolution = new OperationResult
            {
                Success = false,
                RequiresConflictResolution = true,
                ErrorCode = ErrorCodes.VPK_EXTRACT_FAILED
            };

            Assert.That(MiscController.ShouldRebuildClean(needsResolution), Is.False);
        }

        [TestCase(ErrorCodes.MISC_APPLY_FAILED)]
        [TestCase(ErrorCodes.VPK_RECOMPILE_FAILED)]
        [TestCase(ErrorCodes.VPK_REPLACE_FAILED)]
        [TestCase(ErrorCodes.VPK_TOOL_NOT_FOUND)]
        [TestCase(null)]
        public void ShouldRebuildClean_OtherFailures_DoNotRebuild(string? errorCode)
        {
            var result = new OperationResult
            {
                Success = false,
                Message = "failed",
                ErrorCode = errorCode
            };

            Assert.That(MiscController.ShouldRebuildClean(result), Is.False);
        }
    }
}
