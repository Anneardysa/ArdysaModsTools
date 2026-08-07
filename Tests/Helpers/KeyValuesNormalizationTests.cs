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
using System.Text;
using ArdysaModsTools.Core.Helpers;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Helpers
{
    [TestFixture]
    public class KeyValuesNormalizationTests
    {
        [Test]
        public void CrLf_BecomesLf()
        {
            Assert.That(KeyValuesBlockHelper.NormalizeKvText("a\r\nb\r\nc"), Is.EqualTo("a\nb\nc"));
        }

        [Test]
        public void LoneCr_BecomesLf()
        {
            Assert.That(KeyValuesBlockHelper.NormalizeKvText("a\rb"), Is.EqualTo("a\nb"));
        }

        [Test]
        public void ConsecutiveCrs_EachBecomeALf()
        {
            Assert.That(KeyValuesBlockHelper.NormalizeKvText("a\r\rb"), Is.EqualTo("a\n\nb"));
            Assert.That(KeyValuesBlockHelper.NormalizeKvText("a\r\n\r\nb"), Is.EqualTo("a\n\nb"));
        }

        [Test]
        public void TrailingCr_IsStillConverted()
        {
            Assert.That(KeyValuesBlockHelper.NormalizeKvText("a\r"), Is.EqualTo("a\n"));
        }

        [Test]
        public void SmartQuotes_BecomeAscii()
        {
            Assert.That(KeyValuesBlockHelper.NormalizeKvText("“x”"), Is.EqualTo("\"x\""));
            Assert.That(KeyValuesBlockHelper.NormalizeKvText("‘x’"), Is.EqualTo("'x'"));
        }

        [Test]
        public void NonBreakingSpaces_BecomeOrdinarySpaces()
        {
            Assert.That(KeyValuesBlockHelper.NormalizeKvText("a b c d"), Is.EqualTo("a b c d"));
        }

        [Test]
        public void ZeroWidthCharacters_AreStripped()
        {
            Assert.That(KeyValuesBlockHelper.NormalizeKvText("a​b‌c‍d⁠e"), Is.EqualTo("abcde"));
        }

        [Test]
        public void ByteOrderMark_IsStrippedWhereverItAppears()
        {
            Assert.That(KeyValuesBlockHelper.NormalizeKvText("﻿\"key\""), Is.EqualTo("\"key\""));
            Assert.That(KeyValuesBlockHelper.NormalizeKvText("a﻿b"), Is.EqualTo("ab"));
        }

        [Test]
        public void EverythingAtOnce()
        {
            const string messy = "﻿“key”\r\n\t “value​”\r";
            Assert.That(KeyValuesBlockHelper.NormalizeKvText(messy), Is.EqualTo("\"key\"\n\t \"value\"\n"));
        }

        [Test]
        public void EmptyInput_ReturnsEmpty()
        {
            Assert.That(KeyValuesBlockHelper.NormalizeKvText(""), Is.EqualTo(string.Empty));
            Assert.That(KeyValuesBlockHelper.NormalizeKvText(null!), Is.EqualTo(string.Empty));
        }

        [Test]
        public void AlreadyNormalizedText_IsReturnedWithoutCopying()
        {
            var clean = new StringBuilder()
                .Append("\"items_game\"\n{\n\t\"items\"\n\t{\n\t\t\"101\" { \"name\" \"a\" }\n\t}\n}\n")
                .ToString();

            Assert.That(KeyValuesBlockHelper.NormalizeKvText(clean), Is.SameAs(clean));
        }

        [Test]
        public void Normalization_IsIdempotent()
        {
            const string messy = "﻿“a”\r\n b​\r";

            var once = KeyValuesBlockHelper.NormalizeKvText(messy);
            var twice = KeyValuesBlockHelper.NormalizeKvText(once);

            Assert.That(twice, Is.EqualTo(once));
            Assert.That(twice, Is.SameAs(once), "the second pass should find nothing to do");
        }

        [Test]
        public void BulkCopyingBetweenSubstitutions_LosesNothing()
        {
            var input = new StringBuilder();
            var expected = new StringBuilder();
            for (int i = 0; i < 5000; i++)
            {
                input.Append("\t\t\"key").Append(i).Append("\"\t\"value").Append(i).Append("\"\r\n");
                expected.Append("\t\t\"key").Append(i).Append("\"\t\"value").Append(i).Append("\"\n");
            }

            Assert.That(KeyValuesBlockHelper.NormalizeKvText(input.ToString()),
                        Is.EqualTo(expected.ToString()));
        }
    }
}
