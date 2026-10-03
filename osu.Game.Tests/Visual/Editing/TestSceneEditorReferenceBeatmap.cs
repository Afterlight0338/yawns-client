// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.IO.Serialization;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components;
using osu.Game.Screens.Edit.Compose.Components.Timeline;
using osu.Game.Screens.Edit.GameplayTest;
using osu.Game.Screens.Edit.Reference;
using osu.Game.Skinning;
using osuTK;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Editing
{
    /// <summary>
    /// YAWNS: the reference beatmap drawn under the edited one on the compose screen.
    /// </summary>
    public partial class TestSceneEditorReferenceBeatmap : EditorTestScene
    {
        protected override Ruleset CreateEditorRuleset() => new OsuRuleset();

        private EditorReferenceBeatmap reference => Editor.ReferenceBeatmap;

        private ReferenceBeatmapLayer<OsuHitObject> layer => Editor.ChildrenOfType<ReferenceBeatmapLayer<OsuHitObject>>().Single();

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            // The test beatmap manager hands back the edited test beatmap for any lookup, so the reference is a copy of it.
            AddStep("load reference", () => reference.Load(EditorBeatmap.BeatmapInfo));
            AddUntilStep("reference displayed", () => layer.DrawableRuleset?.IsLoaded == true);
        }

        [Test]
        public void TestFollowsEditorClockWithOffsetAndOpacity()
        {
            AddStep("seek to 3000", () => EditorClock.Seek(3000));
            AddUntilStep("reference in sync", () => Precision.AlmostEquals(layer.DrawableRuleset!.FrameStableClock.CurrentTime, EditorClock.CurrentTime, 1));
            AddUntilStep("reference objects visible", () => layer.DrawableRuleset!.Playfield.AllHitObjects.Any());

            AddStep("move reference 500ms later", () => reference.Offset.Value = 500);
            AddUntilStep("reference 500ms behind", () => Precision.AlmostEquals(layer.DrawableRuleset!.FrameStableClock.CurrentTime, EditorClock.CurrentTime - 500, 1));

            AddStep("set opacity", () => reference.Opacity.Value = 0.25f);
            AddAssert("layer faded", () => layer.Alpha, () => Is.EqualTo(0.25f));
        }

        [Test]
        public void TestReferencePlaysNoHitsounds()
        {
            AddStep("start playback", () => EditorClock.Start());
            AddUntilStep("edited beatmap played hitsounds", () => editedSamples().Any(s => s.Played));
            AddUntilStep("reference objects were hit", () => layer.ChildrenOfType<DrawableHitObject>().Any(h => h.Judged && h.IsHit));
            AddAssert("reference played no hitsounds", () => !layer.ChildrenOfType<PoolableSkinnableSample>().Any(s => s.Played));
            AddStep("stop playback", () => EditorClock.Stop());
        }

        [Test]
        public void TestSurvivesComposeReloadAndTestPlay()
        {
            AddStep("reload compose screen", () => Editor.ReloadComposeScreen());
            AddUntilStep("reference displayed again", () => Editor.ChildrenOfType<ReferenceBeatmapLayer<OsuHitObject>>().SingleOrDefault()?.DrawableRuleset?.IsLoaded == true);

            AddStep("test gameplay", () => Editor.TestGameplay());
            AddUntilStep("wait for test play", () => Stack.CurrentScreen is EditorPlayer player && player.IsLoaded);
            AddStep("exit test play", () => Stack.CurrentScreen.Exit());
            AddUntilStep("back in editor", () => Stack.CurrentScreen is Editor editor && editor.ReadyForUse);
            AddUntilStep("reference still displayed", () => layer.DrawableRuleset?.IsLoaded == true);
        }

        [Test]
        public void TestQuickReplaceAndClear()
        {
            AddStep("load twice in a row", () =>
            {
                reference.Load(EditorBeatmap.BeatmapInfo);
                reference.Load(EditorBeatmap.BeatmapInfo);
            });
            AddUntilStep("latest reference displayed", () => layer.DrawableRuleset?.IsLoaded == true);
            AddAssert("only one reference drawn", () => layer.ChildrenOfType<DrawableRuleset>().Count(), () => Is.EqualTo(1));

            AddStep("clear", () => reference.Clear());
            AddAssert("nothing drawn", () => layer.DrawableRuleset == null && !layer.ChildrenOfType<DrawableRuleset>().Any());
        }

        [Test]
        public void TestTimelineLaneCopyAndInsert()
        {
            const double offset = 500;

            double start = 0;
            double end = 0;
            int countBefore = 0;

            AddAssert("timeline grew for the lanes", () => timeline.Height, () => Is.EqualTo(80 + ReferenceRhythmLanes.HEIGHT));

            AddStep("move reference 500ms later", () => reference.Offset.Value = offset);
            AddStep("pick the second to fourth objects", () =>
            {
                var objects = reference.Beatmap.Value!.HitObjects;
                start = objects[1].StartTime + offset;
                end = objects[3].StartTime + offset;
            });
            AddStep("seek to them", () => EditorClock.Seek(start));

            AddStep("drag across them on the lane", () =>
            {
                InputManager.MoveMouseTo(lane.ToScreenSpace(new Vector2(timeline.PositionAtTime(start), ReferenceRhythmLanes.ROW_HEIGHT * 1.5f)));
                InputManager.PressButton(MouseButton.Left);
                InputManager.MoveMouseTo(lane.ToScreenSpace(new Vector2(timeline.PositionAtTime((start + end) / 2), ReferenceRhythmLanes.ROW_HEIGHT * 1.5f)));
            });
            AddStep("finish drag", () =>
            {
                InputManager.MoveMouseTo(lane.ToScreenSpace(new Vector2(timeline.PositionAtTime(end), ReferenceRhythmLanes.ROW_HEIGHT * 1.5f)));
                InputManager.ReleaseButton(MouseButton.Left);
            });
            AddAssert("three reference objects selected", () => lane.SelectedObjects.Count(), () => Is.EqualTo(3));

            AddStep("copy from the context menu", () => ((OsuMenuItem)lane.ContextMenuItems.First()).Action.Value!.Invoke());
            AddAssert("clipboard holds them", () => Editor.Clipboard.Content.Value.Deserialize<ClipboardContent>().HitObjects.Count, () => Is.EqualTo(3));

            AddStep("count objects", () => countBefore = EditorBeatmap.HitObjects.Count);
            AddStep("paste", () => Editor.Paste());
            AddAssert("three objects pasted", () => EditorBeatmap.HitObjects.Count, () => Is.EqualTo(countBefore + 3));
            AddStep("undo", () => Editor.Undo());

            AddStep("insert at the same time", () => lane.InsertSelection());
            AddAssert("inserted where the reference shows them", () => EditorBeatmap.SelectedHitObjects.Select(h => h.StartTime),
                () => Is.EquivalentTo(reference.Beatmap.Value!.HitObjects.Skip(1).Take(3).Select(h => h.StartTime + offset)));
            AddStep("undo", () => Editor.Undo());
            AddAssert("back to the original objects", () => EditorBeatmap.HitObjects.Count, () => Is.EqualTo(countBefore));

            AddStep("clear reference", () => reference.Clear());
            AddAssert("timeline shrank back", () => timeline.Height, () => Is.EqualTo(80));
        }

        [Test]
        public void TestCopyToolsFromThePanel()
        {
            double firstTimingPoint = 0;

            AddStep("clear reference", () => reference.Clear());
            AddAssert("copy tools disabled", () => !toolButton("Hitsounds").Enabled.Value && !toolButton("Timing").Enabled.Value);

            AddStep("load reference", () => reference.Load(EditorBeatmap.BeatmapInfo));
            AddUntilStep("copy tools enabled", () => toolButton("Hitsounds").Enabled.Value && toolButton("Timing").Enabled.Value);

            AddStep("open hitsounds", () => toolButton("Hitsounds").TriggerClick());
            AddUntilStep("hitsounds popover shown", () => Editor.ChildrenOfType<ReferenceHitsoundsPopover>().SingleOrDefault()?.IsPresent == true);
            AddStep("copy hitsounds", () => Editor.ChildrenOfType<ReferenceHitsoundsPopover>().Single().ChildrenOfType<RoundedButton>().Single().TriggerClick());
            AddAssert("reports the copy", () => Editor.ChildrenOfType<ReferenceHitsoundsPopover>().Single().ChildrenOfType<OsuSpriteText>().Any(t => t.Text.ToString().StartsWith("Copied onto", StringComparison.Ordinal)));

            AddStep("store timing", () => firstTimingPoint = EditorBeatmap.ControlPointInfo.TimingPoints.First().Time);
            AddStep("move reference 500ms later", () => reference.Offset.Value = 500);
            AddStep("open timing", () => toolButton("Timing").TriggerClick());
            AddUntilStep("timing popover shown", () => Editor.ChildrenOfType<ReferenceTimingPopover>().SingleOrDefault()?.IsPresent == true);
            AddStep("copy timing", () => Editor.ChildrenOfType<ReferenceTimingPopover>().Single().ChildrenOfType<RoundedButton>().Single().TriggerClick());
            AddAssert("timing moved with the reference", () => EditorBeatmap.ControlPointInfo.TimingPoints.First().Time, () => Is.EqualTo(firstTimingPoint + 500));

            AddStep("undo", () => Editor.Undo());
            AddAssert("timing restored", () => EditorBeatmap.ControlPointInfo.TimingPoints.First().Time, () => Is.EqualTo(firstTimingPoint));
        }

        [Test]
        public void TestRhythmDifferences()
        {
            HitObject removed = null!;

            AddUntilStep("same rhythm, nothing marked", () => lane.OverlayObjectCount > 0 && !lane.YourDifferences.Any() && !lane.OverlayDifferences.Any());

            AddStep("delete one of your objects", () =>
            {
                removed = EditorBeatmap.HitObjects[2];
                EditorBeatmap.Remove(removed);
            });
            AddUntilStep("the overlay's hitsound there is marked", () => lane.OverlayDifferences.Contains(removed.StartTime));
            AddAssert("none of yours are marked", () => !lane.YourDifferences.Any());

            AddStep("undo", () => Editor.Undo());
            AddUntilStep("nothing marked again", () => !lane.OverlayDifferences.Any() && !lane.YourDifferences.Any());

            AddStep("move overlay 100ms later", () => reference.Offset.Value = 100);
            AddUntilStep("rhythm no longer lines up", () => lane.YourDifferences.Any() && lane.OverlayDifferences.Any());
        }

        [Test]
        public void TestOverlayPattern()
        {
            int fullCount = 0;
            int patternCount = 0;
            double patternFirstObject = 0;
            double expectedPlacement = 0;

            AddStep("count overlay objects", () => fullCount = reference.Beatmap.Value!.HitObjects.Count);
            AddStep("select the sixth to ninth objects", () =>
            {
                var objects = reference.Beatmap.Value!.HitObjects;
                lane.Select(objects[5].StartTime, objects[8].StartTime);
                patternCount = lane.SelectedObjects.Count();
                patternFirstObject = objects[5].StartTime;
            });

            AddStep("seek to 10s", () => EditorClock.Seek(10000));
            AddStep("overlay the pattern here", () =>
            {
                expectedPlacement = ((IBeatSnapProvider)Editor).SnapTime(EditorClock.CurrentTime);
                lane.OverlaySelectedPattern();
            });

            AddUntilStep("playfield draws only the pattern", () => layer.DrawableRuleset?.IsLoaded == true && layer.DrawableRuleset.Beatmap.HitObjects.Count == patternCount);
            AddUntilStep("lane shows only the pattern", () => lane.OverlayObjectCount, () => Is.EqualTo(patternCount));
            AddAssert("pattern starts at the current time", () => patternFirstObject + reference.DisplayOffset.Value, () => Is.EqualTo(expectedPlacement));
            AddUntilStep("playfield follows the placement", () => Precision.AlmostEquals(layer.DrawableRuleset!.FrameStableClock.CurrentTime, EditorClock.CurrentTime - reference.DisplayOffset.Value, 1));

            AddStep("overlay the whole map again", () => reference.ShowWholeMap());
            AddUntilStep("playfield draws the whole map", () => layer.DrawableRuleset?.IsLoaded == true && layer.DrawableRuleset.Beatmap.HitObjects.Count == fullCount);
            AddAssert("alignment offset is back", () => reference.DisplayOffset.Value, () => Is.EqualTo(reference.Offset.Value));
        }

        private EditorToolButton toolButton(string text) => Editor.ChildrenOfType<EditorToolButton>().Single(b => b.Text.ToString() == text);

        private ReferenceRhythmLanes lane => Editor.ChildrenOfType<ReferenceRhythmLanes>().Single();

        private Timeline timeline => Editor.ChildrenOfType<Timeline>().Single();

        private IEnumerable<PoolableSkinnableSample> editedSamples() =>
            Editor.ChildrenOfType<PoolableSkinnableSample>().Except(layer.ChildrenOfType<PoolableSkinnableSample>());
    }
}
