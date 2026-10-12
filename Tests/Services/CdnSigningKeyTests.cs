/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
using ArdysaModsTools.Core.Constants;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class CdnSigningKeyTests
    {
        [Test]
        public void Key_IsPresent()
        {
            Assert.That(CdnSigningKey.Key, Is.Not.Null,
                "this build cannot sign CDN requests. Either the CdnSigningKey.cs fragments are " +
                "wrong (identical, truncated, or a half-applied rotation — regenerate with " +
                "ModsPack/scripts/cdn/gen-cdn-secret.ps1), or this is a clone of the public " +
                "GitHub mirror, where the fragments ship as zeros by design (ADR-0019).");
        }

        [Test]
        public void Key_Is32Bytes()
        {
            Assert.That(CdnSigningKey.Key, Has.Length.EqualTo(32));
        }

        [Test]
        public void Key_IsNotAllZeros()
        {
            Assert.That(CdnSigningKey.Key, Has.Some.Not.EqualTo((byte)0));
        }
    }
}
