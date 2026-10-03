// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit.MappingTools.Hitsounds;
using osuTK;

namespace osu.Game.Tests.Editing
{
    /// <summary>
    /// YAWNS: <see cref="HitsoundProject"/>, the Hitsound Studio port.
    /// </summary>
    [TestFixture]
    public class HitsoundProjectTest
    {
        private static readonly HitsoundSound soft_normal = new HitsoundSound(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT);
        private static readonly HitsoundSound soft_clap = new HitsoundSound(HitSampleInfo.HIT_CLAP, HitSampleInfo.BANK_SOFT);
        private static readonly HitsoundSound soft_whistle = new HitsoundSound(HitSampleInfo.HIT_WHISTLE, HitSampleInfo.BANK_SOFT);
        private static readonly HitsoundSound drum_finish = new HitsoundSound(HitSampleInfo.HIT_FINISH, HitSampleInfo.BANK_DRUM);
        private static readonly HitsoundSound drum_normal = new HitsoundSound(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_DRUM);
        private static readonly HitsoundSound normal_clap_2 = new HitsoundSound(HitSampleInfo.HIT_CLAP, HitSampleInfo.BANK_NORMAL, 2);

        private static HitObject circle(double time, Vector2 position) => new HitCircle { StartTime = time, Position = position };

        [Test]
        public void TestSimultaneousSameBankShareOneObject()
        {
            var objects = HitsoundProject.Generate(new[]
            {
                new HitsoundTrigger(1000, soft_normal, 80),
                new HitsoundTrigger(1000, soft_clap, 80),
                new HitsoundTrigger(1000, soft_whistle, 80),
            }, circle);

            Assert.That(objects, Has.Count.EqualTo(1));
            Assert.That(((HitCircle)objects[0]).Position, Is.EqualTo(HitsoundProject.POSITION));
            Assert.That(objects[0].Samples.Select(s => s.Name), Is.EquivalentTo(new[] { HitSampleInfo.HIT_NORMAL, HitSampleInfo.HIT_CLAP, HitSampleInfo.HIT_WHISTLE }));
        }

        [Test]
        public void TestDifferentAdditionBanksSplit()
        {
            var objects = HitsoundProject.Generate(new[]
            {
                new HitsoundTrigger(1000, soft_normal, 100),
                new HitsoundTrigger(1000, soft_clap, 100),
                new HitsoundTrigger(1000, drum_finish, 100),
            }, circle);

            Assert.That(objects, Has.Count.EqualTo(2), "one addition bank per object");
            Assert.That(objects.Count(o => o.Samples.Any(s => s.Name == HitSampleInfo.HIT_NORMAL && s.Bank == HitSampleInfo.BANK_SOFT)), Is.EqualTo(1));
        }

        [Test]
        public void TestDifferentIndexSplitsNormal()
        {
            var objects = HitsoundProject.Generate(new[]
            {
                new HitsoundTrigger(1000, soft_normal, 100),
                new HitsoundTrigger(1000, normal_clap_2, 100),
            }, circle);

            Assert.That(objects, Has.Count.EqualTo(2), "one custom index per object");
            Assert.That(objects.Single(o => o.Samples.Any(s => s.Name == HitSampleInfo.HIT_CLAP)).Samples.All(s => s.Suffix == "2"));
        }

        [Test]
        public void TestObjectVolumeIsTheLoudest()
        {
            var objects = HitsoundProject.Generate(new[]
            {
                new HitsoundTrigger(1000, soft_normal, 40),
                new HitsoundTrigger(1000, soft_clap, 90),
            }, circle);

            Assert.That(objects.Single().Samples.Select(s => s.Volume), Is.All.EqualTo(90));
        }

        [Test]
        public void TestImportSliderNodes()
        {
            var beatmap = new OsuBeatmap { BeatmapInfo = { Ruleset = new OsuRuleset().RulesetInfo } };
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });

            var slider = new Slider
            {
                StartTime = 1000,
                RepeatCount = 1,
                Path = new SliderPath(new[] { new PathControlPoint(Vector2.Zero, PathType.LINEAR), new PathControlPoint(new Vector2(140, 0)) }),
                NodeSamples =
                {
                    new[] { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT) },
                    new[] { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT), new HitSampleInfo(HitSampleInfo.HIT_CLAP, HitSampleInfo.BANK_SOFT) },
                    new[] { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_DRUM) },
                },
            };
            beatmap.HitObjects.Add(slider);
            slider.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            var triggers = HitsoundProject.Import(beatmap);
            double span = slider.SpanDuration;

            Assert.That(triggers.Select(t => (t.Time, t.Sound)), Is.EquivalentTo(new[]
            {
                (1000.0, soft_normal),
                (System.Math.Round(1000 + span), soft_normal),
                (System.Math.Round(1000 + span), soft_clap),
                (System.Math.Round(1000 + 2 * span), drum_normal),
            }));
        }

        [Test]
        public void TestSurvivesOsuFile()
        {
            var triggers = new[]
            {
                new HitsoundTrigger(1000, soft_normal, 70),
                new HitsoundTrigger(1000, soft_clap, 70),
                new HitsoundTrigger(1250, drum_normal, 50),
                new HitsoundTrigger(1250, drum_finish, 50),
                new HitsoundTrigger(1500, normal_clap_2, 100),
                new HitsoundTrigger(1500, new HitsoundSound(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_NORMAL, 2), 100),
                new HitsoundTrigger(1750, new HitsoundSound(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_NORMAL, File: "kick.wav"), 60),
            };

            var beatmap = new OsuBeatmap { BeatmapInfo = { Ruleset = new OsuRuleset().RulesetInfo } };
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            beatmap.HitObjects.AddRange(HitsoundProject.Generate(triggers, circle).Cast<OsuHitObject>());

            foreach (var h in beatmap.HitObjects)
                h.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            var stream = new MemoryStream();
            using (var writer = new StreamWriter(stream, Encoding.UTF8, 1024, true))
                new LegacyBeatmapEncoder(beatmap, null, null).Encode(writer);
            stream.Position = 0;

            IBeatmap decoded;
            using (var reader = new LineBufferedReader(stream))
                decoded = new FlatWorkingBeatmap(new LegacyBeatmapDecoder { ApplyOffsets = false }.Decode(reader)).GetPlayableBeatmap(new OsuRuleset().RulesetInfo);

            Assert.That(HitsoundProject.IsHitsoundDifficulty(decoded));

            // A custom file sample also plays the object's hitnormal in osu!, so ignore that one extra trigger.
            var imported = HitsoundProject.Import(decoded).Where(t => !(t.Time == 1750 && t.Sound.File == null)).ToList();

            Assert.That(imported, Is.EquivalentTo(triggers));
        }

        [Test]
        public void TestGameplayDifficultyIsNotAHitsoundDifficulty()
        {
            var beatmap = new OsuBeatmap();
            beatmap.HitObjects.Add(new HitCircle { StartTime = 0, Position = HitsoundProject.POSITION });
            beatmap.HitObjects.Add(new HitCircle { StartTime = 500, Position = new Vector2(100, 100) });

            Assert.That(HitsoundProject.IsHitsoundDifficulty(beatmap), Is.False);
        }
    }
}
