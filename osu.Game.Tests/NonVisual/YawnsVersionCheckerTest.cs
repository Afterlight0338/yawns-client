// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Updater;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// YAWNS: <see cref="YawnsVersionChecker.IsNewer"/>, comparing a release tag with the running version.
    /// </summary>
    [TestFixture]
    public class YawnsVersionCheckerTest
    {
        [TestCase("v6769.004", "6769.003", true)]
        [TestCase("v6770.001", "6769.999", true)]
        [TestCase("v6769.003", "6769.003", false)]
        [TestCase("v6769.002", "6769.003", false)]
        [TestCase("6769.010", "6769.009", true)]
        [TestCase("v6769.003.1", "6769.003", true)]
        [TestCase("v6769.003", "6769.003.1", false)]
        [TestCase("nightly", "6769.003", false)]
        [TestCase("", "6769.003", false)]
        [TestCase(null, "6769.003", false)]
        public void TestIsNewer(string? tag, string current, bool expected) => Assert.That(YawnsVersionChecker.IsNewer(tag, current), Is.EqualTo(expected));
    }
}
