// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Screens.Edit.MappingTools;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Editor
{
    /// <summary>
    /// YAWNS: <see cref="PerfectIt"/>.
    /// </summary>
    [TestFixture]
    public class PerfectItTest
    {
        private static readonly Vector2 centre = new Vector2(256, 192);

        private static Vector2 onCircle(double degrees, float radius) =>
            centre + radius * new Vector2((float)Math.Cos(double.DegreesToRadians(degrees)), (float)Math.Sin(double.DegreesToRadians(degrees)));

        private static readonly Vector2[] jitter =
        {
            new Vector2(3, -2), new Vector2(-4, 1), new Vector2(2, 4), new Vector2(-1, -3), new Vector2(4, 2), new Vector2(-3, 3),
        };

        [Test]
        public void TestExactPolygonStaysPut()
        {
            var hexagon = Enumerable.Range(0, 6).Select(i => onCircle(i * 60 + 10, 100)).ToArray();
            var result = PerfectIt.FitPolygon(hexagon, 6, 1);

            Assert.That(result.Moved, Is.LessThan(0.01));

            for (int i = 0; i < 6; i++)
                Assert.That(Vector2.Distance(result.Positions[i], hexagon[i]), Is.LessThan(0.01f));
        }

        [Test]
        public void TestRoughPolygonBecomesRegular()
        {
            var rough = Enumerable.Range(0, 6).Select(i => onCircle(i * 60, 100) + jitter[i]).ToArray();
            var result = PerfectIt.FitPolygon(rough, 6, 1);

            Assert.That(result.Moved, Is.GreaterThan(1).And.LessThan(6));

            float radius = Vector2.Distance(result.Positions[0], centre);

            foreach (var p in result.Positions)
                Assert.That(Vector2.Distance(p, centre), Is.EqualTo(radius).Within(1.5f), "all on one circle around the centre");

            // Neighbouring vertices are the same distance apart.
            float side = Vector2.Distance(result.Positions[0], result.Positions[1]);

            for (int i = 1; i < 6; i++)
                Assert.That(Vector2.Distance(result.Positions[i], result.Positions[(i + 1) % 6]), Is.EqualTo(side).Within(0.01f));
        }

        [TestCase(1)]
        [TestCase(-1)]
        public void TestPentagramIsFoundInEitherDirection(int direction)
        {
            var star = Enumerable.Range(0, 5).Select(i => onCircle(direction * i * 144 - 90, 120)).ToArray();
            Assert.That(PerfectIt.FitPolygon(star, 5, 2).Moved, Is.LessThan(0.01));
        }

        [Test]
        public void TestRotationalSymmetry()
        {
            // Two objects turned by 120 degrees twice, each a little off.
            var exact = new[] { 0, 120, 240 }.SelectMany(a => new[] { onCircle(a + 10, 60), onCircle(a + 40, 120) }).ToArray();
            var rough = exact.Select((p, i) => p + jitter[i]).ToArray();

            var result = PerfectIt.FitRotational(rough, 3);

            Assert.That(result.Moved, Is.GreaterThan(0.5).And.LessThan(6));

            // The second group is the first turned by 120 degrees around the fitted centre (where the groups balance).
            Vector2 fittedCentre = result.Positions.Aggregate(Vector2.Zero, (sum, p) => sum + p) / result.Positions.Length;
            for (int p = 0; p < 2; p++)
            {
                Vector2 offset = result.Positions[p] - fittedCentre;
                Vector2 turned = new Vector2(offset.X * -0.5f - offset.Y * 0.8660254f, offset.X * 0.8660254f - offset.Y * 0.5f);
                Assert.That(Vector2.Distance(result.Positions[2 + p], fittedCentre + turned), Is.LessThan(0.01f), $"object {p}");
            }

            Assert.That(PerfectIt.FitRotational(exact, 3).Moved, Is.LessThan(0.01));
            Assert.That(PerfectIt.FitRotational(rough.Take(5).ToArray(), 3).Moved, Is.EqualTo(0), "5 objects do not split into 3 groups");
        }

        [Test]
        public void TestMirrorPair()
        {
            var points = new[] { new Vector2(200, 100), new Vector2(180, 250), new Vector2(402, 103), new Vector2(419, 248) };
            var result = PerfectIt.FitMirror(points);

            // The line is about x = 300 (slightly tilted, it is a fit): each pair adds up to twice that, and the heights nearly match.
            for (int i = 0; i < 2; i++)
            {
                Assert.That(result.Positions[i].X + result.Positions[i + 2].X, Is.EqualTo(600).Within(3f));
                Assert.That(result.Positions[i].Y, Is.EqualTo(result.Positions[i + 2].Y).Within(1.5f));
            }

            Assert.That(PerfectIt.FitMirror(result.Positions).Moved, Is.LessThan(0.01), "fitting a fit changes nothing");
        }
    }
}
