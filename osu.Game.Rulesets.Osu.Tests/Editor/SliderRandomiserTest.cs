// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit.MappingTools;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Editor
{
    /// <summary>
    /// YAWNS: <see cref="SliderRandomiser"/>.
    /// </summary>
    [TestFixture]
    public class SliderRandomiserTest
    {
        private static Slider slider(Vector2 head, Vector2 end)
        {
            var s = new Slider
            {
                StartTime = 1000,
                Position = head,
                Path = new SliderPath(new[] { new PathControlPoint(Vector2.Zero, PathType.LINEAR), new PathControlPoint(end) }),
            };
            s.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            return s;
        }

        [Test]
        public void TestRotationKeepsHeadLengthAndStaysOnTheField()
        {
            var sliders = Enumerable.Range(0, 20).Select(_ => slider(new Vector2(256, 192), new Vector2(120, 0))).ToArray();
            int changed = 0;

            int count = SliderRandomiser.RotateRandomly(sliders, 180, new Random(5), _ => changed++);

            Assert.That(count, Is.EqualTo(20));
            Assert.That(changed, Is.EqualTo(20));

            foreach (var s in sliders)
            {
                Assert.That(s.Position, Is.EqualTo(new Vector2(256, 192)));
                Assert.That(s.Path.ControlPoints[1].Position.Length, Is.EqualTo(120).Within(0.01f));
            }

            Assert.That(sliders.Select(s => MathF.Round(s.Path.ControlPoints[1].Position.X)).Distinct().Count(), Is.GreaterThan(5), "angles vary");
        }

        [Test]
        public void TestRotationThatCannotFitLeavesTheSliderAlone()
        {
            // 700 pixels from a corner cannot fit on a 512 x 384 playfield at any angle.
            var tooLong = slider(new Vector2(5, 5), new Vector2(700, 0));
            int changed = 0;

            int count = SliderRandomiser.RotateRandomly(new[] { tooLong }, 180, new Random(1), _ => changed++);

            Assert.That(count, Is.EqualTo(0));
            Assert.That(changed, Is.EqualTo(0));
            Assert.That(tooLong.Path.ControlPoints[1].Position, Is.EqualTo(new Vector2(700, 0)));
        }

        [Test]
        public void TestJitterKeepsTheFirstPointAndMovesTheRest()
        {
            var jittered = SliderRandomiser.Jittered(new[] { Vector2.Zero, new Vector2(50, 0), new Vector2(100, 40) }, 10, new Random(2));

            Assert.That(jittered[0], Is.EqualTo(Vector2.Zero));
            Assert.That(Vector2.Distance(jittered[1], new Vector2(50, 0)), Is.GreaterThan(0).And.LessThanOrEqualTo(10.001f));
            Assert.That(Vector2.Distance(jittered[2], new Vector2(100, 40)), Is.LessThanOrEqualTo(10.001f));
        }

        [Test]
        public void TestJitterShapesIsDeterministicForASeed()
        {
            Vector2[] run(int seed)
            {
                var s = slider(new Vector2(200, 200), new Vector2(80, 0));
                s.Path.ControlPoints.Insert(1, new PathControlPoint(new Vector2(40, 30)));
                SliderRandomiser.JitterShapes(new[] { s }, 15, new Random(seed), _ => { });
                return s.Path.ControlPoints.Select(p => p.Position).ToArray();
            }

            Assert.That(run(3), Is.EqualTo(run(3)));
            Assert.That(run(3), Is.Not.EqualTo(run(4)));
        }
    }
}
