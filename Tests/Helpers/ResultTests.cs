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
            var result = Result<int>.Success(42);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.IsFailure, Is.False);
            Assert.That(result.Value, Is.EqualTo(42));
        }

        [Test]
        public void Success_WithNullValue_CreatesSuccessfulResult()
        {
            var result = Result<string?>.Success(null);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Null);
        }

        #endregion

        #region Failure Tests

        [Test]
        public void Failure_CreatesFailedResult()
        {
            string errorMessage = "Something went wrong";

            var result = Result<int>.Failure(errorMessage);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.EqualTo(errorMessage));
        }

        [Test]
        public void Failure_WithErrorCode_CreatesFailedResultWithCode()
        {
            string errorMessage = "Something went wrong";
            string errorCode = "ERR_001";

            var result = Result<int>.Failure(errorMessage, errorCode);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.EqualTo(errorMessage));
            Assert.That(result.ErrorCode, Is.EqualTo(errorCode));
        }

        [Test]
        public void Failure_AccessingValue_ThrowsInvalidOperationException()
        {
            var result = Result<int>.Failure("error");

            Assert.Throws<InvalidOperationException>(() => _ = result.Value);
        }

        [Test]
        public void Success_AccessingError_ThrowsInvalidOperationException()
        {
            var result = Result<int>.Success(42);

            Assert.Throws<InvalidOperationException>(() => _ = result.Error);
        }

        #endregion

        #region Match Tests

        [Test]
        public void Match_OnSuccess_CallsOnSuccessFunction()
        {
            var result = Result<int>.Success(10);

            var matched = result.Match(
                onSuccess: x => $"Value is {x}",
                onFailure: e => $"Error: {e}"
            );

            Assert.That(matched, Is.EqualTo("Value is 10"));
        }

        [Test]
        public void Match_OnFailure_CallsOnFailureFunction()
        {
            var result = Result<int>.Failure("original error");

            var matched = result.Match(
                onSuccess: x => $"Value is {x}",
                onFailure: e => $"Error: {e}"
            );

            Assert.That(matched, Is.EqualTo("Error: original error"));
        }

        #endregion

        #region OnSuccess/OnFailure Tests

        [Test]
        public void OnSuccess_WhenSuccessful_ExecutesAction()
        {
            var result = Result<int>.Success(42);
            int capturedValue = 0;

            result.OnSuccess(v => capturedValue = v);

            Assert.That(capturedValue, Is.EqualTo(42));
        }

        [Test]
        public void OnSuccess_WhenFailure_DoesNotExecuteAction()
        {
            var result = Result<int>.Failure("error");
            bool wasExecuted = false;

            result.OnSuccess(_ => wasExecuted = true);

            Assert.That(wasExecuted, Is.False);
        }

        [Test]
        public void OnFailure_WhenFailure_ExecutesAction()
        {
            var result = Result<int>.Failure("test error");
            string? capturedError = null;

            result.OnFailure(e => capturedError = e);

            Assert.That(capturedError, Is.EqualTo("test error"));
        }

        [Test]
        public void OnFailure_WhenSuccess_DoesNotExecuteAction()
        {
            var result = Result<int>.Success(42);
            bool wasExecuted = false;

            result.OnFailure(_ => wasExecuted = true);

            Assert.That(wasExecuted, Is.False);
        }

        #endregion

        #region GetValueOrDefault Tests

        [Test]
        public void GetValueOrDefault_OnSuccess_ReturnsValue()
        {
            var result = Result<int>.Success(42);

            var value = result.GetValueOrDefault(0);

            Assert.That(value, Is.EqualTo(42));
        }

        [Test]
        public void GetValueOrDefault_OnFailure_ReturnsDefault()
        {
            var result = Result<int>.Failure("error");

            var value = result.GetValueOrDefault(99);

            Assert.That(value, Is.EqualTo(99));
        }

        #endregion

        #region Implicit Conversion Tests

        [Test]
        public void ImplicitConversion_FromValue_CreatesSuccess()
        {
            Result<string> result = "hello";

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo("hello"));
        }

        #endregion

        #region ToString Tests

        [Test]
        public void ToString_OnSuccess_ReturnsSuccessString()
        {
            var result = Result<int>.Success(42);

            var str = result.ToString();

            Assert.That(str, Is.EqualTo("Success(42)"));
        }

        [Test]
        public void ToString_OnFailure_ReturnsFailureString()
        {
            var result = Result<int>.Failure("test error");

            var str = result.ToString();

            Assert.That(str, Is.EqualTo("Failure(test error)"));
        }

        #endregion
    }
}
