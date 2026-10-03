// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
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
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.MappingTools;
using osuTK;

namespace osu.Game.Tests.Editing
{
    /// <summary>
    /// YAWNS: the Hitsound Copier port.
    /// </summary>
    [TestFixture]
    public class HitsoundCopierTest
    {
        [Test]
        public void TestCopiesOntoMatchingHitsoundsOnly()
        {
            var source = beatmap(
                circle(1000, sample(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT, 60), sample(HitSampleInfo.HIT_WHISTLE, HitSampleInfo.BANK_DRUM, 60)),
                circle(2000, sample(HitSampleInfo.HIT_NORMAL)));

            var target = beatmap(
                circle(1003, sample(HitSampleInfo.HIT_NORMAL)),
                circle(1500, sample(HitSampleInfo.HIT_NORMAL), sample(HitSampleInfo.HIT_CLAP)),
                circle(2050, sample(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_DRUM, 30)));

            int matched = new HitsoundCopier().Copy(source, 0, target);

            Assert.That(matched, Is.EqualTo(1), "only the 1003ms circle is within 5ms of a source hitsound");
            Assert.That(describe(target.HitObjects[0]), Is.EqualTo("hitnormal:soft:60 hitwhistle:drum:60"));
            Assert.That(describe(target.HitObjects[1]), Is.EqualTo("hitnormal:normal:100 hitclap:normal:100"), "unmatched hitsounds are left alone");
            Assert.That(describe(target.HitObjects[2]), Is.EqualTo("hitnormal:drum:30"), "50ms away is outside the leniency");
        }

        [Test]
        public void TestSourceOffset()
        {
            var source = beatmap(circle(1000, sample(HitSampleInfo.HIT_FINISH)));
            var target = beatmap(circle(1500, sample(HitSampleInfo.HIT_NORMAL)));

            Assert.That(new HitsoundCopier().Copy(source, 500, target), Is.EqualTo(1));
            Assert.That(describe(target.HitObjects[0]), Is.EqualTo("hitfinish:normal:100"));
        }

        [Test]
        public void TestKeepTargetSampleSetsAndVolumes()
        {
            var source = beatmap(circle(1000, sample(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT, 60), sample(HitSampleInfo.HIT_CLAP, HitSampleInfo.BANK_SOFT, 60)));
            var target = beatmap(circle(1000, sample(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_DRUM, 80)));

            var copier = new HitsoundCopier();
            copier.CopySampleSets.Value = false;
            copier.CopyVolumes.Value = false;
            copier.Copy(source, 0, target);

            Assert.That(describe(target.HitObjects[0]), Is.EqualTo("hitnormal:drum:80 hitclap:drum:80"), "only the addition is copied");
        }

        [Test]
        public void TestOverwriteEverything()
        {
            var source = beatmap(
                circle(1000, sample(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT, 40)),
                circle(3000, sample(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_DRUM, 70)));

            var target = beatmap(
                circle(1000, sample(HitSampleInfo.HIT_NORMAL)),
                circle(2000, sample(HitSampleInfo.HIT_NORMAL), sample(HitSampleInfo.HIT_WHISTLE)),
                circle(4000, sample(HitSampleInfo.HIT_NORMAL), sample(HitSampleInfo.HIT_FINISH)));

            var copier = new HitsoundCopier();
            copier.OverwriteEverything.Value = true;
            copier.Copy(source, 0, target);

            Assert.That(describe(target.HitObjects[0]), Is.EqualTo("hitnormal:soft:40"));
            Assert.That(describe(target.HitObjects[1]), Is.EqualTo("hitnormal:soft:40"), "unmatched: additions removed, source bank and volume at that moment applied");
            Assert.That(describe(target.HitObjects[2]), Is.EqualTo("hitnormal:drum:70"));
        }

        [Test]
        public void TestSliderNodesAndBody()
        {
            var sourceSlider = slider(1000, sample(HitSampleInfo.HIT_WHISTLE), sample(HitSampleInfo.HIT_CLAP), sample(HitSampleInfo.HIT_FINISH));
            sourceSlider.Samples = new[] { sample(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT) }.ToList();

            var targetSlider = slider(1000, sample(HitSampleInfo.HIT_NORMAL), sample(HitSampleInfo.HIT_NORMAL), sample(HitSampleInfo.HIT_NORMAL));

            var source = beatmap(sourceSlider);
            var target = beatmap(targetSlider);

            Assert.That(new HitsoundCopier().Copy(source, 0, target), Is.EqualTo(3), "head, repeat and tail");

            Assert.That(targetSlider.NodeSamples.Select(n => string.Join(" ", n.Select(s => s.Name))), Is.EqualTo(new[] { "hitwhistle", "hitclap", "hitfinish" }));
            Assert.That(targetSlider.Samples.Single().Bank, Is.EqualTo(HitSampleInfo.BANK_SOFT), "slider body sound copied");
        }

        [Test]
        public void TestNeverCopiesTwoSourcesOntoOneTarget()
        {
            var source = beatmap(circle(1000, sample(HitSampleInfo.HIT_WHISTLE)), circle(1002, sample(HitSampleInfo.HIT_CLAP)));
            var target = beatmap(circle(1001, sample(HitSampleInfo.HIT_NORMAL)));

            Assert.That(new HitsoundCopier().Copy(source, 0, target), Is.EqualTo(1));
            Assert.That(describe(target.HitObjects[0]), Is.EqualTo("hitwhistle:normal:100"), "the first source hitsound wins");
        }

        [Test]
        public void TestIsOneUndoableChange()
        {
            var source = beatmap(circle(1000, sample(HitSampleInfo.HIT_FINISH)), circle(2000, sample(HitSampleInfo.HIT_CLAP)));
            var target = beatmap(circle(1000, sample(HitSampleInfo.HIT_NORMAL)), circle(2000, sample(HitSampleInfo.HIT_NORMAL)));

            var changeHandler = new BeatmapEditorChangeHandler(target);

            new HitsoundCopier().Copy(source, 0, target);
            Assert.That(describe(target.HitObjects[1]), Is.EqualTo("hitclap:normal:100"));

            changeHandler.RestoreState(-1);
            Assert.That(target.HitObjects.Select(describe), Is.EqualTo(new[] { "hitnormal:normal:100", "hitnormal:normal:100" }));
        }

        [Test]
        public void TestCopiedHitsoundsSurviveSaving()
        {
            var source = beatmap(
                circle(1000, sample(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT, 60), sample(HitSampleInfo.HIT_WHISTLE, HitSampleInfo.BANK_DRUM, 60)),
                slider(2000,
                    new[] { sample(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT, 80), sample(HitSampleInfo.HIT_CLAP, HitSampleInfo.BANK_SOFT, 80) },
                    new[] { sample(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT, 80) },
                    new[] { sample(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT, 80), sample(HitSampleInfo.HIT_FINISH, HitSampleInfo.BANK_SOFT, 80) }));

            var target = beatmap(
                circle(1000, sample(HitSampleInfo.HIT_NORMAL)),
                slider(2000, new[] { sample(HitSampleInfo.HIT_NORMAL) }, new[] { sample(HitSampleInfo.HIT_NORMAL) }, new[] { sample(HitSampleInfo.HIT_NORMAL) }));

            new HitsoundCopier().Copy(source, 0, target);

            // Encode to .osu and read it back, the way saving and reopening in the editor does.
            using var stream = new MemoryStream();

            using (var writer = new StreamWriter(stream, Encoding.UTF8, 1024, true))
                new LegacyBeatmapEncoder(target, null, null).Encode(writer);

            stream.Position = 0;

            using var reader = new LineBufferedReader(stream);
            var saved = new FlatWorkingBeatmap(new LegacyBeatmapDecoder { ApplyOffsets = false }.Decode(reader)).GetPlayableBeatmap(new OsuRuleset().RulesetInfo);

            Assert.That(saved.HitObjects.Select(describeAll), Is.EqualTo(source.HitObjects.Select(describeAll)));
        }

        private static string describeAll(HitObject hitObject) => hitObject is IHasRepeats repeats
            ? $"nodes [{string.Join(" | ", repeats.NodeSamples.Select(n => describeSamples(n)))}]"
            : describeSamples(hitObject.Samples);

        private static string describeSamples(IEnumerable<HitSampleInfo> samples) => string.Join(" ", samples.Select(s => $"{s.Name}:{s.Bank}:{s.Volume}"));

        private static EditorBeatmap beatmap(params HitObject[] hitObjects)
        {
            var osuBeatmap = new OsuBeatmap { BeatmapInfo = { Ruleset = new OsuRuleset().RulesetInfo } };
            osuBeatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            osuBeatmap.HitObjects.AddRange(hitObjects.Cast<OsuHitObject>());

            // The game applies defaults before the editor sees a beatmap, slider durations depend on it.
            foreach (var h in osuBeatmap.HitObjects)
                h.ApplyDefaults(osuBeatmap.ControlPointInfo, osuBeatmap.Difficulty);

            return new EditorBeatmap(osuBeatmap);
        }

        private static HitCircle circle(double time, params HitSampleInfo[] samples) => new HitCircle { StartTime = time, Samples = samples.ToList() };

        /// <summary>
        /// A slider with one sample per node (head, repeats, tail).
        /// </summary>
        private static Slider slider(double time, params HitSampleInfo[] nodeSamples) => slider(time, nodeSamples.Select(s => new[] { s }).ToArray());

        /// <summary>
        /// A slider with the given samples per node (head, repeats, tail).
        /// </summary>
        private static Slider slider(double time, params HitSampleInfo[][] nodeSamples) => new Slider
        {
            StartTime = time,
            Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(200, 0) }),
            RepeatCount = nodeSamples.Length - 2,
            NodeSamples = nodeSamples.Select(n => (IList<HitSampleInfo>)n.ToList()).ToList(),
            Samples = new[] { sample(HitSampleInfo.HIT_NORMAL) }.ToList(),
        };

        // Explicit banks, as decoded from a .osu where the sample set was specified. Auto banks would be saved as "follow the normal sample".
        private static HitSampleInfo sample(string name, string bank = HitSampleInfo.BANK_NORMAL, int volume = 100) => new HitSampleInfo(name, bank, volume: volume, editorAutoBank: false);

        private static string describe(HitObject hitObject) => string.Join(" ", hitObject.Samples.Select(s => $"{s.Name}:{s.Bank}:{s.Volume}"));
    }
}
