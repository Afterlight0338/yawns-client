// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Edit;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Editor
{
    /// <summary>
    /// YAWNS: the Slider Completionator port.
    /// </summary>
    [TestFixture]
    public class SliderCompletionatorTest
    {
        private Slider slider = null!;
        private EditorBeatmap beatmap = null!;

        [SetUp]
        public void SetUp()
        {
            var b = new OsuBeatmap { BeatmapInfo = { Ruleset = new OsuRuleset().RulesetInfo }, Difficulty = { SliderMultiplier = 1.4 } };
            b.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });

            b.HitObjects.Add(slider = new Slider
            {
                StartTime = 1000,
                Path = new SliderPath(new[] { new PathControlPoint(Vector2.Zero, PathType.LINEAR), new PathControlPoint(new Vector2(140, 0)) }),
            });

            beatmap = new EditorBeatmap(b);
        }

        [Test]
        public void TestDurationInBeatsWorksOutVelocity()
        {
            var tool = new SliderCompletionator { Beats = { Value = 2 } };
            tool.Apply(new[] { slider }, beatmap, 0);

            Assert.That(slider.SpanDuration, Is.EqualTo(1000).Within(1), "two beats at 120 BPM");
            Assert.That(slider.Path.Distance, Is.EqualTo(140).Within(0.5), "length kept");
            Assert.That(slider.SliderVelocityMultiplier, Is.EqualTo(0.5).Within(0.001));
        }

        [Test]
        public void TestEndAtPlayhead()
        {
            var tool = new SliderCompletionator { Duration = { Value = SliderCompletionator.DurationSource.Playhead } };
            tool.Apply(new[] { slider }, beatmap, 1750);

            Assert.That(slider.EndTime, Is.EqualTo(1750).Within(1));
        }

        [Test]
        public void TestKeepVelocityWorksOutLength()
        {
            var tool = new SliderCompletionator
            {
                Free = { Value = SliderCompletionator.FreeVariable.Length },
                Beats = { Value = 1 },
                Velocity = { Value = 2 },
            };
            tool.Apply(new[] { slider }, beatmap, 0);

            // 100 * 1.4 * 2 px per beat.
            Assert.That(slider.Path.Distance, Is.EqualTo(280).Within(0.5));
            Assert.That(slider.SpanDuration, Is.EqualTo(500).Within(1));
        }

        [Test]
        public void TestMoveAnchorsScalesThePath()
        {
            var tool = new SliderCompletionator { Free = { Value = SliderCompletionator.FreeVariable.Length }, Beats = { Value = 0.5 }, MoveAnchors = { Value = true } };
            tool.Apply(new[] { slider }, beatmap, 0);

            Assert.That(slider.Path.ControlPoints[1].Position.X, Is.EqualTo(70).Within(0.5), "half a beat at 1x is 70px");
            Assert.That(slider.Path.ExpectedDistance.Value, Is.Null);
        }

        [Test]
        public void TestImpossibleVelocityKeepsDuration()
        {
            var tool = new SliderCompletionator { Beats = { Value = 0.125 }, Length = { Value = 4 } }; // 560px in 62.5ms would need 32x
            var (_, clamped) = tool.Apply(new[] { slider }, beatmap, 0);

            Assert.That(clamped, Is.EqualTo(1));
            Assert.That(slider.SpanDuration, Is.EqualTo(62.5).Within(1), "the length gave way instead");
        }
    }
}
