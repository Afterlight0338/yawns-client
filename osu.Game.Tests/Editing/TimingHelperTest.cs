// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.MappingTools;

namespace osu.Game.Tests.Editing
{
    /// <summary>
    /// YAWNS: the Timing Helper port.
    /// </summary>
    [TestFixture]
    public class TimingHelperTest
    {
        private static EditorBeatmap map(double beatLength, IEnumerable<double> times)
        {
            var b = new OsuBeatmap { BeatmapInfo = { Ruleset = new OsuRuleset().RulesetInfo } };
            b.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = beatLength });
            b.HitObjects.AddRange(times.Select(t => new HitCircle { StartTime = Math.Round(t) }));
            return new EditorBeatmap(b);
        }

        [Test]
        public void TestFixesTheBpm()
        {
            // Sounds every beat at 180 BPM, roughly timed at 179.8 (the auto beat count needs timing that is close).
            var beatmap = map(60000 / 179.8, Enumerable.Range(0, 24).Select(i => i * 60000 / 180.0));

            int added = new TimingHelper().Apply(beatmap);

            Assert.That(added, Is.EqualTo(0));
            Assert.That(60000 / beatmap.ControlPointInfo.TimingPoints.Single().BeatLength, Is.EqualTo(180).Within(0.001));
        }

        [Test]
        public void TestAddsRedLineAtTempoChange()
        {
            // 180 BPM for 16 beats, then 200 BPM.
            double change = 16 * 60000 / 180.0;
            var times = Enumerable.Range(0, 17).Select(i => i * 60000 / 180.0)
                                  .Concat(Enumerable.Range(1, 16).Select(i => change + i * 60000 / 200.0));
            var beatmap = map(60000 / 180.0, times);
            var helper = new TimingHelper();
            helper.BeatsBetween.Value = 1; // markers on every beat

            int added = helper.Apply(beatmap);

            Assert.That(added, Is.EqualTo(1));
            var second = beatmap.ControlPointInfo.TimingPoints[1];
            Assert.That(second.Time, Is.EqualTo(Math.Round(change)).Within(1));
            Assert.That(60000 / second.BeatLength, Is.EqualTo(200).Within(0.001));

            foreach (var h in beatmap.HitObjects)
            {
                var redLine = beatmap.ControlPointInfo.TimingPointAt(h.StartTime);
                double beats = (h.StartTime - redLine.Time) / redLine.BeatLength;
                Assert.That(Math.Abs(beats * 4 - Math.Round(beats * 4)) * redLine.BeatLength / 4, Is.LessThanOrEqualTo(3), $"object at {h.StartTime} is off the grid");
            }
        }
    }
}
