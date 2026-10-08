// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Audio;
using osu.Game.Beatmaps.ControlPoints;
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
    /// YAWNS: <see cref="HitsoundProject"/>, the Hitsounds tab's lanes read from beatmaps (Hitsound Studio's importer rules).
    /// </summary>
    [TestFixture]
    public class HitsoundProjectTest
    {
        private static HitSampleInfo sample(string name, string bank, int index = 0, int volume = 100) =>
            new HitSampleInfo(name, bank, index >= 2 ? index.ToString() : null, volume, useBeatmapSamples: index >= 1);

        private static OsuBeatmap beatmapWith(params HitObject[] objects)
        {
            var beatmap = new OsuBeatmap { BeatmapInfo = { Ruleset = new OsuRuleset().RulesetInfo } };
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });

            foreach (var h in objects)
            {
                h.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
                beatmap.HitObjects.Add((OsuHitObject)h);
            }

            return beatmap;
        }

        private static HitCircle circle(double time, params HitSampleInfo[] samples) => new HitCircle { StartTime = time, Position = HitsoundProject.POSITION, Samples = samples.ToList() };

        [Test]
        public void TestOneLanePerSoundInOrderOfFirstUse()
        {
            var beatmap = beatmapWith(
                circle(1000, sample(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT), sample(HitSampleInfo.HIT_CLAP, HitSampleInfo.BANK_DRUM)),
                circle(1500, sample(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT)),
                circle(2000, sample(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_NORMAL, 2)));

            var (lanes, triggers) = HitsoundProject.ImportLanes(beatmap, null);

            Assert.That(lanes.Select(l => l.Name), Is.EqualTo(new[] { "Soft HitNormal", "Drum Clap", "Normal HitNormal #2" }));
            Assert.That(lanes[1].Bank, Is.EqualTo(HitSampleInfo.BANK_DRUM), "an addition lane's bank is the addition's bank");
            Assert.That(lanes[1].Addition, Is.EqualTo(HitsoundAddition.Clap));
            Assert.That(triggers.Count(t => t.LaneId == lanes[0].Id), Is.EqualTo(2));
            Assert.That(lanes.Select(l => l.Colour).Distinct().Count(), Is.EqualTo(3));
        }

        [Test]
        public void TestMissingCustomIndexFallsBackLikeLazer()
        {
            var beatmap = beatmapWith(
                circle(1000, sample(HitSampleInfo.HIT_CLAP, HitSampleInfo.BANK_SOFT, 3)),
                circle(1500, sample(HitSampleInfo.HIT_WHISTLE, HitSampleInfo.BANK_SOFT, 4)),
                circle(2000, sample(HitSampleInfo.HIT_FINISH, HitSampleInfo.BANK_SOFT, 2)));

            // soft-hitclap3 is missing but soft-hitclap.wav exists (index 1); soft-hitwhistle4 is missing entirely (skin); soft-hitfinish2 exists.
            var (lanes, _) = HitsoundProject.ImportLanes(beatmap, new[] { "soft-hitclap.wav", "Soft-HitFinish2.ogg", "audio.mp3" });

            Assert.That(lanes.Select(l => l.Index), Is.EqualTo(new[] { 1, 0, 2 }));
        }

        [Test]
        public void TestHitVolumeOnlyWhereItDiffersFromTheLane()
        {
            var beatmap = beatmapWith(
                circle(1000, sample(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT, volume: 70)),
                circle(1500, sample(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT, volume: 40)),
                circle(2000, sample(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT, volume: 70)));

            var (lanes, triggers) = HitsoundProject.ImportLanes(beatmap, null);

            Assert.That(lanes.Single().Volume, Is.EqualTo(70), "the lane takes its first hit's volume");
            Assert.That(triggers.Select(t => t.Volume), Is.EqualTo(new int?[] { null, 40, null }));
            Assert.That(triggers.Select(t => t.VolumeOn(lanes[0])), Is.EqualTo(new[] { 70, 40, 70 }));
        }

        [Test]
        public void TestImportSliderNodesAndSpinnerEnd()
        {
            var slider = new Slider
            {
                StartTime = 1000,
                RepeatCount = 1,
                Path = new SliderPath(new[] { new PathControlPoint(Vector2.Zero, PathType.LINEAR), new PathControlPoint(new Vector2(140, 0)) }),
                NodeSamples =
                {
                    new List<HitSampleInfo> { sample(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT) },
                    new List<HitSampleInfo> { sample(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT), sample(HitSampleInfo.HIT_CLAP, HitSampleInfo.BANK_SOFT) },
                    new List<HitSampleInfo> { sample(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_DRUM) },
                },
            };
            var spinner = new Spinner { StartTime = 5000, Duration = 1000, Samples = { sample(HitSampleInfo.HIT_FINISH, HitSampleInfo.BANK_NORMAL) } };

            var beatmap = beatmapWith(slider, spinner);
            double span = slider.SpanDuration;

            var samples = HitsoundProject.Import(beatmap);

            Assert.That(samples.Select(s => (s.Time, s.Sound.Name)), Is.EquivalentTo(new[]
            {
                (1000.0, "Soft HitNormal"),
                (System.Math.Round(1000 + span), "Soft HitNormal"),
                (System.Math.Round(1000 + span), "Soft Clap"),
                (System.Math.Round(1000 + 2 * span), "Drum HitNormal"),
                (6000.0, "Normal Finish"),
            }));
        }

        [Test]
        public void TestProjectSurvivesJson()
        {
            var project = new HitsoundProjectData
            {
                Lanes = HitsoundProject.DefaultLanes(),
                Compact = true,
                ExportedHash = "ABC",
                GeneratedFiles = { "soft-hitclap2.wav" },
            };
            project.Lanes[0].File = "kick.wav";
            project.Lanes[1].Muted = true;
            project.Triggers.Add(new HitsoundTrigger { LaneId = project.Lanes[0].Id, Time = 1000, Volume = 40 });
            project.Triggers.Add(new HitsoundTrigger { LaneId = project.Lanes[3].Id, Time = 1250 });

            var read = HitsoundProjectData.Deserialise(project.Serialise())!;

            Assert.That(read.Lanes.Select(l => (l.Id, l.Name, l.Bank, l.Addition, l.Index, l.Volume, l.Colour, l.File, l.Muted)),
                Is.EqualTo(project.Lanes.Select(l => (l.Id, l.Name, l.Bank, l.Addition, l.Index, l.Volume, l.Colour, l.File, l.Muted))));
            Assert.That(read.Triggers.Select(t => (t.Id, t.LaneId, t.Time, t.Volume)), Is.EqualTo(project.Triggers.Select(t => (t.Id, t.LaneId, t.Time, t.Volume))));
            Assert.That(read.Compact, Is.True);
            Assert.That(read.ExportedHash, Is.EqualTo("ABC"));
            Assert.That(read.GeneratedFiles, Is.EqualTo(new[] { "soft-hitclap2.wav" }));
        }

        [Test]
        public void TestBrokenProjectJsonIsIgnored()
        {
            Assert.That(HitsoundProjectData.Deserialise("{ not json"), Is.Null);

            // Hits on a lane that is not there are dropped.
            var read = HitsoundProjectData.Deserialise("{\"Lanes\":[],\"Triggers\":[{\"LaneId\":\"gone\",\"Time\":5}]}")!;
            Assert.That(read.Triggers, Is.Empty);
        }

        [Test]
        public void TestDefaultLanesAreHitsoundStudios()
        {
            Assert.That(HitsoundProject.DefaultLanes().Select(l => (l.Name, l.Volume)),
                Is.EqualTo(new[] { ("Soft Clap", 90), ("Soft Whistle", 80), ("Soft Finish", 90), ("Drum Kick", 85) }));
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
