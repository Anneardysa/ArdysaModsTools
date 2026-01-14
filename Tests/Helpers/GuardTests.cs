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
using ArdysaModsTools.Core.Helpers;
using System;

namespace ArdysaModsTools.Tests.Helpers
{
    [TestFixture]
    public class GuardTests
    {
        #region NotNull Tests

        [Test]
        public void NotNull_WithNullValue_ThrowsArgumentNullException()
        {
            // Arrange
            string? nullValue = null;

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => Guard.NotNull(nullValue));
            Assert.That(ex!.ParamName, Is.EqualTo("nullValue"));
        }

        [Test]
        public void NotNull_WithValue_DoesNotThrow()
        {
            // Arrange
            string value = "test";

            // Act & Assert
            Assert.DoesNotThrow(() => Guard.NotNull(value));
        }

        [Test]
        public void NotNull_WithCustomParamName_UsesProvidedName()
        {
            // Arrange
            object? nullValue = null;

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => Guard.NotNull(nullValue, "customParam"));
            Assert.That(ex!.ParamName, Is.EqualTo("customParam"));
        }

        #endregion

        #region NotNullOrEmpty Tests

        [Test]
        public void NotNullOrEmpty_WithNull_ThrowsArgumentException()
        {
            // Arrange
            string? nullValue = null;

            // Act & Assert
            Assert.Throws<ArgumentException>(() => Guard.NotNullOrEmpty(nullValue));
        }

        [Test]
        public void NotNullOrEmpty_WithEmpty_ThrowsArgumentException()
        {
            // Arrange
            string empty = "";

            // Act & Assert
            Assert.Throws<ArgumentException>(() => Guard.NotNullOrEmpty(empty));
        }

        [Test]
        public void NotNullOrEmpty_WithWhitespace_ThrowsArgumentException()
        {
            // Arrange
            string whitespace = "   ";

            // Act & Assert
            Assert.Throws<ArgumentException>(() => Guard.NotNullOrEmpty(whitespace));
        }

        [Test]
        public void NotNullOrEmpty_WithValidString_DoesNotThrow()
        {
            // Arrange
            string value = "test value";

            // Act & Assert
            Assert.DoesNotThrow(() => Guard.NotNullOrEmpty(value));
        }

        #endregion

        #region GreaterThan Tests

        [Test]
        public void GreaterThan_WithValueGreater_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => Guard.GreaterThan(10, 5));
        }

        [Test]
        public void GreaterThan_WithValueEqual_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => Guard.GreaterThan(5, 5));
        }

        [Test]
        public void GreaterThan_WithValueLess_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => Guard.GreaterThan(3, 5));
        }

        #endregion

        #region InRange Tests

        [Test]
        public void InRange_WithValueInRange_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => Guard.InRange(5, 1, 10));
        }

        [Test]
        public void InRange_AtMinBoundary_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => Guard.InRange(1, 1, 10));
        }

        [Test]
        public void InRange_AtMaxBoundary_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => Guard.InRange(10, 1, 10));
        }

        [Test]
        public void InRange_BelowMin_ThrowsArgumentOutOfRangeException()
        {
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => Guard.InRange(0, 1, 10));
        }

        [Test]
        public void InRange_AboveMax_ThrowsArgumentOutOfRangeException()
        {
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => Guard.InRange(11, 1, 10));
        }

        #endregion

        #region FileExists Tests

        [Test]
        public void FileExists_WithNonExistentFile_ThrowsFileNotFoundException()
        {
            // Arrange
            string fakePath = @"C:\this\path\does\not\exist\file.txt";

            // Act & Assert
            Assert.Throws<System.IO.FileNotFoundException>(() => Guard.FileExists(fakePath));
        }

        #endregion
    }
}

