// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Audio;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.MappingTools;
using osu.Game.Screens.Edit.MappingTools.Hitsounds;
using osuTK;

namespace osu.Game.Tests.Editing
{
    /// <summary>
    /// YAWNS: the Rhythm Guide port.
    /// </summary>
    [TestFixture]
    public class RhythmGuideTest
    {
        private static OsuBeatmap map(params (double Time, bool Clap)[] objects)
        {
            var b = new OsuBeatmap { BeatmapInfo = { Ruleset = new OsuRuleset().RulesetInfo } };
            b.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 480 });

            foreach (var (time, clap) in objects)
            {
                var circle = new HitCircle { StartTime = time, Position = new Vector2(100, 100), Samples = { new HitSampleInfo(HitSampleInfo.HIT_NORMAL) } };
                if (clap)
                    circle.Samples.Add(new HitSampleInfo(HitSampleInfo.HIT_CLAP));
                b.HitObjects.Add(circle);
            }

            return b;
        }

        [Test]
        public void TestTimesSnapAndMerge()
        {
            // 481 is a millisecond off beat 2; 640 is 1/3 into it (1/12 grid), 1000 + 1000 from both maps once.
            var a = map((481, false), (1000, true));
            var b = map((640, false), (1000, false));

            var times = RhythmGuide.Times(new[] { a, b }, RhythmGuide.Events.All, a);

            Assert.That(times, Is.EqualTo(new double[] { 480, 640, 1000 }));
        }

        [Test]
        public void TestOnlyHitsounds()
        {
            var a = map((480, false), (960, true));

            Assert.That(RhythmGuide.Times(new[] { a }, RhythmGuide.Events.Hitsounds, a), Is.EqualTo(new double[] { 960 }));
        }

        [Test]
        public void TestAddsWhereNothingIs()
        {
            var target = new EditorBeatmap(map((480, false)));

            var added = RhythmGuide.AddTo(target, new double[] { 480, 960, 1440 }, newComboEverything: true);

            Assert.That(added.Select(h => h.StartTime), Is.EqualTo(new double[] { 960, 1440 }));
            Assert.That(added.Cast<HitCircle>().All(c => c.Position == HitsoundProject.POSITION && c.NewCombo));
            Assert.That(target.HitObjects, Has.Count.EqualTo(3));
        }
    }
}
