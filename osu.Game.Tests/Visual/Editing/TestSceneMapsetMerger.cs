// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit.MappingTools;
using osu.Game.Storyboards;
using osu.Game.Tests.Beatmaps.IO;
using osuTK;
using static osu.Game.Rulesets.Objects.Legacy.ConvertHitObjectParser;

namespace osu.Game.Tests.Visual.Editing
{
    /// <summary>
    /// YAWNS: bringing a difficulty of another set into the edited one (<see cref="MapsetMerger"/>).
    /// </summary>
    public partial class TestSceneMapsetMerger : EditorTestScene
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

        [Test]
        public void TestMergeGuestDifficulty()
        {
            BeatmapInfo guest = null!;
            BeatmapInfo merged = null!;

            AddStep("make a guest set with a custom clap", () =>
            {
                var working = beatmaps.CreateNew(new OsuRuleset().RulesetInfo, new APIUser { Username = "guest" });
                guest = working.BeatmapInfo;
                guest.DifficultyName = "Guest's Insane";

                beatmaps.AddFile(guest.BeatmapSet!, new MemoryStream(new byte[] { 1, 2, 3 }), "soft-hitclap.wav");

                var beatmap = (OsuBeatmap)working.GetPlayableBeatmap(guest.Ruleset);
                beatmap.HitObjects.Add(new HitCircle
                {
                    StartTime = 1000,
                    Position = new Vector2(100),
                    Samples =
                    {
                        new LegacyHitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT, 100, customSampleBank: 1),
                        new LegacyHitSampleInfo(HitSampleInfo.HIT_CLAP, HitSampleInfo.BANK_SOFT, 100, customSampleBank: 1),
                    },
                });
                beatmaps.Save(guest, beatmap);
            });

            AddStep("this set already has an index 1 clap", () => beatmaps.AddFile(EditorBeatmap.BeatmapInfo.BeatmapSet!, new MemoryStream(new byte[] { 4, 5, 6 }), "soft-hitclap.wav"));
            AddStep("merge it into the edited set", () => merged = MapsetMerger.Merge(beatmaps, EditorBeatmap.BeatmapInfo.BeatmapSet!, guest));

            AddAssert("set has the guest difficulty", () => EditorBeatmap.BeatmapInfo.BeatmapSet!.Beatmaps.Select(b => b.DifficultyName), () => Does.Contain("Guest's Insane"));
            AddAssert("it takes this set's metadata", () => merged.Metadata.Title, () => Is.EqualTo(EditorBeatmap.BeatmapInfo.Metadata.Title));
            AddAssert("clap moved to index 2 with its file", () =>
            {
                var reloaded = beatmaps.GetWorkingBeatmap(beatmaps.QueryBeatmap(b => b.ID == merged.ID)!).GetPlayableBeatmap(merged.Ruleset);
                var clap = (LegacyHitSampleInfo)reloaded.HitObjects.Single().Samples.Single(s => s.Name == HitSampleInfo.HIT_CLAP);
                var set = beatmaps.QueryBeatmapSet(s => s.ID == importedBeatmapSet.ID)!.Value;

                return clap.CustomSampleBank == 2 && set.Files.Any(f => f.Filename == "soft-hitclap2.wav") && set.Files.Any(f => f.Filename == "soft-hitclap.wav");
            });
        }
    }
}
