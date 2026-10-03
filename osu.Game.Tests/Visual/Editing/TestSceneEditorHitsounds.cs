// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Graphics.Containers;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.MappingTools.Hitsounds;
using osuTK;

namespace osu.Game.Tests.Visual.Editing
{
    /// <summary>
    /// YAWNS: the Hitsounds tab.
    /// </summary>
    public partial class TestSceneEditorHitsounds : EditorTestScene
    {
        protected override Ruleset CreateEditorRuleset() => new OsuRuleset();

        private static readonly HitsoundSound soft_clap = new HitsoundSound(HitSampleInfo.HIT_CLAP, HitSampleInfo.BANK_SOFT);

        private HitsoundsScreen screen => Editor.ChildrenOfType<HitsoundsScreen>().Single();

        private HitsoundLane clapLane => screen.Lanes.Single(l => l.Sound == soft_clap);

        [Test]
        public void TestEditHitsoundDifficulty()
        {
            AddStep("open hitsounds tab", () => Editor.Mode.Value = EditorScreenMode.Hitsounds);
            AddUntilStep("tab loaded", () => Editor.ChildrenOfType<HitsoundsScreen>().SingleOrDefault()?.IsLoaded == true);
            AddAssert("gameplay difficulty is read-only", () => screen.Editable, () => Is.False);
            AddAssert("its hitsounds are shown", () => screen.Triggers.Count, () => Is.GreaterThan(0));

            AddStep("empty the difficulty, single timing", () =>
            {
                EditorBeatmap.Clear();
                EditorBeatmap.ControlPointInfo.Clear();
                EditorBeatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            });
            AddUntilStep("now editable", () => screen.Editable);

            AddStep("add soft clap lane", () => screen.AddLane(soft_clap));
            AddStep("click at 1000", () => screen.Toggle(clapLane, 1000, 5));
            AddAssert("one object in the middle", () => EditorBeatmap.HitObjects.Cast<HitCircle>().Select(h => h.Position), () => Is.EqualTo(new[] { HitsoundProject.POSITION }));
            AddAssert("it claps", () => EditorBeatmap.HitObjects.Single().Samples.Any(s => s.Name == HitSampleInfo.HIT_CLAP && s.Bank == HitSampleInfo.BANK_SOFT));

            AddStep("click it again", () => screen.Toggle(clapLane, 1002, 5));
            AddAssert("removed", () => EditorBeatmap.HitObjects, () => Is.Empty);

            AddStep("add 1000 and 1500", () =>
            {
                screen.AddHit(clapLane, 1000);
                screen.AddHit(clapLane, 1500);
                screen.Commit();
            });
            AddAssert("two objects", () => EditorBeatmap.HitObjects.Count, () => Is.EqualTo(2));

            AddStep("select all and copy", () =>
            {
                screen.Select(screen.Triggers);
                Editor.Copy();
            });
            AddStep("seek to 3000", () => EditorClock.Seek(3000));
            AddStep("paste", () => Editor.Paste());
            AddAssert("pasted at the playhead", () => EditorBeatmap.HitObjects.Select(h => h.StartTime).Order(), () => Is.EqualTo(new double[] { 1000, 1500, 3000, 3500 }));

            AddStep("undo", () => Editor.Undo());
            AddUntilStep("back to two", () => EditorBeatmap.HitObjects.Count, () => Is.EqualTo(2));
            AddUntilStep("lanes read back", () => screen.Triggers.Count(t => t.Sound == soft_clap), () => Is.EqualTo(2));
        }

        [Test]
        public void TestClickLaneAddsHitAtPlayhead()
        {
            AddStep("empty the difficulty, single timing", () =>
            {
                EditorBeatmap.Clear();
                EditorBeatmap.ControlPointInfo.Clear();
                EditorBeatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            });
            AddStep("open hitsounds tab", () => Editor.Mode.Value = EditorScreenMode.Hitsounds);
            AddUntilStep("editable", () => Editor.ChildrenOfType<HitsoundsScreen>().SingleOrDefault()?.Editable == true);
            AddStep("add soft clap lane", () => screen.AddLane(soft_clap));
            AddStep("seek to 2000", () => EditorClock.Seek(2000));
            AddWaitStep("let the view follow", 5);

            AddStep("click the lane under the playhead", () =>
            {
                var sequencer = screen.ChildrenOfType<HitsoundSequencer>().Single();
                InputManager.MoveMouseTo(sequencer.ToScreenSpace(new Vector2(sequencer.DrawWidth / 2, HitsoundsScreen.GHOST_ROW_HEIGHT + HitsoundsScreen.ROW_HEIGHT / 2)));
                InputManager.Click(osuTK.Input.MouseButton.Left);
            });
            AddAssert("hit at 2000", () => EditorBeatmap.HitObjects.Select(h => h.StartTime), () => Is.EqualTo(new double[] { 2000 }));
        }

        [Test]
        public void TestWheelScrollsLanesOnlyOverLaneList()
        {
            OsuScrollContainer laneScroll = null!;

            AddStep("open hitsounds tab", () => Editor.Mode.Value = EditorScreenMode.Hitsounds);
            AddUntilStep("tab loaded", () => Editor.ChildrenOfType<HitsoundsScreen>().SingleOrDefault()?.IsLoaded == true);
            AddStep("add 40 lanes", () =>
            {
                for (int i = 1; i <= 40; i++)
                    screen.AddLane(new HitsoundSound(HitSampleInfo.HIT_CLAP, HitSampleInfo.BANK_SOFT, i));
            });
            AddUntilStep("lanes overflow", () =>
            {
                laneScroll = screen.ChildrenOfType<OsuScrollContainer>().Single(s => s.ScrollDirection == Direction.Vertical);
                return laneScroll.ScrollableExtent > 0;
            });

            AddStep("wheel over the sequencer", () =>
            {
                var sequencer = screen.ChildrenOfType<HitsoundSequencer>().Single();
                InputManager.MoveMouseTo(sequencer.ScreenSpaceDrawQuad.Centre);
                InputManager.ScrollVerticalBy(-3);
            });
            AddAssert("lanes did not scroll", () => laneScroll.Current, () => Is.EqualTo(0));

            AddStep("wheel over the lane list", () =>
            {
                InputManager.MoveMouseTo(laneScroll.ToScreenSpace(new Vector2(HitsoundsScreen.HEADER_WIDTH / 2, laneScroll.DrawHeight / 2)));
                InputManager.ScrollVerticalBy(-3);
            });
            AddUntilStep("lanes scrolled", () => laneScroll.Current, () => Is.GreaterThan(0));
        }
    }
}
