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
    /// YAWNS: <see cref="AngledFlip"/>.
    /// </summary>
    [TestFixture]
    public class AngledFlipTest
    {
        private static readonly Vector2 centre = new Vector2(256, 192);

        private static void assertAt(OsuHitObject h, Vector2 expected) =>
            Assert.That(Vector2.Distance(h.Position, expected), Is.LessThan(0.01f), $"{h.Position} vs {expected}");

        [Test]
        public void TestVerticalLineFlipsLeftToRight()
        {
            var circle = new HitCircle { StartTime = 1000, Position = new Vector2(356, 100) };
            var copy = new AngledFlip { Angle = { Value = 90 } }.Create(new[] { circle }, centre, 500).Single();

            assertAt(copy, new Vector2(156, 100));
            Assert.That(copy.StartTime, Is.EqualTo(1000), "in place keeps the time");
            Assert.That(circle.Position, Is.EqualTo(new Vector2(356, 100)), "original untouched");
        }

        [Test]
        public void TestHorizontalLineFlipsTopToBottom()
        {
            var circle = new HitCircle { Position = new Vector2(356, 100) };
            assertAt(new AngledFlip { Angle = { Value = 0 } }.Create(new[] { circle }, centre, 500).Single(), new Vector2(356, 284));
        }

        [Test]
        public void TestDiagonalSwapsAxesAndDirectionMirrors()
        {
            var circle = new HitCircle { Position = centre + new Vector2(100, 0) };

            // 45 degrees clockwise on screen (y down): the line runs top-left to bottom-right, so (100, 0) goes to (0, 100).
            assertAt(new AngledFlip { Angle = { Value = 45 } }.Create(new[] { circle }, centre, 500).Single(), centre + new Vector2(0, 100));
            assertAt(new AngledFlip { Angle = { Value = 45 }, Anticlockwise = { Value = true } }.Create(new[] { circle }, centre, 500).Single(), centre + new Vector2(0, -100));
        }

        [Test]
        public void TestSliderBodyFlipsAndFlippingTwiceRestores()
        {
            var slider = new Slider
            {
                Position = new Vector2(100, 100),
                Path = new SliderPath(new[] { new PathControlPoint(Vector2.Zero), new PathControlPoint(new Vector2(80, 30)) }),
            };

            slider.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            var flip = new AngledFlip { Angle = { Value = 90 } };
            var once = (Slider)flip.Create(new[] { slider }, centre, 500).Single();

            Assert.That(once.Path.ControlPoints[1].Position.X, Is.EqualTo(-80).Within(0.01f));
            Assert.That(once.Path.ControlPoints[1].Position.Y, Is.EqualTo(30).Within(0.01f));

            var twice = (Slider)flip.Create(new[] { once }, centre, 500).Single();
            assertAt(twice, slider.Position);
            Assert.That(Vector2.Distance(twice.Path.ControlPoints[1].Position, new Vector2(80, 30)), Is.LessThan(0.01f));
        }

        [Test]
        public void TestKeepOriginalOffsetsTime()
        {
            var circle = new HitCircle { StartTime = 1000, Position = new Vector2(356, 100) };
            var copy = new AngledFlip { KeepOriginal = { Value = true }, Beats = { Value = 2 } }.Create(new[] { circle }, centre, 500).Single();

            Assert.That(copy.StartTime, Is.EqualTo(2000));
        }
    }
}
