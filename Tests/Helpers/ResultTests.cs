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
using ArdysaModsTools.Core.Models;
using System;

namespace ArdysaModsTools.Tests.Helpers
{
    [TestFixture]
    public class ResultTests
    {
        #region Success Tests

        [Test]
        public void Success_CreatesSuccessfulResult()
        {
            // Act
            var result = Result<int>.Success(42);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.IsFailure, Is.False);
            Assert.That(result.Value, Is.EqualTo(42));
        }

        [Test]
        public void Success_WithNullValue_CreatesSuccessfulResult()
        {
            // Act
            var result = Result<string?>.Success(null);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Null);
        }

        #endregion

        #region Failure Tests

        [Test]
        public void Failure_CreatesFailedResult()
        {
            // Arrange
            string errorMessage = "Something went wrong";

            // Act
            var result = Result<int>.Failure(errorMessage);

            // Assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.EqualTo(errorMessage));
        }

        [Test]
        public void Failure_WithErrorCode_CreatesFailedResultWithCode()
        {
            // Arrange
            string errorMessage = "Something went wrong";
            string errorCode = "ERR_001";

            // Act
            var result = Result<int>.Failure(errorMessage, errorCode);

            // Assert
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.EqualTo(errorMessage));
            Assert.That(result.ErrorCode, Is.EqualTo(errorCode));
        }

        [Test]
        public void Failure_AccessingValue_ThrowsInvalidOperationException()
        {
            // Arrange
            var result = Result<int>.Failure("error");

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _ = result.Value);
        }

        [Test]
        public void Success_AccessingError_ThrowsInvalidOperationException()
        {
            // Arrange
            var result = Result<int>.Success(42);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _ = result.Error);
        }

        #endregion

        #region Match Tests

        [Test]
        public void Match_OnSuccess_CallsOnSuccessFunction()
        {
            // Arrange
            var result = Result<int>.Success(10);

            // Act
            var matched = result.Match(
                onSuccess: x => $"Value is {x}",
                onFailure: e => $"Error: {e}"
            );

            // Assert
            Assert.That(matched, Is.EqualTo("Value is 10"));
        }

        [Test]
        public void Match_OnFailure_CallsOnFailureFunction()
        {
            // Arrange
            var result = Result<int>.Failure("original error");

            // Act
            var matched = result.Match(
                onSuccess: x => $"Value is {x}",
                onFailure: e => $"Error: {e}"
            );

            // Assert
            Assert.That(matched, Is.EqualTo("Error: original error"));
        }

        #endregion

        #region OnSuccess/OnFailure Tests

        [Test]
        public void OnSuccess_WhenSuccessful_ExecutesAction()
        {
            // Arrange
            var result = Result<int>.Success(42);
            int capturedValue = 0;

            // Act
            result.OnSuccess(v => capturedValue = v);

            // Assert
            Assert.That(capturedValue, Is.EqualTo(42));
        }

        [Test]
        public void OnSuccess_WhenFailure_DoesNotExecuteAction()
        {
            // Arrange
            var result = Result<int>.Failure("error");
            bool wasExecuted = false;

            // Act
            result.OnSuccess(_ => wasExecuted = true);

            // Assert
            Assert.That(wasExecuted, Is.False);
        }

        [Test]
        public void OnFailure_WhenFailure_ExecutesAction()
        {
            // Arrange
            var result = Result<int>.Failure("test error");
            string? capturedError = null;

            // Act
            result.OnFailure(e => capturedError = e);

            // Assert
            Assert.That(capturedError, Is.EqualTo("test error"));
        }

        [Test]
        public void OnFailure_WhenSuccess_DoesNotExecuteAction()
        {
            // Arrange
            var result = Result<int>.Success(42);
            bool wasExecuted = false;

            // Act
            result.OnFailure(_ => wasExecuted = true);

            // Assert
            Assert.That(wasExecuted, Is.False);
        }

        #endregion

        #region GetValueOrDefault Tests

        [Test]
        public void GetValueOrDefault_OnSuccess_ReturnsValue()
        {
            // Arrange
            var result = Result<int>.Success(42);

            // Act
            var value = result.GetValueOrDefault(0);

            // Assert
            Assert.That(value, Is.EqualTo(42));
        }

        [Test]
        public void GetValueOrDefault_OnFailure_ReturnsDefault()
        {
            // Arrange
            var result = Result<int>.Failure("error");

            // Act
            var value = result.GetValueOrDefault(99);

            // Assert
            Assert.That(value, Is.EqualTo(99));
        }

        #endregion

        #region Implicit Conversion Tests

        [Test]
        public void ImplicitConversion_FromValue_CreatesSuccess()
        {
            // Act
            Result<string> result = "hello";

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo("hello"));
        }

        #endregion

        #region ToString Tests

        [Test]
        public void ToString_OnSuccess_ReturnsSuccessString()
        {
            // Arrange
            var result = Result<int>.Success(42);

            // Act
            var str = result.ToString();

            // Assert
            Assert.That(str, Is.EqualTo("Success(42)"));
        }

        [Test]
        public void ToString_OnFailure_ReturnsFailureString()
        {
            // Arrange
            var result = Result<int>.Failure("test error");

            // Act
            var str = result.ToString();

            // Assert
            Assert.That(str, Is.EqualTo("Failure(test error)"));
        }

        #endregion
    }
}
