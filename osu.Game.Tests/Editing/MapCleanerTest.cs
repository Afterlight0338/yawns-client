// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Models;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.MappingTools;
using osuTK;
using static osu.Game.Rulesets.Objects.Legacy.ConvertHitObjectParser;

namespace osu.Game.Tests.Editing
{
    /// <summary>
    /// YAWNS: the Map Cleaner port.
    /// </summary>
    [TestFixture]
    public class MapCleanerTest
    {
        private static EditorBeatmap map(params HitObject[] objects)
        {
            var b = new OsuBeatmap { BeatmapInfo = { Ruleset = new OsuRuleset().RulesetInfo } };
            b.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 480 });
            b.HitObjects.AddRange(objects.Cast<OsuHitObject>());

            foreach (var h in b.HitObjects)
                h.ApplyDefaults(b.ControlPointInfo, b.Difficulty);

            return new EditorBeatmap(b);
        }

        private static Slider slider(double time, float length) => new Slider
        {
            StartTime = time,
            Position = new Vector2(100),
            Path = new SliderPath(new[] { new PathControlPoint(Vector2.Zero, PathType.LINEAR), new PathControlPoint(new Vector2(length, 0)) }),
        };

        [Test]
        public void TestResnapStartsAndSliderEnds()
        {
            var circle = new HitCircle { StartTime = 483 };
            var onBeat = new HitCircle { StartTime = 960 };
            var s = slider(1443, 100);
            var beatmap = map(circle, onBeat, s);
            double oldEnd = s.EndTime;

            int moved = MapCleaner.Resnap(beatmap, beatmap.HitObjects, MapCleaner.DEFAULT_DIVISORS);

            Assert.That(moved, Is.EqualTo(2));
            Assert.That(circle.StartTime, Is.EqualTo(480));
            Assert.That(onBeat.StartTime, Is.EqualTo(960));
            Assert.That(s.StartTime, Is.EqualTo(1440));
            Assert.That(s.EndTime, Is.EqualTo(MapCleaner.Snap(beatmap.ControlPointInfo, oldEnd, MapCleaner.DEFAULT_DIVISORS)).Within(0.5));
        }

        [Test]
        public void TestResnapUsesTwelfths()
        {
            var circle = new HitCircle { StartTime = 162 }; // 1/3 of a 480 ms beat is 160
            var beatmap = map(circle);

            MapCleaner.Resnap(beatmap, beatmap.HitObjects, MapCleaner.DEFAULT_DIVISORS);

            Assert.That(circle.StartTime, Is.EqualTo(160));
        }

        [Test]
        public void TestResnapBookmarks()
        {
            var beatmap = map(new HitCircle { StartTime = 0 });
            beatmap.Bookmarks.AddRange(new[] { 483, 478, 960 });

            Assert.That(MapCleaner.ResnapBookmarks(beatmap, MapCleaner.DEFAULT_DIVISORS), Is.EqualTo(2));
            Assert.That(beatmap.Bookmarks, Is.EqualTo(new[] { 480, 960 }));
        }

        [Test]
        public void TestUnmuteTakesEarlierVolume()
        {
            var loud = new HitCircle { StartTime = 0, Samples = { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, volume: 70) } };
            var muted = new HitCircle { StartTime = 480, Samples = { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, volume: 5) } };
            var beatmap = map(loud, muted);

            Assert.That(MapCleaner.Unmute(beatmap, beatmap.HitObjects), Is.EqualTo(1));
            Assert.That(muted.Samples.Single().Volume, Is.EqualTo(70));
        }

        [Test]
        public void TestUnmuteClickableOnlyKeepsMutedTails()
        {
            var s = slider(0, 100);
            s.NodeSamples = new List<IList<HitSampleInfo>>
            {
                new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, volume: 5) },
                new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, volume: 5) },
            };
            var beatmap = map(s);

            MapCleaner.Unmute(beatmap, beatmap.HitObjects, clickableOnly: true);

            Assert.That(s.NodeSamples[0].Single().Volume, Is.EqualTo(100));
            Assert.That(s.NodeSamples[1].Single().Volume, Is.EqualTo(5));
        }

        [Test]
        public void TestMuteUnclickable()
        {
            var s = slider(0, 100);
            s.NodeSamples = new List<IList<HitSampleInfo>>
            {
                new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, volume: 80) },
                new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, volume: 80) },
            };
            var beatmap = map(s);

            Assert.That(MapCleaner.MuteUnclickable(beatmap), Is.EqualTo(1));
            Assert.That(s.NodeSamples[0].Single().Volume, Is.EqualTo(80));
            Assert.That(s.NodeSamples[1].Single().Volume, Is.EqualTo(MapCleaner.MUTED_VOLUME));
        }

        [Test]
        public void TestUnusedHitsoundFiles()
        {
            var clap = new HitCircle { StartTime = 0, Samples = { new LegacyHitSampleInfo(HitSampleInfo.HIT_CLAP, HitSampleInfo.BANK_SOFT, 100, customSampleBank: 1) } };
            var kick = new HitCircle { StartTime = 480, Samples = { new FileHitSampleInfo("kick.wav", 100) } };
            var s = slider(960, 100);
            s.Samples = new List<HitSampleInfo> { new LegacyHitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_NORMAL, 100, customSampleBank: 3) };
            var beatmap = map(clap, kick, s);

            var set = new BeatmapSetInfo();
            foreach (string name in new[] { "soft-hitclap.wav", "soft-hitclap2.wav", "kick.wav", "normal-sliderslide3.ogg", "normal-hitnormal3.wav", "drum-hitfinish.wav", "bg.jpg" })
                set.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = name }, name));

            Assert.That(MapCleaner.UnusedHitsoundFiles(set, new[] { beatmap }), Is.EqualTo(new[] { "drum-hitfinish.wav", "soft-hitclap2.wav" }));
        }
    }
}
