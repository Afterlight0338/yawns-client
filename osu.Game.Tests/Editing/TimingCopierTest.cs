// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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
    /// YAWNS: the Timing Copier port.
    /// </summary>
    [TestFixture]
    public class TimingCopierTest
    {
        [Test]
        public void TestKeepBeats()
        {
            var target = beatmap(new[] { (0.0, 500.0) }, 1000, 1500, 2000);
            var source = beatmap(new[] { (0.0, 600.0) });

            Assert.That(copier(TimingCopier.ObjectHandling.KeepBeats, resnap: false).Copy(source, 100, target), Is.True);

            Assert.That(target.HitObjects.Select(h => h.StartTime), Is.EqualTo(new[] { 1300, 1900, 2500 }), "beats 2, 3 and 4 of the new timing");
            Assert.That(timing(target), Is.EqualTo(new[] { (100.0, 600.0) }));
        }

        [Test]
        public void TestKeepTimes()
        {
            var target = beatmap(new[] { (0.0, 500.0) }, 1000, 1510);
            var source = beatmap(new[] { (0.0, 600.0) });

            copier(TimingCopier.ObjectHandling.KeepTimes, resnap: false).Copy(source, 0, target);

            Assert.That(target.HitObjects.Select(h => h.StartTime), Is.EqualTo(new[] { 1000, 1510 }));
            Assert.That(timing(target), Is.EqualTo(new[] { (0.0, 600.0) }));
        }

        [Test]
        public void TestKeepTimesAndResnap()
        {
            var target = beatmap(new[] { (0.0, 500.0) }, 1195);
            var source = beatmap(new[] { (0.0, 600.0) });

            copier(TimingCopier.ObjectHandling.KeepTimes, resnap: true).Copy(source, 0, target);

            Assert.That(target.HitObjects.Single().StartTime, Is.EqualTo(1200), "snapped onto beat 2 of the new timing");
        }

        [Test]
        public void TestSeveralTimingSections()
        {
            var target = beatmap(new[] { (0.0, 500.0) }, Enumerable.Range(0, 9).Select(beat => beat * 500.0).ToArray());
            var source = beatmap(new[] { (0.0, 500.0), (2000.0, 250.0) });

            copier(TimingCopier.ObjectHandling.KeepBeats, resnap: false).Copy(source, 0, target);

            Assert.That(target.HitObjects.Select(h => h.StartTime), Is.EqualTo(new[] { 0, 500, 1000, 1500, 2000, 2250, 2500, 2750, 3000 }));
            Assert.That(timing(target), Is.EqualTo(new[] { (0.0, 500.0), (2000.0, 250.0) }));
        }

        [Test]
        public void TestKiaiBookmarksAndSpinnersFollowTheBeats()
        {
            var target = beatmap(new[] { (0.0, 500.0) });
            target.Add(new Spinner { StartTime = 1000, Duration = 1000 });
            target.ControlPointInfo.Add(2000, new EffectControlPoint { KiaiMode = true });
            target.Bookmarks.Add(1500);

            var source = beatmap(new[] { (0.0, 600.0) });

            copier(TimingCopier.ObjectHandling.KeepBeats, resnap: false).Copy(source, 0, target);

            var spinner = (Spinner)target.HitObjects.Single();
            Assert.That((spinner.StartTime, spinner.Duration), Is.EqualTo((1200.0, 1200.0)));
            Assert.That(target.ControlPointInfo.EffectPoints.Single(e => e.KiaiMode).Time, Is.EqualTo(2400));
            Assert.That(target.Bookmarks.Single(), Is.EqualTo(1800));
        }

        [Test]
        public void TestIsOneUndoableChange()
        {
            var target = beatmap(new[] { (0.0, 500.0) }, 1000);
            var source = beatmap(new[] { (0.0, 600.0) });
            var changeHandler = new BeatmapEditorChangeHandler(target);

            copier(TimingCopier.ObjectHandling.KeepBeats, resnap: false).Copy(source, 0, target);
            changeHandler.RestoreState(-1);

            Assert.That(target.HitObjects.Single().StartTime, Is.EqualTo(1000));
            Assert.That(timing(target), Is.EqualTo(new[] { (0.0, 500.0) }));
        }

        [Test]
        public void TestNothingToCopy()
        {
            var target = beatmap(new[] { (0.0, 500.0) }, 1000);
            var source = beatmap(new (double, double)[0]);

            Assert.That(copier(TimingCopier.ObjectHandling.KeepBeats, resnap: true).Copy(source, 0, target), Is.False);
            Assert.That(timing(target), Is.EqualTo(new[] { (0.0, 500.0) }));
        }

        private static TimingCopier copier(TimingCopier.ObjectHandling objects, bool resnap)
        {
            var copier = new TimingCopier();
            copier.Objects.Value = objects;
            copier.Resnap.Value = resnap;
            return copier;
        }

        private static (double, double)[] timing(EditorBeatmap beatmap) => beatmap.ControlPointInfo.TimingPoints.Select(t => (t.Time, t.BeatLength)).ToArray();

        private static EditorBeatmap beatmap((double time, double beatLength)[] timingPoints, params double[] circleTimes)
        {
            var osuBeatmap = new OsuBeatmap { BeatmapInfo = { Ruleset = new OsuRuleset().RulesetInfo } };

            foreach (var (time, beatLength) in timingPoints)
                osuBeatmap.ControlPointInfo.Add(time, new TimingControlPoint { BeatLength = beatLength });

            foreach (double time in circleTimes)
                osuBeatmap.HitObjects.Add(new HitCircle { StartTime = time });

            foreach (var h in osuBeatmap.HitObjects)
                h.ApplyDefaults(osuBeatmap.ControlPointInfo, osuBeatmap.Difficulty);

            return new EditorBeatmap(osuBeatmap);
        }
    }
}
