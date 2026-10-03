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
    /// YAWNS: copying the Hitsounds tab's hits onto the other difficulties of the set, which are saved.
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

        [Test]
        public void TestCopyToOtherDifficulty()
        {
            BeatmapInfo target = null!;
            double time = 0;

            AddStep("pick another difficulty and one of its circles", () =>
            {
                target = importedBeatmapSet.Beatmaps.Last(b => !b.Equals(EditorBeatmap.BeatmapInfo));
                var playable = beatmaps.GetWorkingBeatmap(target).GetPlayableBeatmap(target.Ruleset);
                time = playable.HitObjects.First(h => h.Samples.All(s => s.Name != HitSampleInfo.HIT_CLAP) && h is not osu.Game.Rulesets.Objects.Types.IHasDuration).StartTime;
            });

            AddStep("make this a hitsound difficulty", () => EditorBeatmap.Clear());
            AddStep("open hitsounds tab", () => Editor.Mode.Value = EditorScreenMode.Hitsounds);
            AddUntilStep("editable", () => Editor.ChildrenOfType<HitsoundsScreen>().SingleOrDefault()?.Editable == true);

            AddStep("clap at that time", () =>
            {
                var lane = new HitsoundSound(HitSampleInfo.HIT_CLAP, HitSampleInfo.BANK_DRUM);
                screen.AddLane(lane);
                screen.AddHit(screen.Lanes.Single(l => l.Sound == lane), time);
                screen.Commit();
            });

            AddAssert("copied onto one difficulty", () => screen.CopyTo(new[] { target }, new HitsoundCopier()), () => Is.EqualTo(1));

            AddAssert("saved difficulty claps there", () =>
            {
                var reloaded = beatmaps.GetWorkingBeatmap(beatmaps.QueryBeatmap(b => b.ID == target.ID)!).GetPlayableBeatmap(target.Ruleset);
                return reloaded.HitObjects.Single(h => h.StartTime == time).Samples.Any(s => s.Name == HitSampleInfo.HIT_CLAP && s.Bank == HitSampleInfo.BANK_DRUM);
            });
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
                return screen.GhostObjects.Select(g => g.Start).SequenceEqual(playable.HitObjects.Select(h => h.StartTime));
            });

            AddStep("ghost none", () => screen.Ghost.Value = GhostChoice.NONE);
            AddAssert("no ghosts", () => screen.GhostObjects, () => Is.Empty);
        }
    }
}
