// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Framework.Graphics;
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
    /// YAWNS: the Sliderator port.
    /// </summary>
    [TestFixture]
    public class SlideratorTest
    {
        [Test]
        public void TestSpeedsUpInTheSameDuration()
        {
            var beatmap = new OsuBeatmap { BeatmapInfo = { Ruleset = new OsuRuleset().RulesetInfo } };
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            var slider = new Slider
            {
                StartTime = 1000,
                Position = new Vector2(100, 200),
                Path = new SliderPath(new[] { new PathControlPoint(Vector2.Zero, PathType.LINEAR), new PathControlPoint(new Vector2(200, 0)) }),
            };
            beatmap.HitObjects.Add(slider);
            var editorBeatmap = new EditorBeatmap(beatmap);
            slider.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            double duration = slider.Duration;
            Vector2 end = slider.EndPosition;

            Assert.That(SlideratorPopover.Sliderate(slider, Easing.InQuad, editorBeatmap), Is.Null);
            slider.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            Assert.That(slider.Duration, Is.EqualTo(duration).Within(1), "same duration");
            Assert.That(Vector2.Distance(slider.EndPosition, end), Is.LessThan(1.5f), "same end");
            Assert.That(slider.SliderVelocityMultiplier, Is.GreaterThan(1.5), "faster at the end than the plain slider");

            // Half way through, an InQuad ball has covered a quarter of the way (50 px), give or take a hidden zig-zag.
            Vector2 halfway = slider.Position + slider.Path.PositionAt(0.5);
            Assert.That(Math.Abs(halfway.X - 150), Is.LessThan(14), $"ball at {halfway}");
        }
    }
}
