// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using ManagedBass;
using NUnit.Framework;
using osu.Game.Audio;
using osu.Game.Screens.Edit.MappingTools.Hitsounds;
using static osu.Game.Screens.Edit.MappingTools.Hitsounds.HitsoundExporter;

namespace osu.Game.Tests.Editing
{
    /// <summary>
    /// YAWNS: <see cref="HitsoundExporter"/>, the Hitsounds tab's export as Mapping Tools does it: one circle per moment, nothing lost.
    /// </summary>
    [TestFixture]
    public class HitsoundExporterTest
    {
        private static HitsoundLane lane(string bank, HitsoundAddition addition, int index = 0, int volume = 100, string? file = null) =>
            new HitsoundLane { Bank = bank, Addition = addition, Index = index, Volume = volume, File = file };

        private static HitsoundTrigger hit(HitsoundLane lane, double time, int? volume = null) => new HitsoundTrigger { LaneId = lane.Id, Time = time, Volume = volume };

        private static Result build(IReadOnlyList<HitsoundLane> lanes, IEnumerable<HitsoundTrigger> hits, IEnumerable<string>? files = null, ISet<int>? reserved = null) =>
            Build(lanes, hits, files ?? Array.Empty<string>(), reserved ?? new HashSet<int>());

        private static IEnumerable<(string Name, string Bank)> samplesOf(Result result, int circle) =>
            result.Circles[circle].Samples.Select(s => (s.Name, s.Bank));

        [Test]
        public void TestOneCirclePerMomentWithSkinSamples()
        {
            var normal = lane(HitSampleInfo.BANK_SOFT, HitsoundAddition.None);
            var clap = lane(HitSampleInfo.BANK_SOFT, HitsoundAddition.Clap);
            var whistle = lane(HitSampleInfo.BANK_SOFT, HitsoundAddition.Whistle);

            var result = build(new[] { normal, clap, whistle }, new[] { hit(normal, 1000), hit(clap, 1000), hit(whistle, 1000), hit(normal, 1500) });

            Assert.That(result.Circles, Has.Count.EqualTo(2));
            Assert.That(result.Circles.Select(c => ((osu.Game.Rulesets.Objects.Types.IHasPosition)c).Position), Is.All.EqualTo(HitsoundProject.POSITION));
            Assert.That(samplesOf(result, 0), Is.EquivalentTo(new[]
            {
                (HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT),
                (HitSampleInfo.HIT_WHISTLE, HitSampleInfo.BANK_SOFT),
                (HitSampleInfo.HIT_CLAP, HitSampleInfo.BANK_SOFT),
            }));

            Assert.That(result.Files, Is.Empty, "the skin already plays all of it at index 0");
            Assert.That(result.Indices, Is.EqualTo(new[] { 0 }));
            Assert.That(result.Circles.SelectMany(c => c.Samples).All(s => !s.UseBeatmapSamples && s.Suffix == null));
        }

        [Test]
        public void TestDifferentBanksForNormalAndAdditionsStillOneCircle()
        {
            var kick = lane(HitSampleInfo.BANK_DRUM, HitsoundAddition.None);
            var clap = lane(HitSampleInfo.BANK_SOFT, HitsoundAddition.Clap);

            var result = build(new[] { clap, kick }, new[] { hit(kick, 1000), hit(clap, 1000) });

            Assert.That(result.Circles, Has.Count.EqualTo(1));
            Assert.That(samplesOf(result, 0), Is.EquivalentTo(new[] { (HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_DRUM), (HitSampleInfo.HIT_CLAP, HitSampleInfo.BANK_SOFT) }));
            Assert.That(result.Files, Is.Empty);
        }

        [Test]
        public void TestAdditionOnlyMomentGetsSilentHitnormal()
        {
            var clap = lane(HitSampleInfo.BANK_SOFT, HitsoundAddition.Clap);

            var result = build(new[] { clap }, new[] { hit(clap, 1000) });

            Assert.That(result.Circles, Has.Count.EqualTo(1));
            Assert.That(result.Indices, Is.EqualTo(new[] { FIRST_NEW_INDEX }));
            Assert.That(result.Circles[0].Samples.All(s => s.Suffix == FIRST_NEW_INDEX.ToString() && s.UseBeatmapSamples));

            var normalFile = result.Files.Single(f => f.Filename.StartsWith("normal-hitnormal", StringComparison.Ordinal));
            Assert.That(normalFile.Filename, Is.EqualTo($"normal-hitnormal{FIRST_NEW_INDEX}.wav"));
            Assert.That(normalFile.Layers.Single().Source.Kind, Is.EqualTo(SourceKind.Blank));

            var clapFile = result.Files.Single(f => f.Filename.StartsWith("soft-hitclap", StringComparison.Ordinal));
            Assert.That(clapFile.Layers.Single(), Is.EqualTo(new Layer(new SampleSource(SourceKind.Default, "soft-hitclap"), 1)));
        }

        [Test]
        public void TestQuieterHitIsBakedAndCircleTakesLoudest()
        {
            var normal = lane(HitSampleInfo.BANK_SOFT, HitsoundAddition.None, volume: 80);
            var clap = lane(HitSampleInfo.BANK_SOFT, HitsoundAddition.Clap, volume: 40);

            var result = build(new[] { normal, clap }, new[] { hit(normal, 1000), hit(clap, 1000) });

            Assert.That(result.Circles.Single().Samples.Select(s => s.Volume), Is.All.EqualTo(80));

            var clapLayer = result.Files.Single(f => f.Filename.StartsWith("soft-hitclap", StringComparison.Ordinal)).Layers.Single();
            Assert.That(clapLayer.Amplitude, Is.EqualTo(VolumeToAmplitude(0.4) / VolumeToAmplitude(0.8)).Within(1e-4));

            var normalLayer = result.Files.Single(f => f.Filename.StartsWith("soft-hitnormal", StringComparison.Ordinal)).Layers.Single();
            Assert.That(normalLayer.Amplitude, Is.EqualTo(1));
        }

        [Test]
        public void TestTwoSamplesInOneSlotAreMixed()
        {
            var soft = lane(HitSampleInfo.BANK_SOFT, HitsoundAddition.None);
            var kick = lane(HitSampleInfo.BANK_DRUM, HitsoundAddition.None);

            var result = build(new[] { soft, kick }, new[] { hit(soft, 1000), hit(kick, 1000) });

            // The first lane decides the circle's bank, both samples are mixed into that slot.
            var file = result.Files.Single();
            Assert.That(file.Filename, Is.EqualTo($"soft-hitnormal{FIRST_NEW_INDEX}.wav"));
            Assert.That(file.Layers.Select(l => l.Source.Name), Is.EquivalentTo(new[] { "soft-hitnormal", "drum-hitnormal" }));
            Assert.That(samplesOf(result, 0), Is.EqualTo(new[] { (HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT) }));
        }

        [Test]
        public void TestCustomIndexInTheSetIsReused()
        {
            var normal = lane(HitSampleInfo.BANK_SOFT, HitsoundAddition.None, index: 3);
            var clap = lane(HitSampleInfo.BANK_SOFT, HitsoundAddition.Clap, index: 3);
            var files = new[] { "soft-hitnormal3.wav", "Soft-HitClap3.ogg" };

            var result = build(new[] { normal, clap }, new[] { hit(normal, 1000), hit(clap, 1000) }, files);

            Assert.That(result.Files, Is.Empty);
            Assert.That(result.Indices, Is.EqualTo(new[] { 3 }));
            Assert.That(result.UsedSetFiles, Is.EquivalentTo(files));
            Assert.That(result.Circles.Single().Samples.All(s => s.Suffix == "3"));
        }

        [Test]
        public void TestDifferentIndicesInOneMomentGetANewIndexWithCopies()
        {
            var normal = lane(HitSampleInfo.BANK_SOFT, HitsoundAddition.None, index: 3);
            var clap = lane(HitSampleInfo.BANK_SOFT, HitsoundAddition.Clap, index: 4);

            var result = build(new[] { normal, clap }, new[] { hit(normal, 1000), hit(clap, 1000) },
                new[] { "soft-hitnormal3.wav", "soft-hitclap4.ogg" }, new HashSet<int> { 2, 3, 4 });

            int index = result.Indices.Single();
            Assert.That(index, Is.EqualTo(5), "2 to 4 are taken");
            Assert.That(result.Files.Select(f => (f.Filename, f.IsCopy)), Is.EquivalentTo(new[] { ("soft-hitclap5.ogg", true), ("soft-hitnormal5.wav", true) }));
        }

        [Test]
        public void TestMomentsThatAgreeShareAnIndex()
        {
            var normal = lane(HitSampleInfo.BANK_SOFT, HitsoundAddition.None, index: 3);
            var clap = lane(HitSampleInfo.BANK_SOFT, HitsoundAddition.Clap, index: 4);
            var whistle = lane(HitSampleInfo.BANK_SOFT, HitsoundAddition.Whistle, index: 4);
            var files = new[] { "soft-hitnormal3.wav", "soft-hitclap4.wav", "soft-hitwhistle4.wav" };

            // Neither moment fits index 3 or 4 as it is, but they agree on every slot they share, so they get one new index.
            var result = build(new[] { normal, clap, whistle }, new[] { hit(normal, 1000), hit(clap, 1000), hit(normal, 2000), hit(whistle, 2000) }, files, new HashSet<int> { 3, 4 });

            Assert.That(result.NewIndices, Is.EqualTo(1));
            Assert.That(result.Files.Select(f => f.Filename), Is.EquivalentTo(new[] { "soft-hitnormal2.wav", "soft-hitclap2.wav", "soft-hitwhistle2.wav" }));
        }

        [Test]
        public void TestHitsWithinLeniencyAreOneMoment()
        {
            var normal = lane(HitSampleInfo.BANK_SOFT, HitsoundAddition.None);
            var clap = lane(HitSampleInfo.BANK_SOFT, HitsoundAddition.Clap);

            var result = build(new[] { normal, clap }, new[] { hit(normal, 1000), hit(clap, 1000 + LENIENCY), hit(clap, 1000 + 2 * LENIENCY + 1) });

            Assert.That(result.Circles.Select(c => c.StartTime), Is.EqualTo(new[] { 1000.0, 1000 + 2 * LENIENCY + 1 }));
        }

        [Test]
        public void TestMutedLanesAreLeftOut()
        {
            var normal = lane(HitSampleInfo.BANK_SOFT, HitsoundAddition.None);
            var muted = lane(HitSampleInfo.BANK_SOFT, HitsoundAddition.Clap);
            muted.Muted = true;

            var result = build(new[] { normal, muted }, new[] { hit(normal, 1000), hit(muted, 1000), hit(muted, 2000) });

            Assert.That(result.Circles, Has.Count.EqualTo(1));
            Assert.That(samplesOf(result, 0), Is.EqualTo(new[] { (HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT) }));
        }

        [Test]
        public void TestCustomFileLaneIsCopiedToAStandardName()
        {
            var kick = lane(HitSampleInfo.BANK_DRUM, HitsoundAddition.None, file: "Kick.wav");

            var result = build(new[] { kick }, new[] { hit(kick, 1000) }, new[] { "kick.wav" });

            Assert.That(result.Files.Single().Filename, Is.EqualTo($"drum-hitnormal{FIRST_NEW_INDEX}.wav"));
            Assert.That(result.Files.Single().IsCopy);
            Assert.That(result.Circles.Single().Samples.Single().Bank, Is.EqualTo(HitSampleInfo.BANK_DRUM));
        }

        [Test]
        public void TestIndexOfFile()
        {
            Assert.That(IndexOfFile("soft-hitclap.wav"), Is.EqualTo(1));
            Assert.That(IndexOfFile("Drum-HitNormal12.ogg"), Is.EqualTo(12));
            Assert.That(IndexOfFile("kick.wav"), Is.Null);
            Assert.That(IndexOfFile("soft-sliderslide2.wav"), Is.Null);
        }

        [Test]
        public void TestWriterMixesWithoutClipping()
        {
            if (!Bass.Init(0) && Bass.LastError != Errors.Already)
                Assert.Ignore("BASS is not available.");

            short[] loud = Enumerable.Repeat((short)30000, 4410).ToArray();
            byte[] a = HitsoundSampleWriter.Wav(loud, 44100, 1);
            byte[] b = HitsoundSampleWriter.Wav(loud, 22050, 1);

            var file = new GeneratedFile("soft-hitnormal2.wav", new[]
            {
                new Layer(new SampleSource(SourceKind.File, "a.wav"), 1),
                new Layer(new SampleSource(SourceKind.File, "b.wav"), 1),
            });

            byte[] mixed = HitsoundSampleWriter.Render(file, s => s.Name == "a.wav" ? a : b);
            var decoded = HitsoundSampleWriter.Decode(mixed);

            Assert.That(decoded, Is.Not.Null);
            Assert.That(decoded!.Value.Rate, Is.EqualTo(44100), "the higher rate wins");
            Assert.That(decoded.Value.Samples.Length, Is.EqualTo(8820), "the 22050 Hz file (0.2 s) sets the length");
            Assert.That(decoded.Value.Samples.Max(), Is.LessThanOrEqualTo(1f));
            Assert.That(decoded.Value.Samples.Take(4000).Min(), Is.GreaterThan(0.9f), "two loud samples add up to the limiter's ceiling, not past it");
        }

        [Test]
        public void TestBlankIsTheStandard44ByteFile()
        {
            var file = new GeneratedFile("normal-hitnormal2.wav", new[] { new Layer(SampleSource.BLANK, 1) });
            Assert.That(HitsoundSampleWriter.Render(file, _ => null), Has.Length.EqualTo(44));
        }
    }
}
