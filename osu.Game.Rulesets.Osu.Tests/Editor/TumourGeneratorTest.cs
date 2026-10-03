// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Edit;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.MappingTools;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Editor
{
    /// <summary>
    /// YAWNS: the Tumour Generator port.
    /// </summary>
    [TestFixture]
    public class TumourGeneratorTest
    {
        [TestCase(TumourGenerator.Shape.Triangle)]
        [TestCase(TumourGenerator.Shape.Square)]
        [TestCase(TumourGenerator.Shape.Circle)]
        [TestCase(TumourGenerator.Shape.Parabola)]
        public void TestTumoursKeepEndsAndDuration(TumourGenerator.Shape shape)
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

            var generator = new TumourGenerator();
            generator.Template.Value = shape;

            Assert.That(TumourGeneratorPopover.Grow(slider, generator, editorBeatmap), Is.Null);
            slider.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            Assert.That(slider.Duration, Is.EqualTo(duration).Within(1), "same duration");
            Assert.That(Vector2.Distance(slider.EndPosition, end), Is.LessThan(0.5f), "same end");
            Assert.That(slider.Path.Distance, Is.GreaterThan(220), "longer path");

            // 5 tumours (at 0, 40, 80, 120, 160 px), alternating sides, each 15 px high (a parabola peaks at half its control point).
            var points = new List<Vector2>();
            slider.Path.GetPathToProgress(points, 0, 1);
            float highest = points.Max(p => p.Y), lowest = points.Min(p => p.Y);
            const float expected = 15;
            Assert.That(highest, Is.EqualTo(expected).Within(1));
            Assert.That(lowest, Is.EqualTo(-expected).Within(1));
        }
    }
}
