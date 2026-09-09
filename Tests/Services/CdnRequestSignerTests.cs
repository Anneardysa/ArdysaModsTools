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
using System;
using System.Text.RegularExpressions;
using ArdysaModsTools.Core.Services.Cdn;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class CdnRequestSignerTests
    {
        private static readonly byte[] TestKey = Convert.FromHexString(
            "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f");

        private const string VectorPath = "Assets/models/Abaddon/blightfall.zip";
        private const long VectorExpiry = 1767225600;
        private const string VectorSignature =
            "bebd75c341072c8e50ad6f28d4b3ba727cdc29ba1abc670f4fcfe48f64913592";

        [Test]
        public void Sign_MatchesTheWorkerVector()
        {
            Assert.That(CdnRequestSigner.Sign(TestKey, VectorPath, VectorExpiry),
                Is.EqualTo(VectorSignature),
                "signing string drifted from ModsPack/scripts/cdn/test-guard.mjs");
        }

        [Test]
        public void BuildHeaderValue_IsVersionExpiryAndDigest()
        {
            string header = CdnRequestSigner.BuildHeaderValue(TestKey, VectorPath, VectorExpiry);

            Assert.That(header, Is.EqualTo($"1.{VectorExpiry}.{VectorSignature}"));
            Assert.That(Regex.IsMatch(header, "^1\\.[0-9]+\\.[0-9a-f]{64}$"), Is.True,
                "the Workers reject any header that does not match this exact shape");
        }

        [Test]
        public void Sign_IsBoundToThePath()
        {
            Assert.That(CdnRequestSigner.Sign(TestKey, "Assets/models/a.zip", VectorExpiry),
                Is.Not.EqualTo(CdnRequestSigner.Sign(TestKey, "Assets/models/b.zip", VectorExpiry)));
        }

        [Test]
        public void Sign_IsBoundToTheExpiry()
        {
            Assert.That(CdnRequestSigner.Sign(TestKey, VectorPath, VectorExpiry),
                Is.Not.EqualTo(CdnRequestSigner.Sign(TestKey, VectorPath, VectorExpiry + 1)));
        }

        [Test]
        public void Sign_IsBoundToTheKey()
        {
            var other = new byte[TestKey.Length];
            Array.Fill(other, (byte)0xFF);

            Assert.That(CdnRequestSigner.Sign(TestKey, VectorPath, VectorExpiry),
                Is.Not.EqualTo(CdnRequestSigner.Sign(other, VectorPath, VectorExpiry)));
        }

        [Test]
        public void Sign_EmptyPath_Throws()
        {
            Assert.Throws<ArgumentException>(() => CdnRequestSigner.Sign(TestKey, "", VectorExpiry));
        }

        [Test]
        public void Sign_NullKey_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => CdnRequestSigner.Sign(null!, VectorPath, VectorExpiry));
        }

        private const int WorkerMinSkewSeconds = -600;
        private const int WorkerMaxSkewSeconds = 900;

        [Test]
        public void LifetimeSeconds_SignsInsideTheWorkerAcceptanceWindow()
        {
            Assert.That(CdnRequestSigner.LifetimeSeconds, Is.GreaterThan(0),
                "a non-positive lifetime signs a token that is already expired when it is sent");

            Assert.That(CdnRequestSigner.LifetimeSeconds, Is.LessThan(WorkerMaxSkewSeconds),
                $"the Workers reject a skew above {WorkerMaxSkewSeconds}s " +
                "(GUARD.maxSkew in ModsPack/scripts/cdn/_guard-block.js)");
        }

        [Test]
        public void LifetimeSeconds_LeavesRoomForAnUncorrectedClock()
        {
            int headroom = WorkerMaxSkewSeconds - CdnRequestSigner.LifetimeSeconds;

            Assert.That(headroom, Is.GreaterThanOrEqualTo(-WorkerMinSkewSeconds),
                "too little headroom: ordinary clock drift would start costing a corrective retry " +
                "on the first protected request of every session");
        }
    }
}
