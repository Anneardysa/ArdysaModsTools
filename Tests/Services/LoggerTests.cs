/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */
using NUnit.Framework;
using System;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Tests.Helpers;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class LoggerTests
    {
        #region NullLogger Tests

        [Test]
        public void NullLogger_Instance_IsSingleton()
        {
            var instance1 = NullLogger.Instance;
            var instance2 = NullLogger.Instance;

            Assert.That(instance1, Is.SameAs(instance2));
        }

        [Test]
        public void NullLogger_Log_DoesNotThrow()
        {
            var logger = NullLogger.Instance;

            Assert.DoesNotThrow(() => logger.Log("test message"));
            Assert.DoesNotThrow(() => logger.Log("test", LogLevel.Error));
            Assert.DoesNotThrow(() => logger.LogError("error", new Exception("test")));
            Assert.DoesNotThrow(() => logger.LogWarning("warning"));
            Assert.DoesNotThrow(() => logger.LogDebug("debug"));
            Assert.DoesNotThrow(() => logger.FlushBufferedLogs());
        }

        [Test]
        public void NullLogger_ImplementsIAppLogger()
        {
            Assert.That(NullLogger.Instance, Is.InstanceOf<IAppLogger>());
        }

        #endregion

        #region TestLogger Tests

        [Test]
        public void TestLogger_CaputresAllMessages()
        {
            var logger = new TestLogger();

            logger.Log("info message");
            logger.LogWarning("warning message");
            logger.LogError("error message");
            logger.LogDebug("debug message");

            Assert.That(logger.Logs.Count, Is.EqualTo(4));
        }

        [Test]
        public void TestLogger_HasLogContaining_MatchesCaseInsensitive()
        {
            var logger = new TestLogger();
            logger.Log("Hello World");

            Assert.That(logger.HasLogContaining("hello"), Is.True);
            Assert.That(logger.HasLogContaining("WORLD"), Is.True);
            Assert.That(logger.HasLogContaining("missing"), Is.False);
        }

        [Test]
        public void TestLogger_HasLogContaining_WithLevel_FiltersCorrectly()
        {
            var logger = new TestLogger();
            logger.Log("info message", LogLevel.Info);
            logger.Log("error message", LogLevel.Error);

            Assert.That(logger.HasLogContaining("message", LogLevel.Info), Is.True);
            Assert.That(logger.HasLogContaining("message", LogLevel.Error), Is.True);
            Assert.That(logger.HasLogContaining("info", LogLevel.Error), Is.False);
        }

        [Test]
        public void TestLogger_ErrorCount_CountsErrorsOnly()
        {
            var logger = new TestLogger();
            logger.Log("info");
            logger.LogWarning("warning");
            logger.LogError("error 1");
            logger.LogError("error 2", new Exception("test"));

            Assert.That(logger.ErrorCount, Is.EqualTo(2));
            Assert.That(logger.WarningCount, Is.EqualTo(1));
        }

        [Test]
        public void TestLogger_LogError_IncludesExceptionMessage()
        {
            var logger = new TestLogger();
            var ex = new InvalidOperationException("Operation failed");

            logger.LogError("Something went wrong", ex);

            Assert.That(logger.HasLogContaining("Something went wrong"), Is.True);
            Assert.That(logger.HasLogContaining("Operation failed"), Is.True);
        }

        [Test]
        public void TestLogger_Clear_RemovesAllLogs()
        {
            var logger = new TestLogger();
            logger.Log("message 1");
            logger.Log("message 2");

            logger.Clear();

            Assert.That(logger.Logs.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestLogger_GetMessagesAtLevel_FiltersCorrectly()
        {
            var logger = new TestLogger();
            logger.Log("info 1", LogLevel.Info);
            logger.Log("info 2", LogLevel.Info);
            logger.LogWarning("warning 1");
            logger.LogError("error 1");

            var infoMessages = logger.GetMessagesAtLevel(LogLevel.Info);

            Assert.That(infoMessages.Count(), Is.EqualTo(2));
        }

        [Test]
        public void TestLogger_Messages_ReturnsAllMessageStrings()
        {
            var logger = new TestLogger();
            logger.Log("first");
            logger.Log("second");

            var messages = logger.Messages.ToList();

            Assert.That(messages, Contains.Item("first"));
            Assert.That(messages, Contains.Item("second"));
        }

        #endregion
    }
}
