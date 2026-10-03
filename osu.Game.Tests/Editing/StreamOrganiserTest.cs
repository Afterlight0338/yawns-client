// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Screens.Edit.MappingTools;
using osuTK;

namespace osu.Game.Tests.Editing
{
    /// <summary>
    /// YAWNS: <see cref="StreamOrganiser"/>.
    /// </summary>
    [TestFixture]
    public class StreamOrganiserTest
    {
        private static readonly double[] even_times = { 0, 100, 200, 300, 400, 500 };

        // A badly hand-placed straight stream.
        private static readonly Vector2[] wobbly_line = { new Vector2(0, 0), new Vector2(10, 0), new Vector2(30, 0), new Vector2(35, 0), new Vector2(60, 0), new Vector2(100, 0) };

        /// <summary>
        /// A user's hand-placed 12 object stream shaped like a question mark (hook over the top, then an S bend down), read off a screenshot
        /// and scaled to osu! pixels. Very uneven: 3, 4 and 5 bunch up, 9 to 11 bend the wrong way a little.
        /// </summary>
        private static readonly Vector2[] question_mark = new[]
        {
            new Vector2(230, 420), new Vector2(285, 318), new Vector2(408, 275), new Vector2(522, 283), new Vector2(605, 345), new Vector2(633, 468),
            new Vector2(548, 578), new Vector2(420, 630), new Vector2(333, 708), new Vector2(335, 833), new Vector2(415, 905), new Vector2(545, 890),
        }.Select(p => p * 0.6f).ToArray();

        private static readonly double[] question_mark_times = Enumerable.Range(0, 12).Select(i => i * 79.0).ToArray();

        [Test]
        public void TestQuestionMarkIsEvenlySpaced()
        {
            var result = StreamOrganiser.Organise(question_mark, question_mark_times, 1, 0.5);
            double[] g = gaps(result);

            Assert.That(g.Max() / g.Min(), Is.LessThan(1.02), $"gaps {string.Join(", ", g.Select(x => x.ToString("0.0")))}");
            assertEndsPinned(question_mark, result);
        }

        [Test]
        public void TestQuestionMarkKeepsItsShapeWithoutWobble()
        {
            var result = StreamOrganiser.Organise(question_mark, question_mark_times, 1, 0.5);

            // Still a "?": the stream turns one way over the hook, then the other way through the S. Not a ")".
            Assert.That(turnSignChanges(result), Is.EqualTo(1), $"turns {string.Join(", ", turns(result).Select(x => x.ToString("0")))}");

            // Smooth: how sharply it turns changes gradually from object to object, unlike the hand placement.
            Assert.That(jerkiness(result), Is.LessThan(jerkiness(question_mark) * 0.6), $"jerkiness {jerkiness(result):0} vs {jerkiness(question_mark):0}");

            // Close to where it was placed: every object within half a circle radius (~22px at CS4) of the hand-drawn path.
            foreach (var p in result)
                Assert.That(distanceToPolyline(p, question_mark), Is.LessThan(22));
        }

        [Test]
        public void TestNoisyCurveComesBackClean()
        {
            // An S curve, sampled evenly, with every other object knocked 6px sideways.
            Vector2 truth(double t) => new Vector2((float)(t * 300), (float)(60 * Math.Sin(t * 2 * Math.PI)));

            var noisy = Enumerable.Range(0, 13).Select(i =>
            {
                var p = truth(i / 12.0);
                return i is 0 or 12 ? p : p + new Vector2(0, i % 2 == 0 ? 6 : -6);
            }).ToArray();

            double[] times = Enumerable.Range(0, 13).Select(i => i * 100.0).ToArray();
            var curve = Enumerable.Range(0, 1001).Select(i => truth(i / 1000.0)).ToArray();

            // The default evens the turning out, which rounds the S's peaks off a little (60px swing, 6px noise).
            var result = StreamOrganiser.Organise(noisy, times, 1, 0.5);
            Assert.That(result.Max(p => distanceToPolyline(p, curve)), Is.LessThan(10));
            Assert.That(turnSignChanges(result), Is.EqualTo(1));

            // Following the shape closely gives the true curve back.
            var close = StreamOrganiser.Organise(noisy, times, 1, 1);
            Assert.That(close.Max(p => distanceToPolyline(p, curve)), Is.LessThan(3));
            Assert.That(turnSignChanges(close), Is.EqualTo(1));
        }

        [Test]
        public void TestEvenSpacing()
        {
            var result = StreamOrganiser.Organise(wobbly_line, even_times, 1, 0.5);

            Assert.That(gaps(result), Is.All.EqualTo(20).Within(0.5));
            Assert.That(result.Select(p => p.Y), Is.All.EqualTo(0).Within(0.01));
            assertEndsPinned(wobbly_line, result);
        }

        [Test]
        public void TestAccelerate()
        {
            var result = StreamOrganiser.Organise(wobbly_line, even_times, 3, 0.5);
            double[] g = gaps(result);

            for (int i = 1; i < g.Length; i++)
                Assert.That(g[i], Is.GreaterThan(g[i - 1]));

            Assert.That(g[^1] / g[0], Is.EqualTo(3).Within(0.1));
            assertEndsPinned(wobbly_line, result);
        }

        [Test]
        public void TestDecelerateMirrorsAccelerate()
        {
            double[] accelerate = gaps(StreamOrganiser.Organise(wobbly_line, even_times, 3, 0.5));
            double[] decelerate = gaps(StreamOrganiser.Organise(wobbly_line, even_times, 1 / 3.0, 0.5));

            Assert.That(decelerate, Is.EqualTo(accelerate.Reverse()).Within(0.5));
        }

        [Test]
        public void TestSpacingFollowsRhythm()
        {
            // 1/4, 1/8, 1/4 at 150 BPM: the 1/8 gap gets half the spacing.
            var line = new[] { new Vector2(0, 0), new Vector2(30, 0), new Vector2(40, 0), new Vector2(100, 0) };
            var result = StreamOrganiser.Organise(line, new double[] { 0, 100, 150, 250 }, 1, 0.5);

            Assert.That(gaps(result), Is.EqualTo(new double[] { 40, 20, 40 }).Within(0.5));
        }

        [Test]
        public void TestCleanArcStaysPut()
        {
            // Evenly spaced points on a circle arc are already a clean stream.
            var arc = Enumerable.Range(0, 8).Select(i => new Vector2(256, 192) + 100 * new Vector2(MathF.Cos(i * 0.2f), MathF.Sin(i * 0.2f))).ToArray();
            var result = StreamOrganiser.Organise(arc, Enumerable.Range(0, 8).Select(i => i * 100.0).ToArray(), 1, 0.5);

            for (int i = 0; i < arc.Length; i++)
                Assert.That(Vector2.Distance(arc[i], result[i]), Is.LessThan(1), $"object {i}");
        }

        [Test]
        public void TestFollowShapeKeepsMoreDetail()
        {
            // A straight stream with a deliberate bump over three objects.
            var bumped = new[]
            {
                new Vector2(0, 0), new Vector2(20, 0), new Vector2(40, 0), new Vector2(60, 20), new Vector2(80, 30), new Vector2(100, 20),
                new Vector2(120, 0), new Vector2(140, 0), new Vector2(160, 0), new Vector2(180, 0), new Vector2(200, 0),
            };
            var times = Enumerable.Range(0, bumped.Length).Select(i => i * 100.0).ToArray();

            float loose = StreamOrganiser.Organise(bumped, times, 1, 0).Max(p => p.Y);
            float close = StreamOrganiser.Organise(bumped, times, 1, 1).Max(p => p.Y);

            Assert.That(close, Is.GreaterThan(loose));
        }

        [Test]
        public void TestZigzagIsIronedOut()
        {
            var zigzag = new[] { new Vector2(0, 0), new Vector2(20, 15), new Vector2(40, -15), new Vector2(60, 15), new Vector2(80, -15), new Vector2(100, 0) };

            // At the default setting, wobble from one object to the next is removed.
            var result = StreamOrganiser.Organise(zigzag, even_times, 1, 0.5);

            Assert.That(result.Max(p => Math.Abs(p.Y)), Is.LessThan(8));
        }

        [Test]
        public void TestArcPutsStreamOnACircle()
        {
            // A wobbly arc around (256, 192) with radius 100.
            var wobbly = Enumerable.Range(0, 7).Select(i =>
            {
                float a = i * MathF.PI / 12;
                float r = i is 0 or 6 ? 100 : 100 + (i % 2 == 0 ? -6 : 6);
                return new Vector2(256, 192) + r * new Vector2(MathF.Cos(a), MathF.Sin(a));
            }).ToArray();

            var result = StreamOrganiser.Organise(wobbly, Enumerable.Range(0, 7).Select(i => i * 100.0).ToArray(), 1, 0, StreamOrganiser.ShapeMode.Arc);

            Assert.That(result.Select(p => Vector2.Distance(p, new Vector2(256, 192))), Is.All.EqualTo(100).Within(3));
            Assert.That(gaps(result), Is.All.EqualTo(gaps(result)[0]).Within(0.5), "even");
        }

        [Test]
        public void TestArcBeyondHalfCircle()
        {
            // Three quarters of a circle: the arc must go the long way round, through the objects.
            var points = Enumerable.Range(0, 10).Select(i => new Vector2(256, 192) + 80 * new Vector2(MathF.Cos(i * MathF.PI / 6), MathF.Sin(i * MathF.PI / 6))).ToArray();
            var result = StreamOrganiser.Organise(points, Enumerable.Range(0, 10).Select(i => i * 100.0).ToArray(), 1, 0, StreamOrganiser.ShapeMode.Arc);

            for (int i = 0; i < points.Length; i++)
                Assert.That(Vector2.Distance(points[i], result[i]), Is.LessThan(1), $"object {i}");
        }

        [Test]
        public void TestStraight()
        {
            var zigzag = new[] { new Vector2(0, 0), new Vector2(20, 10), new Vector2(40, -10), new Vector2(60, 10), new Vector2(100, 0) };
            var result = StreamOrganiser.Organise(zigzag, even_times.Take(5).ToArray(), 1, 0, StreamOrganiser.ShapeMode.Straight);

            Assert.That(result.Select(p => p.Y), Is.All.EqualTo(0).Within(0.01));
            Assert.That(gaps(result), Is.All.EqualTo(25).Within(0.5));
        }

        [Test]
        public void TestDistanceSnapSpacing()
        {
            // 30px per 100ms: the stream ends up 150px long, past the hand-placed 100px, continuing straight on.
            var result = StreamOrganiser.Organise(wobbly_line, even_times, 1, 0, StreamOrganiser.ShapeMode.Straight, (_, duration) => duration * 0.3);

            Assert.That(gaps(result), Is.All.EqualTo(30).Within(0.5));
            Assert.That(result[0], Is.EqualTo(wobbly_line[0]));
            Assert.That(result[^1].X, Is.EqualTo(150).Within(0.5));
            Assert.That(result.Select(p => p.Y), Is.All.EqualTo(0).Within(0.01));
        }

        [Test]
        public void TestDistanceSnapAccelerates()
        {
            var result = StreamOrganiser.Organise(wobbly_line, even_times, 2, 0, StreamOrganiser.ShapeMode.Straight, (_, _) => 20);
            double[] g = gaps(result);

            Assert.That(g[0], Is.EqualTo(20).Within(0.5), "starts at the distance snap");
            Assert.That(g[^1], Is.EqualTo(40).Within(0.5));
        }

        [Test]
        public void TestDistanceSnapOnQuestionMark()
        {
            var result = StreamOrganiser.Organise(question_mark, question_mark_times, 1, 0.5, StreamOrganiser.ShapeMode.Clean, (_, _) => 40);

            Assert.That(gaps(result), Is.All.EqualTo(40).Within(0.5));
        }

        [Test]
        public void TestTooShortIsUnchanged()
        {
            var pair = new[] { new Vector2(0, 0), new Vector2(50, 50) };
            Assert.That(StreamOrganiser.Organise(pair, new double[] { 0, 100 }, 2, 1), Is.EqualTo(pair));
        }

        private static double[] gaps(Vector2[] points) => points.Zip(points.Skip(1), (a, b) => (double)Vector2.Distance(a, b)).ToArray();

        /// <summary>
        /// Signed turning at each inner object, in degrees (positive one way, negative the other).
        /// </summary>
        private static double[] turns(Vector2[] points) => Enumerable.Range(1, points.Length - 2).Select(i =>
        {
            Vector2 a = points[i] - points[i - 1];
            Vector2 b = points[i + 1] - points[i];
            return MathHelper.RadiansToDegrees(Math.Atan2(a.X * b.Y - a.Y * b.X, Vector2.Dot(a, b)));
        }).ToArray();

        /// <summary>
        /// How often the stream switches between turning one way and the other (ignoring near-straight bits).
        /// </summary>
        private static int turnSignChanges(Vector2[] points)
        {
            int[] signs = turns(points).Where(t => Math.Abs(t) > 3).Select(Math.Sign).ToArray();
            return signs.Zip(signs.Skip(1), (a, b) => a != b).Count(x => x);
        }

        /// <summary>
        /// How unevenly the turning changes along the stream, in degrees: the sum of the changes of the change of turn.
        /// A clean stream eases in and out of its bends (an S still has to go from one way to the other, that alone costs nothing here).
        /// </summary>
        private static double jerkiness(Vector2[] points)
        {
            double[] t = turns(points);
            double[] change = t.Zip(t.Skip(1), (a, b) => b - a).ToArray();
            return change.Zip(change.Skip(1), (a, b) => Math.Abs(b - a)).Sum();
        }

        private static double distanceToPolyline(Vector2 p, Vector2[] polyline) => Enumerable.Range(0, polyline.Length - 1).Min(i =>
        {
            Vector2 a = polyline[i], b = polyline[i + 1];
            float t = Math.Clamp(Vector2.Dot(p - a, b - a) / Math.Max(Vector2.DistanceSquared(a, b), 1e-6f), 0, 1);
            return (double)Vector2.Distance(p, a + t * (b - a));
        });

        private static void assertEndsPinned(Vector2[] original, Vector2[] result)
        {
            Assert.That(result[0], Is.EqualTo(original[0]));
            Assert.That(result[^1], Is.EqualTo(original[^1]));
        }
    }
}
