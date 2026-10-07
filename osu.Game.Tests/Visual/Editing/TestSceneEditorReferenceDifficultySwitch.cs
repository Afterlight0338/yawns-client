// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Skinning;
using osu.Game.Storyboards;
using osu.Game.Tests.Beatmaps.IO;

namespace osu.Game.Tests.Visual.Editing
{
    /// <summary>
    /// YAWNS: the reference beatmap stays loaded when switching difficulties.
    /// </summary>
    public partial class TestSceneEditorReferenceDifficultySwitch : EditorTestScene
    {
        protected override Ruleset CreateEditorRuleset() => new OsuRuleset();

        protected override bool IsolateSavingFromDatabase => false;

        [Resolved]
        private OsuGameBase game { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        private BeatmapSetInfo importedBeatmapSet = null!;

        // Normal, Hard, Insane.
        private BeatmapInfo[] osuDifficulties => importedBeatmapSet.Beatmaps.Where(b => b.Ruleset.OnlineID == 0).OrderBy(b => b.StarRating).ToArray();

        public override void SetUpSteps()
        {
            AddStep("import test beatmap", () => importedBeatmapSet = BeatmapImportHelper.LoadOszIntoOsu(game, virtualTrack: true).GetResultSafely());
            base.SetUpSteps();
        }

        protected override WorkingBeatmap CreateWorkingBeatmap(IBeatmap beatmap, Storyboard? storyboard = null)
            => beatmaps.GetWorkingBeatmap(osuDifficulties[0]);

        [Test]
        public void TestReferenceFollowsDifficultySwitches()
        {
            AddAssert("three osu! difficulties", () => osuDifficulties, () => Has.Length.EqualTo(3));

            AddStep("reference Hard while editing Normal", () => Editor.ReferenceBeatmap.Load(osuDifficulties[1]));
            AddStep("set offset, opacity and skin", () =>
            {
                Editor.ReferenceBeatmap.Offset.Value = 25;
                Editor.ReferenceBeatmap.Opacity.Value = 0.6f;
                Editor.ReferenceBeatmap.Skin.Value = TrianglesSkin.CreateInfo().ToLiveUnmanaged();
            });

            switchTo(2);
            AddUntilStep("reference is still Hard", () => referenceBeatmap(), () => Is.EqualTo(osuDifficulties[1]));
            AddAssert("offset kept", () => Editor.ReferenceBeatmap.Offset.Value, () => Is.EqualTo(25));
            AddAssert("opacity kept", () => Editor.ReferenceBeatmap.Opacity.Value, () => Is.EqualTo(0.6f));
            AddAssert("skin kept", () => Editor.ReferenceBeatmap.Skin.Value.ID, () => Is.EqualTo(SkinInfo.TRIANGLES_SKIN));

            // Now editing Insane with Hard as reference. Switching to Hard swaps the reference to Insane.
            switchTo(1);
            AddUntilStep("reference swapped to Insane", () => referenceBeatmap(), () => Is.EqualTo(osuDifficulties[2]));
        }

        private BeatmapInfo? referenceBeatmap() => Editor.ReferenceBeatmap.Beatmap.Value?.BeatmapInfo;

        private void switchTo(int index)
        {
            AddStep($"switch to {index}", () => Editor.SwitchToDifficulty(osuDifficulties[index]));
            AddUntilStep("editing it", () => Beatmap.Value.BeatmapInfo.Equals(osuDifficulties[index]) && Stack.CurrentScreen == Editor && Editor?.IsLoaded == true);
        }
    }
}
