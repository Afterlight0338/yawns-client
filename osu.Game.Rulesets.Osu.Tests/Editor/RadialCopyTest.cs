// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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
        [Test]
        public void TestCircleAroundPlayfieldCentre()
        {
            var circle = new HitCircle { StartTime = 1000, Position = new Vector2(356, 192) };
            var radial = new RadialCopy { Count = { Value = 4 }, Beats = { Value = 1 } };

            var copies = radial.Create(new[] { circle }, 500);

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
            var slider = new Slider
            {
                StartTime = 1000,
                Position = new Vector2(256, 192),
                Path = new SliderPath(new[] { new PathControlPoint(Vector2.Zero, PathType.LINEAR), new PathControlPoint(new Vector2(100, 0)) }),
            };
            slider.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());

            var copies = new RadialCopy { Count = { Value = 3 } }.Create(new[] { slider }, 500);
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

            var copies = new RadialCopy { Count = { Value = 2 }, Around = { Value = RadialCopy.Centre.Selection } }.Create(new[] { a, b }, 500);

            // Half a turn around (150, 100) swaps them.
            Assert.That(Vector2.Distance(copies[0].Position, new Vector2(200, 100)), Is.LessThan(0.01f));
            Assert.That(Vector2.Distance(copies[1].Position, new Vector2(100, 100)), Is.LessThan(0.01f));
        }
    }
}
