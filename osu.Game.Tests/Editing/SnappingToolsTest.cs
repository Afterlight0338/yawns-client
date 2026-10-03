// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Screens.Edit.MappingTools;
using osuTK;
using static osu.Game.Screens.Edit.MappingTools.SnappingTools;

namespace osu.Game.Tests.Editing
{
    /// <summary>
    /// YAWNS: the Snapping Tools port's geometry.
    /// </summary>
    [TestFixture]
    public class SnappingToolsTest
    {
        private static SnapSource circle(float x, float y) => new SnapSource(new Vector2(x, y), Array.Empty<Vector2>());

        private static bool has(Geometry g, Vector2 p) => g.Points.Concat(g.Intersections).Any(q => Vector2.Distance(p, q) < 0.01f);

        [Test]
        public void TestPointsFromTwoObjects()
        {
            var g = Generate(new[] { circle(100, 200), circle(200, 200) });

            Assert.That(has(g, new Vector2(150, 200)), "midpoint");
            Assert.That(has(g, new Vector2(150, 200 - 50 * MathF.Sqrt(3))), "equilateral triangle apex");
            Assert.That(has(g, new Vector2(100, 100)) && has(g, new Vector2(200, 300)), "square corners");
        }

        [Test]
        public void TestPerfectCircleCentre()
        {
            var slider = new SnapSource(new Vector2(100, 200), new[] { new Vector2(200, 100), new Vector2(300, 200) }, new Vector2(300, 200),
                PerfectCircle: new[] { new Vector2(100, 200), new Vector2(200, 100), new Vector2(300, 200) });

            var g = Generate(new[] { slider });

            Assert.That(has(g, new Vector2(200, 200)), "centre");
            Assert.That(g.Circles.Any(c => Vector2.Distance(c.Centre, new Vector2(200, 200)) < 0.01f && Math.Abs(c.Radius - 100) < 0.01f));
        }

        [Test]
        public void TestSnapPrefersPointsThenShapes()
        {
            var g = Generate(new[] { circle(100, 200), circle(200, 200) });

            Assert.That(Snap(g, new Vector2(152, 203), 8), Is.EqualTo(new Vector2(150, 200)));
            Assert.That(Snap(g, new Vector2(300, 204), 8)!.Value.Y, Is.EqualTo(200).Within(0.01), "onto the line through both");
            Assert.That(Snap(g, new Vector2(400, 50), 2), Is.Null);
        }
    }
}
