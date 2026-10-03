// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit.MappingTools;
using osuTK;

namespace osu.Game.Tests.Editing
{
    /// <summary>
    /// YAWNS: <see cref="AxisFinder"/>.
    /// </summary>
    [TestFixture]
    public class AxisFinderTest
    {
        [Test]
        public void TestDetectsTiltFromAllFourAxes()
        {
            // Straight sliders on 12, -12, 102 and 78 degrees, in both directions, slightly imprecise like hand placement.
            var sliders = new[] { 12.0, -12.4, 102.3, 78, 192, 11.6, -167.8, 258 }.Select(a => straight(a, 120));

            Assert.That(AxisFinder.DetectTilt(sliders), Is.EqualTo(12).Within(0.5));
        }

        [Test]
        public void TestMirroredMapHasSameTilt()
        {
            double[] angles = { 8, 98, -8, 82, 8 };

            double? tilt = AxisFinder.DetectTilt(angles.Select(a => straight(a, 100)));
            double? mirrored = AxisFinder.DetectTilt(angles.Select(a => straight(180 - a, 100)));

            Assert.That(tilt, Is.EqualTo(8).Within(0.5));
            Assert.That(mirrored, Is.EqualTo(tilt).Within(0.01));
        }

        [Test]
        public void TestCurvedSlidersAreIgnored()
        {
            var objects = new[] { straight(15, 100), straight(-15, 100), straight(75, 100) }
                          .Concat(Enumerable.Range(0, 10).Select(_ => curved()));

            Assert.That(AxisFinder.DetectTilt(objects), Is.EqualTo(15).Within(0.5));
        }

        [Test]
        public void TestTooFewSliders()
        {
            Assert.That(AxisFinder.DetectTilt(new HitObject[] { straight(10, 100), straight(10, 100), new HitCircle() }), Is.Null);
        }

        [TestCase(20, 12, -8)]
        [TestCase(100, 12, 2)] // nearest is 102
        [TestCase(190, 12, 2)] // same line as 10, nearest 12
        [TestCase(-80, 12, 2)] // same line as 100
        [TestCase(45, 12, -33)] // 45 is between 12 and 78, 12 wins ties toward the first axis
        [TestCase(-12, 12, 0)]
        public void TestRotationToNearestAxis(double angle, double tilt, double expected)
        {
            Assert.That(AxisFinder.RotationToNearestAxis(angle, tilt), Is.EqualTo(expected).Within(0.001));
        }

        private static Slider straight(double angle, float length)
        {
            double rad = MathHelper.DegreesToRadians(angle);

            return new Slider
            {
                Position = new Vector2(256, 192),
                Path = new SliderPath(new[]
                {
                    new PathControlPoint(Vector2.Zero, PathType.LINEAR),
                    new PathControlPoint(length * new Vector2((float)Math.Cos(rad), (float)Math.Sin(rad))),
                }),
            };
        }

        private static Slider curved() => new Slider
        {
            Path = new SliderPath(new[]
            {
                new PathControlPoint(Vector2.Zero, PathType.PERFECT_CURVE),
                new PathControlPoint(new Vector2(50, 40)),
                new PathControlPoint(new Vector2(100, 0)),
            }),
        };
    }
}
