// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Audio;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Timing;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.MappingTools;
using osuTK;

namespace osu.Game.Tests.Editing
{
    /// <summary>
    /// YAWNS: the Property Transformer port.
    /// </summary>
    [TestFixture]
    public class PropertyTransformerTest
    {
        private static EditorBeatmap beatmap()
        {
            var b = new OsuBeatmap { BeatmapInfo = { Ruleset = new OsuRuleset().RulesetInfo, Metadata = { PreviewTime = 1000 } } };
            b.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            b.ControlPointInfo.Add(2000, new EffectControlPoint { KiaiMode = true });

            b.HitObjects.Add(new HitCircle
            {
                StartTime = 1000,
                Samples = { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT, volume: 60) },
            });
            b.HitObjects.Add(new Slider
            {
                StartTime = 2000,
                SliderVelocityMultiplier = 1.5,
                Path = new SliderPath(new[] { new PathControlPoint(Vector2.Zero, PathType.LINEAR), new PathControlPoint(new Vector2(100, 0)) }),
            });
            b.HitObjects.Add(new Spinner { StartTime = 4000, Duration = 1000 });
            b.HitObjects.Add(new HitCircle { StartTime = 10000 }); // after the break, so the break stays valid (lazer drops breaks with nothing after them)

            b.Bookmarks = new[] { 1000, 3000 };
            b.Breaks.Add(new BreakPeriod(6000, 8000));

            return new EditorBeatmap(b);
        }

        [Test]
        public void TestDoubleTheTimes()
        {
            var b = beatmap();

            new PropertyTransformer
            {
                ObjectTime = new PropertyTransformer.Transform(2),
                TimingPointTime = new PropertyTransformer.Transform(2),
                BookmarkTime = new PropertyTransformer.Transform(2),
                BreakTime = new PropertyTransformer.Transform(2),
                PreviewTime = new PropertyTransformer.Transform(2),
            }.Apply(b);

            Assert.That(b.HitObjects.Select(h => h.StartTime), Is.EqualTo(new double[] { 2000, 4000, 8000, 20000 }));
            Assert.That(b.HitObjects.OfType<Spinner>().Single().EndTime, Is.EqualTo(10000), "spinner end follows");
            Assert.That(b.ControlPointInfo.EffectPoints.Single().Time, Is.EqualTo(4000));
            Assert.That(b.Bookmarks, Is.EqualTo(new[] { 2000, 6000 }));
            Assert.That(b.Breaks.Any(br => br.StartTime == 12000), "manual break moved");
            Assert.That(b.BeatmapInfo.Metadata.PreviewTime, Is.EqualTo(2000));
        }

        [Test]
        public void TestBpmAndVelocityAreClipped()
        {
            var b = beatmap();

            new PropertyTransformer
            {
                Bpm = new PropertyTransformer.Transform(1, 30),
                SliderVelocity = new PropertyTransformer.Transform(10),
            }.Apply(b);

            Assert.That(b.ControlPointInfo.TimingPoints.Single().BPM, Is.EqualTo(150).Within(0.001));
            Assert.That(b.HitObjects.OfType<Slider>().Single().SliderVelocityMultiplier, Is.EqualTo(10), "15x clipped to 10x");
        }

        [Test]
        public void TestVolumeAndIndex()
        {
            var b = beatmap();

            new PropertyTransformer
            {
                HitsoundVolume = new PropertyTransformer.Transform(2),
                HitsoundIndex = new PropertyTransformer.Transform(1, 2),
            }.Apply(b);

            var sample = b.HitObjects.OfType<HitCircle>().First().Samples.Single();
            Assert.That(sample.Volume, Is.EqualTo(100), "120 clipped to 100");
            Assert.That(sample.Suffix, Is.EqualTo("2"));
        }

        [Test]
        public void TestTimeRangeFilter()
        {
            var b = beatmap();

            new PropertyTransformer { ObjectTime = new PropertyTransformer.Transform(1, 100), MinTime = 1500, MaxTime = 3000 }.Apply(b);

            Assert.That(b.HitObjects.Select(h => h.StartTime), Is.EqualTo(new double[] { 1000, 2100, 4000, 10000 }));
        }

        [Test]
        public void TestOnlyValuesFilter()
        {
            var b = beatmap();
            b.Add(new HitCircle { StartTime = 1500, Samples = { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, volume: 80) } });

            new PropertyTransformer { HitsoundVolume = new PropertyTransformer.Transform(1, -20), OnlyValues = new[] { 80.0 } }.Apply(b);

            Assert.That(b.HitObjects.OfType<HitCircle>().Where(h => h.Samples.Any()).Select(h => h.Samples.Single().Volume), Is.EqualTo(new[] { 60, 60 }));
        }

        [Test]
        public void TestIsOneUndoStep()
        {
            var b = beatmap();
            int transactions = 0;
            b.TransactionEnded += () => transactions++;

            new PropertyTransformer { ObjectTime = new PropertyTransformer.Transform(1, 10), HitsoundVolume = new PropertyTransformer.Transform(1, 5) }.Apply(b);

            Assert.That(transactions, Is.EqualTo(1));
        }
    }
}
