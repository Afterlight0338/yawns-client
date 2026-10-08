// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.MappingTools;
using osu.Game.Screens.Edit.MappingTools.Hitsounds;
using osu.Game.Storyboards;
using osu.Game.Tests.Beatmaps.IO;

namespace osu.Game.Tests.Visual.Editing
{
    /// <summary>
    /// YAWNS: the Hitsounds tab on a real beatmap set: export writes generated samples into the set, and the copier copies onto the other difficulties, which are saved.
    /// </summary>
    public partial class TestSceneEditorHitsoundsCopy : EditorTestScene
    {
        protected override Ruleset CreateEditorRuleset() => new OsuRuleset();

        protected override bool IsolateSavingFromDatabase => false;

        [Resolved]
        private OsuGameBase game { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        private BeatmapSetInfo importedBeatmapSet = null!;

        public override void SetUpSteps()
        {
            AddStep("import test beatmap", () => importedBeatmapSet = BeatmapImportHelper.LoadOszIntoOsu(game, virtualTrack: true).GetResultSafely());
            base.SetUpSteps();
        }

        protected override WorkingBeatmap CreateWorkingBeatmap(IBeatmap beatmap, Storyboard? storyboard = null)
            => beatmaps.GetWorkingBeatmap(importedBeatmapSet.Beatmaps.First());

        private HitsoundsScreen screen => Editor.ChildrenOfType<HitsoundsScreen>().Single();

        private BeatmapSetInfo currentSet => beatmaps.QueryBeatmapSet(s => s.ID == importedBeatmapSet.ID)!.Value;

        [Test]
        public void TestExportAndCopyToOtherDifficulty()
        {
            BeatmapInfo target = null!;
            double time = 0;
            string suffix = string.Empty;

            AddStep("pick another difficulty and one of its circles", () =>
            {
                target = importedBeatmapSet.Beatmaps.Last(b => !b.Equals(EditorBeatmap.BeatmapInfo));
                var playable = beatmaps.GetWorkingBeatmap(target).GetPlayableBeatmap(target.Ruleset);
                time = playable.HitObjects.First(h => h.Samples.All(s => s.Name != HitSampleInfo.HIT_CLAP) && h is not osu.Game.Rulesets.Objects.Types.IHasDuration).StartTime;
            });

            AddStep("make this a hitsound difficulty", () => EditorBeatmap.Clear());
            AddStep("open hitsounds tab", () => Editor.Mode.Value = EditorScreenMode.Hitsounds);
            AddUntilStep("editable", () => Editor.ChildrenOfType<HitsoundsScreen>().SingleOrDefault()?.Editable == true);

            AddStep("a drum clap alone at that time", () =>
            {
                var clap = screen.Lanes.First(l => l.Addition == HitsoundAddition.Clap);
                screen.EditLane(clap, l => l.Bank = HitSampleInfo.BANK_DRUM);
                screen.AddTrigger(clap, time);
            });

            AddAssert("copied onto one difficulty", () => screen.CopyTo(new[] { target }, new HitsoundCopier()), () => Is.EqualTo(1));

            AddAssert("exported first: one circle on a new custom index", () =>
            {
                var samples = EditorBeatmap.HitObjects.Single().Samples;
                suffix = samples[0].Suffix ?? string.Empty;
                return samples.All(s => s.UseBeatmapSamples && s.Suffix == suffix) && int.Parse(suffix) >= HitsoundExporter.FIRST_NEW_INDEX;
            });

            AddAssert("its samples are in the set: a silent hitnormal and the clap", () =>
            {
                var files = currentSet.Files.Select(f => f.Filename).ToList();
                return files.Contains($"normal-hitnormal{suffix}.wav") && files.Contains($"drum-hitclap{suffix}.wav");
            });

            AddAssert("saved difficulty claps there", () =>
            {
                var reloaded = beatmaps.GetWorkingBeatmap(beatmaps.QueryBeatmap(b => b.ID == target.ID)!).GetPlayableBeatmap(target.Ruleset);
                return reloaded.HitObjects.Single(h => h.StartTime == time).Samples.Any(s => s.Name == HitSampleInfo.HIT_CLAP && s.Bank == HitSampleInfo.BANK_DRUM);
            });

            AddStep("put an equally loud hitnormal with the clap and export", () =>
            {
                var kick = screen.Lanes.First(l => l.Name == "Drum Kick");
                screen.EditLane(kick, l => l.Volume = screen.Lanes.First(c => c.Addition == HitsoundAddition.Clap).Volume);
                screen.AddTrigger(kick, time);
                screen.Export();
            });
            AddAssert("skin samples only now: index 0", () => EditorBeatmap.HitObjects.Single().Samples.All(s => !s.UseBeatmapSamples));
            AddAssert("the generated files are gone again", () => currentSet.Files.All(f => !f.Filename.EndsWith($"{suffix}.wav", System.StringComparison.Ordinal)));
        }

        [Test]
        public void TestAudioDroppedOnALane()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "yawns-test-kick.wav");
            HitsoundLane lane = null!;
            int index = 0;

            AddStep("make this a hitsound difficulty", () => EditorBeatmap.Clear());
            AddStep("open hitsounds tab", () => Editor.Mode.Value = EditorScreenMode.Hitsounds);
            AddUntilStep("editable", () => Editor.ChildrenOfType<HitsoundsScreen>().SingleOrDefault()?.Editable == true);

            AddStep("load a kick into the Drum Kick lane", () =>
            {
                System.IO.File.WriteAllBytes(path, HitsoundSampleWriter.Wav(Enumerable.Range(0, 2205).Select(i => (short)(i % 40 < 20 ? 8000 : -8000)).ToArray(), 44100, 1));
                lane = screen.Lanes.First(l => l.Name == "Drum Kick");
                screen.LoadSampleIntoLane(lane, path);
                index = lane.Index;
            });

            AddAssert("a drum hitnormal on a new index", () => (lane.Bank, lane.Addition, lane.File), () => Is.EqualTo((HitSampleInfo.BANK_DRUM, HitsoundAddition.None, (string?)null)));
            AddAssert("index from 2", () => index, () => Is.GreaterThanOrEqualTo(HitsoundExporter.FIRST_NEW_INDEX));
            AddAssert("named after the file", () => lane.Name, () => Is.EqualTo("yawns-test-kick"));
            AddAssert("the file is in the set under a standard name", () => currentSet.Files.Any(f => f.Filename == $"drum-hitnormal{index}.wav"));

            AddStep("place it and export", () =>
            {
                screen.AddTrigger(lane, 1000);
                screen.Export();
            });
            AddAssert("plays that index as it is", () => EditorBeatmap.HitObjects.Single().Samples.Single().Suffix, () => Is.EqualTo(index.ToString()));
            AddAssert("no samples generated", () => currentSet.Files.Count(f => f.Filename.EndsWith($"{index}.wav", System.StringComparison.Ordinal)), () => Is.EqualTo(1));
        }

        [Test]
        public void TestGhostNotesFromAnotherDifficulty()
        {
            AddStep("open hitsounds tab", () => Editor.Mode.Value = EditorScreenMode.Hitsounds);
            AddUntilStep("tab loaded", () => Editor.ChildrenOfType<HitsoundsScreen>().SingleOrDefault()?.IsLoaded == true);
            AddAssert("gameplay difficulty ghosts the overlay map", () => screen.Ghost.Value, () => Is.EqualTo(GhostChoice.OVERLAY));
            AddAssert("no overlay, no ghosts", () => screen.GhostObjects, () => Is.Empty);

            AddStep("ghost another difficulty", () => screen.Ghost.Value = screen.GhostChoices.Last());
            AddAssert("its objects are ghosted", () =>
            {
                var difficulty = screen.Ghost.Value.Difficulty!;
                var playable = beatmaps.GetWorkingBeatmap(difficulty).GetPlayableBeatmap(difficulty.Ruleset);
                return screen.GhostObjects.Select(g => g.Time).SequenceEqual(playable.HitObjects.Select(h => h.StartTime));
            });

            AddStep("ghost none", () => screen.Ghost.Value = GhostChoice.NONE);
            AddAssert("no ghosts", () => screen.GhostObjects, () => Is.Empty);
        }

        [Test]
        public void TestPickingADifficultyLoadsItsHitsounds()
        {
            AddStep("make this a hitsound difficulty", () => EditorBeatmap.Clear());
            AddStep("open hitsounds tab", () => Editor.Mode.Value = EditorScreenMode.Hitsounds);
            AddUntilStep("editable", () => Editor.ChildrenOfType<HitsoundsScreen>().SingleOrDefault()?.Editable == true);
            AddAssert("hardest difficulty ghosted, lanes untouched", () => screen.Lanes.Count, () => Is.EqualTo(4));

            AddStep("ghost only mode, pick another", () =>
            {
                screen.Mode.Value = DiffMode.GhostOnly;
                screen.Ghost.Value = screen.GhostChoices.First(c => c.Difficulty != null && !c.Equals(screen.Ghost.Value));
            });
            AddAssert("lanes untouched", () => screen.Lanes.Count, () => Is.EqualTo(4));

            AddStep("hitsounds mode", () => screen.Mode.Value = DiffMode.Hitsounds);
            AddAssert("its hitsounds are the lanes now", () =>
            {
                var difficulty = screen.Ghost.Value.Difficulty!;
                var playable = beatmaps.GetWorkingBeatmap(difficulty).GetPlayableBeatmap(difficulty.Ruleset);
                return screen.Triggers.Select(t => t.Time).Distinct().Count() == HitsoundProject.Import(playable).Select(s => s.Time).Distinct().Count();
            });

            AddStep("undo", () => screen.Undo());
            AddAssert("default lanes back", () => screen.Lanes.Select(l => l.Name), () => Is.EqualTo(new[] { "Soft Clap", "Soft Whistle", "Soft Finish", "Drum Kick" }));
            AddAssert("and no hits", () => screen.Triggers, () => Is.Empty);
        }
    }
}
