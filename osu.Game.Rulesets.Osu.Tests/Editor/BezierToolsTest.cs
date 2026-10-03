// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Screens.Edit.MappingTools;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Editor
{
    /// <summary>
    /// YAWNS: <see cref="BezierTools"/>.
    /// </summary>
    [TestFixture]
    public class BezierToolsTest
    {
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(6)]
        public void TestDegreeElevationKeepsTheCurve(int pointCount)
        {
            var points = new Vector2[pointCount];

            for (int i = 0; i < pointCount; i++)
                points[i] = new Vector2(i * 37 % 91, (i * i * 53 + 11) % 77);

            var elevated = BezierTools.ElevateDegree(points);

            Assert.That(elevated, Has.Length.EqualTo(pointCount + 1));
            Assert.That(elevated[0], Is.EqualTo(points[0]));
            Assert.That(elevated[^1], Is.EqualTo(points[^1]));

            for (float t = 0; t <= 1.0001f; t += 0.05f)
                Assert.That(Vector2.Distance(BezierTools.Evaluate(elevated, t), BezierTools.Evaluate(points, t)), Is.LessThan(0.001f), $"t = {t}");
        }

        [TestCase(0.5f)]
        [TestCase(0.2f)]
        [TestCase(0.85f)]
        public void TestDragOffsetsMoveTheCurvePointExactly(float t)
        {
            var points = new[] { new Vector2(0, 0), new Vector2(40, 90), new Vector2(120, -30), new Vector2(200, 40) };
            var delta = new Vector2(12, -9);
            var offsets = BezierTools.DragOffsets(points.Length, t, delta);

            Assert.That(offsets[0], Is.EqualTo(Vector2.Zero), "the head stays");

            var moved = points.Select((p, i) => p + offsets[i]).ToArray();
            Vector2 expected = BezierTools.Evaluate(points, t) + delta;

            Assert.That(Vector2.Distance(BezierTools.Evaluate(moved, t), expected), Is.LessThan(0.01f));
        }

        [Test]
        public void TestClosestTFindsAPointOnTheCurve()
        {
            var points = new[] { new Vector2(0, 0), new Vector2(40, 90), new Vector2(120, -30), new Vector2(200, 40) };
            Vector2 onCurve = BezierTools.Evaluate(points, 0.37f);

            var (t, distance) = BezierTools.ClosestT(points, onCurve + new Vector2(0.3f, 0.2f));

            Assert.That(t, Is.EqualTo(0.37f).Within(0.01f));
            Assert.That(distance, Is.LessThan(1f));
        }

        [Test]
        public void TestQuadraticToCubicKnownValues()
        {
            // P0 = (0,0), P1 = (30,60), P2 = (60,0): Q1 = 1/3 P0 + 2/3 P1 = (20,40), Q2 = 2/3 P1 + 1/3 P2 = (40,40).
            var elevated = BezierTools.ElevateDegree(new[] { new Vector2(0, 0), new Vector2(30, 60), new Vector2(60, 0) });

            Assert.That(elevated[1], Is.EqualTo(new Vector2(20, 40)).Using<Vector2>((a, b) => Vector2.Distance(a, b) < 1e-4f ? 0 : 1));
            Assert.That(elevated[2], Is.EqualTo(new Vector2(40, 40)).Using<Vector2>((a, b) => Vector2.Distance(a, b) < 1e-4f ? 0 : 1));
        }
    }
}
