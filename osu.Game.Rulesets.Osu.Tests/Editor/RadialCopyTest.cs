// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Edit;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Editor
{
    /// <summary>
    /// YAWNS: <see cref="RadialCopy"/>.
    /// </summary>
    [TestFixture]
    public class RadialCopyTest
    {
        private static readonly Vector2 centre = new Vector2(256, 192);

        [Test]
        public void TestCircleAroundPlayfieldCentre()
        {
            var circle = new HitCircle { StartTime = 1000, Position = new Vector2(356, 192) };
            var radial = new RadialCopy { Count = { Value = 4 }, Copies = { Value = 3 }, Beats = { Value = 1 } };

            var copies = radial.Create(new[] { circle }, centre, 500);

            Assert.That(copies, Has.Count.EqualTo(3));
            Assert.That(copies.Select(c => c.StartTime), Is.EqualTo(new double[] { 1500, 2000, 2500 }));

            var expected = new[] { new Vector2(256, 292), new Vector2(156, 192), new Vector2(256, 92) };

            for (int i = 0; i < 3; i++)
                Assert.That(Vector2.Distance(copies[i].Position, expected[i]), Is.LessThan(0.01f), $"copy {i}");

            Assert.That(circle.Position, Is.EqualTo(new Vector2(356, 192)), "original untouched");
        }

        [Test]
        public void TestSliderPathRotates()
        {
            var slider = createSlider();
            var copies = new RadialCopy { Count = { Value = 3 }, Copies = { Value = 2 } }.Create(new[] { slider }, centre, 500);
            var first = (Slider)copies[0];

            // 120 degrees: the slider now points down-left (y is down on the playfield).
            Vector2 direction = first.Path.ControlPoints[1].Position.Normalized();
            Assert.That(direction.X, Is.EqualTo(-0.5f).Within(0.001f));
            Assert.That(direction.Y, Is.EqualTo(0.866f).Within(0.001f));
            Assert.That(slider.Path.ControlPoints[1].Position, Is.EqualTo(new Vector2(100, 0)), "original untouched");
        }

        [Test]
        public void TestSelectionCentre()
        {
            var a = new HitCircle { StartTime = 0, Position = new Vector2(100, 100) };
            var b = new HitCircle { StartTime = 100, Position = new Vector2(200, 100) };
            var symmetryCentre = new SymmetryCentre { Around = { Value = SymmetryCentre.Mode.Selection } };
            var selection = new[] { a, b };

            var copies = new RadialCopy { Count = { Value = 2 }, Copies = { Value = 1 } }.Create(selection, symmetryCentre.Resolve(selection), 500);

            // Half a turn around (150, 100) swaps them.
            Assert.That(Vector2.Distance(copies[0].Position, new Vector2(200, 100)), Is.LessThan(0.01f));
            Assert.That(Vector2.Distance(copies[1].Position, new Vector2(100, 100)), Is.LessThan(0.01f));
        }

        [Test]
        public void TestPentagramOrder()
        {
            var circle = new HitCircle { StartTime = 0, Position = centre + new Vector2(100, 0) };
            var radial = new RadialCopy { Count = { Value = 5 }, Step = { Value = 2 } };

            Assert.That(radial.FullTurn, Is.EqualTo(5));
            radial.Copies.Value = radial.FullTurn - 1;

            var copies = radial.Create(new[] { circle }, centre, 500);

            // Copy j sits at 144 * j degrees: the pentagon's corners in the order 0, 2, 4, 1, 3.
            for (int j = 1; j <= 4; j++)
            {
                double angle = MathHelper.DegreesToRadians(144.0 * j);
                var expected = centre + 100 * new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
                Assert.That(Vector2.Distance(copies[j - 1].Position, expected), Is.LessThan(0.01f), $"copy {j}");
            }
        }

        [Test]
        public void TestFullTurnWithCommonDivisor()
        {
            // 6 divisions, 2 at a time: a triangle, back at the start after 3.
            Assert.That(new RadialCopy { Count = { Value = 6 }, Step = { Value = 2 } }.FullTurn, Is.EqualTo(3));
        }

        [Test]
        public void TestAnticlockwise()
        {
            var circle = new HitCircle { Position = new Vector2(356, 192) };
            var copies = new RadialCopy { Count = { Value = 4 }, Copies = { Value = 1 }, Anticlockwise = { Value = true } }.Create(new[] { circle }, centre, 500);

            Assert.That(Vector2.Distance(copies[0].Position, new Vector2(256, 92)), Is.LessThan(0.01f));
        }

        [Test]
        public void TestSpiralKeepsSliderDuration()
        {
            var slider = createSlider();
            double duration = slider.Duration;

            var copies = new RadialCopy { Count = { Value = 4 }, Copies = { Value = 2 }, ScalePerCopy = { Value = 0.8f } }.Create(new[] { slider }, centre, 500);

            for (int j = 1; j <= 2; j++)
            {
                var copy = (Slider)copies[j - 1];
                copy.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());

                Assert.That(copy.Path.Distance, Is.EqualTo(100 * Math.Pow(0.8, j)).Within(0.01), $"copy {j} length");
                Assert.That(copy.Duration, Is.EqualTo(duration).Within(0.5), $"copy {j} duration");
            }

            // The head moves inwards too: 0.8 and 0.64 of the way from the centre.
            Assert.That(Vector2.Distance(copies[0].Position, centre), Is.EqualTo(0).Within(0.01), "head at the centre stays there");
        }

        [Test]
        public void TestAlternateMirror()
        {
            // An off-centre slider pointing along its spoke (+x from the centre) is its own mirror, so use one pointing across: down.
            var slider = createSlider(new Vector2(356, 192), new Vector2(0, 100));
            var copies = new RadialCopy { Count = { Value = 4 }, Copies = { Value = 2 }, AlternateMirror = { Value = true } }.Create(new[] { slider }, centre, 500);

            // Copy 1: mirrored across its spoke (now pointing up), then turned 90 degrees: points right.
            Vector2 first = ((Slider)copies[0]).Path.ControlPoints[1].Position.Normalized();
            Assert.That(first.X, Is.EqualTo(1).Within(0.001f));

            // Copy 2: not mirrored, turned 180 degrees: points up.
            Vector2 second = ((Slider)copies[1]).Path.ControlPoints[1].Position.Normalized();
            Assert.That(second.Y, Is.EqualTo(-1).Within(0.001f));
        }

        private static Slider createSlider(Vector2? position = null, Vector2? end = null)
        {
            var slider = new Slider
            {
                StartTime = 1000,
                Position = position ?? centre,
                Path = new SliderPath(new[] { new PathControlPoint(Vector2.Zero, PathType.LINEAR), new PathControlPoint(end ?? new Vector2(100, 0)) }),
            };
            slider.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            return slider;
        }
    }
}
