// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.MappingTools.Hitsounds;
using osuTK;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Editing
{
    /// <summary>
    /// YAWNS: the Hitsounds tab, Hitsound Studio's sequencer and lane rack in the editor.
    /// </summary>
    public partial class TestSceneEditorHitsounds : EditorTestScene
    {
        protected override Ruleset CreateEditorRuleset() => new OsuRuleset();

        private HitsoundsScreen screen => Editor.ChildrenOfType<HitsoundsScreen>().Single();

        private HitsoundCanvas canvas => screen.Canvas;

        // The default lanes: 0 Soft Clap, 1 Soft Whistle, 2 Soft Finish, 3 Drum Kick.
        private const int clap = 0;
        private const int whistle = 1;
        private const int kick = 3;

        private Vector2 at(double time, int lane) => canvas.ScreenSpacePositionAt(time, lane);

        private HitsoundLane laneAt(int index) => screen.Lanes[index];

        private int hitsOn(int lane) => screen.Triggers.Count(t => t.LaneId == laneAt(lane).Id);

        private void openEmptyHitsoundDifficulty()
        {
            AddStep("empty the difficulty, single timing", () =>
            {
                EditorBeatmap.Clear();
                EditorBeatmap.ControlPointInfo.Clear();
                EditorBeatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            });
            AddStep("open hitsounds tab", () => Editor.Mode.Value = EditorScreenMode.Hitsounds);
            AddUntilStep("editable", () => Editor.ChildrenOfType<HitsoundsScreen>().SingleOrDefault()?.Editable == true);
            AddAssert("Hitsound Studio's default lanes", () => screen.Lanes.Select(l => l.Name), () => Is.EqualTo(new[] { "Soft Clap", "Soft Whistle", "Soft Finish", "Drum Kick" }));
            AddStep("seek to start, 1/4 snap", () =>
            {
                EditorClock.Seek(0);
                screen.SetSnap(4);
            });
            AddUntilStep("view at the start", () => canvas.ScrollLeftMs, () => Is.EqualTo(0));
        }

        private void click(double time, int lane, MouseButton button = MouseButton.Left)
        {
            InputManager.MoveMouseTo(at(time, lane));
            InputManager.Click(button);
        }

        private void placeHits(params (double Time, int Lane)[] hits)
        {
            AddStep($"place {hits.Length} hits", () =>
            {
                foreach (var (time, lane) in hits)
                    screen.AddTrigger(laneAt(lane), time);
            });
        }

        [Test]
        public void TestGameplayDifficultyIsReadOnly()
        {
            AddStep("open hitsounds tab", () => Editor.Mode.Value = EditorScreenMode.Hitsounds);
            AddUntilStep("tab loaded", () => Editor.ChildrenOfType<HitsoundsScreen>().SingleOrDefault()?.IsLoaded == true);
            AddAssert("read-only", () => screen.Editable, () => Is.False);
            AddAssert("banner shown", () => screen.ReadOnlyBannerShown);
            AddAssert("its hitsounds are shown as lanes", () => screen.Triggers.Count, () => Is.GreaterThan(0));

            int before = 0;
            AddStep("click an empty spot", () =>
            {
                before = screen.Triggers.Count;
                click(EditorClock.CurrentTime + 37, 0);
            });
            AddAssert("nothing placed", () => screen.Triggers.Count, () => Is.EqualTo(before));
        }

        [Test]
        public void TestClickPlacesSnappedOnRelease()
        {
            openEmptyHitsoundDifficulty();

            AddStep("press near 1000", () =>
            {
                InputManager.MoveMouseTo(at(1010, clap));
                InputManager.PressButton(MouseButton.Left);
            });
            AddAssert("nothing yet", () => screen.Triggers, () => Is.Empty);
            AddStep("release", () => InputManager.ReleaseButton(MouseButton.Left));
            AddAssert("clap at 1000 (snapped)", () => screen.Triggers.Select(t => (t.LaneId, t.Time)), () => Is.EqualTo(new[] { (laneAt(clap).Id, 1000.0) }));
            AddAssert("not written yet", () => EditorBeatmap.HitObjects, () => Is.Empty);
            AddAssert("export pending", () => screen.HasUnexportedChanges.Value);
        }

        [Test]
        public void TestDragOnEmptySpaceSelectsWithoutPlacing()
        {
            openEmptyHitsoundDifficulty();
            placeHits((1000, clap), (1500, whistle), (3000, clap));

            AddStep("drag from before 1000 on clap to 2000 on whistle", () =>
            {
                InputManager.MoveMouseTo(at(900, clap) - new Vector2(0, 10));
                InputManager.PressButton(MouseButton.Left);
                InputManager.MoveMouseTo(at(1300, whistle));
                InputManager.MoveMouseTo(at(2000, whistle) + new Vector2(0, 10));
            });
            AddStep("release", () => InputManager.ReleaseButton(MouseButton.Left));

            AddAssert("selected the two inside", () => screen.SelectedIds.Count, () => Is.EqualTo(2));
            AddAssert("nothing placed", () => screen.Triggers.Count, () => Is.EqualTo(3));
        }

        [Test]
        public void TestClickNoteSelectsAndShiftToggles()
        {
            openEmptyHitsoundDifficulty();
            placeHits((1000, clap), (1500, clap));

            AddStep("click the first", () => click(1000, clap));
            AddAssert("only it selected", () => screen.SelectedIds.Single(), () => Is.EqualTo(screen.Triggers[0].Id));
            AddAssert("nothing removed or added", () => screen.Triggers.Count, () => Is.EqualTo(2));

            AddStep("shift+click the second", () =>
            {
                InputManager.PressKey(Key.LShift);
                click(1500, clap);
                InputManager.ReleaseKey(Key.LShift);
            });
            AddAssert("both selected", () => screen.SelectedIds.Count, () => Is.EqualTo(2));

            AddStep("shift+click the first again", () =>
            {
                InputManager.PressKey(Key.LShift);
                click(1000, clap);
                InputManager.ReleaseKey(Key.LShift);
            });
            AddAssert("only the second", () => screen.SelectedIds.Single(), () => Is.EqualTo(screen.Triggers[1].Id));
        }

        [Test]
        public void TestDragMovesSelectionInTimeAndAcrossLanes()
        {
            openEmptyHitsoundDifficulty();
            placeHits((1000, clap), (1500, clap));
            AddStep("select both", () => screen.SetSelection(screen.Triggers.Select(t => t.Id)));

            AddStep("drag the first to 1250 on whistle", () =>
            {
                InputManager.MoveMouseTo(at(1000, clap));
                InputManager.PressButton(MouseButton.Left);
                InputManager.MoveMouseTo(at(1100, clap));
                InputManager.MoveMouseTo(at(1255, whistle));
            });
            AddAssert("not moved before release", () => screen.Triggers.Select(t => t.Time), () => Is.EqualTo(new[] { 1000.0, 1500.0 }));
            AddStep("release", () => InputManager.ReleaseButton(MouseButton.Left));

            AddAssert("both moved by 250 to whistle", () => screen.Triggers.Select(t => (t.LaneId, t.Time)),
                () => Is.EqualTo(new[] { (laneAt(whistle).Id, 1250.0), (laneAt(whistle).Id, 1750.0) }));
        }

        [Test]
        public void TestCtrlDragPaints()
        {
            openEmptyHitsoundDifficulty();

            AddStep("ctrl+drag over kick from 1000 to 2000", () =>
            {
                InputManager.PressKey(Key.LControl);
                InputManager.MoveMouseTo(at(1000, kick));
                InputManager.PressButton(MouseButton.Left);

                for (double t = 1000; t <= 2000; t += 25)
                    InputManager.MoveMouseTo(at(t, kick));
            });
            AddStep("release", () =>
            {
                InputManager.ReleaseButton(MouseButton.Left);
                InputManager.ReleaseKey(Key.LControl);
            });

            AddAssert("one hit per 1/4", () => screen.Triggers.Select(t => t.Time), () => Is.EqualTo(new[] { 1000.0, 1125, 1250, 1375, 1500, 1625, 1750, 1875, 2000 }));
        }

        [Test]
        public void TestRightClickDeletesAndRightDragErases()
        {
            openEmptyHitsoundDifficulty();
            placeHits((1000, clap), (1250, clap), (1500, clap), (2000, clap));

            AddStep("right click 1000", () => click(1000, clap, MouseButton.Right));
            AddAssert("removed", () => screen.Triggers.Select(t => t.Time), () => Is.EqualTo(new[] { 1250.0, 1500, 2000 }));

            // Pressed away from any note (a press on one deletes just that one, as in Hitsound Studio).
            AddStep("right drag from 1100 to 1600", () =>
            {
                InputManager.MoveMouseTo(at(1100, clap));
                InputManager.PressButton(MouseButton.Right);

                for (double t = 1100; t <= 1600; t += 10)
                    InputManager.MoveMouseTo(at(t, clap));
            });
            AddStep("release", () => InputManager.ReleaseButton(MouseButton.Right));
            AddAssert("erased what it passed", () => screen.Triggers.Select(t => t.Time), () => Is.EqualTo(new[] { 2000.0 }));
        }

        [Test]
        public void TestRightClickDuringSelectDragDoesNotCrash()
        {
            openEmptyHitsoundDifficulty();
            placeHits((1000, clap));

            // A user's crash in the first version: a right click in the middle of a drag broke the drag's end.
            AddStep("shift+drag", () =>
            {
                InputManager.PressKey(Key.LShift);
                InputManager.MoveMouseTo(at(500, whistle));
                InputManager.PressButton(MouseButton.Left);
                InputManager.MoveMouseTo(at(1500, whistle));
            });
            AddStep("right click", () => InputManager.Click(MouseButton.Right));
            AddStep("release left", () =>
            {
                InputManager.ReleaseButton(MouseButton.Left);
                InputManager.ReleaseKey(Key.LShift);
            });
            AddAssert("still running", () => canvas.IsAlive);
        }

        [Test]
        public void TestAdditionKeys()
        {
            openEmptyHitsoundDifficulty();
            placeHits((1000, kick), (1500, kick));
            AddStep("select both", () => screen.SetSelection(screen.Triggers.Select(t => t.Id)));

            AddStep("press W", () => InputManager.Key(Key.W));
            AddAssert("whistle on both", () => screen.Triggers.Where(t => t.LaneId == laneAt(whistle).Id).Select(t => t.Time), () => Is.EqualTo(new[] { 1000.0, 1500 }));

            AddStep("press W again", () => InputManager.Key(Key.W));
            AddAssert("whistle off again", () => hitsOn(whistle), () => Is.Zero);

            AddStep("press R", () => InputManager.Key(Key.R));
            AddAssert("claps", () => hitsOn(clap), () => Is.EqualTo(2));

            AddStep("remove the finish lane, press E", () =>
            {
                screen.RemoveLane(laneAt(2));
                screen.SetSelection(screen.Triggers.Where(t => t.LaneId == screen.Lanes.Single(l => l.Name == "Drum Kick").Id).Select(t => t.Id));
                InputManager.Key(Key.E);
            });
            AddAssert("a finish lane was made for it", () => screen.Lanes.Last().Addition, () => Is.EqualTo(HitsoundAddition.Finish));
            AddAssert("finishes", () => screen.Triggers.Count(t => t.LaneId == screen.Lanes.Last().Id), () => Is.EqualTo(2));
        }

        [Test]
        public void TestSnapAndGhostKeys()
        {
            openEmptyHitsoundDifficulty();

            AddStep("press 4", () => InputManager.Key(Key.Number4));
            AddAssert("1/3", () => screen.SnapDivisor, () => Is.EqualTo(3));
            AddStep("press 6", () => InputManager.Key(Key.Number6));
            AddAssert("1/8", () => screen.SnapDivisor, () => Is.EqualTo(8));
            AddStep("press 1", () => InputManager.Key(Key.Number1));
            AddAssert("1/1", () => screen.SnapDivisor, () => Is.EqualTo(1));

            AddStep("press G", () => InputManager.Key(Key.G));
            AddAssert("ghost notes hidden", () => canvas.ShowGhostNotes.Value, () => Is.False);
            AddStep("press G", () => InputManager.Key(Key.G));
            AddAssert("ghost notes shown", () => canvas.ShowGhostNotes.Value);

            AddStep("press +", () => InputManager.Key(Key.Plus));
            AddAssert("zoomed in", () => canvas.Zoom.Value, () => Is.EqualTo(275).Within(0.1));
        }

        [Test]
        public void TestCopyPasteDeleteKeys()
        {
            openEmptyHitsoundDifficulty();
            placeHits((1000, clap), (1250, kick));

            AddStep("ctrl+A", () =>
            {
                InputManager.PressKey(Key.LControl);
                InputManager.Key(Key.A);
                InputManager.ReleaseKey(Key.LControl);
            });
            AddAssert("all selected", () => screen.SelectedIds.Count, () => Is.EqualTo(2));

            AddStep("C", () => InputManager.Key(Key.C));
            AddAssert("did not start playback", () => EditorClock.IsRunning, () => Is.False);
            AddStep("seek to 3000", () => EditorClock.Seek(3000));
            AddStep("V", () => InputManager.Key(Key.V));
            AddAssert("pasted at the playhead", () => screen.Triggers.Select(t => (t.LaneId, t.Time)), () => Is.EqualTo(new[]
            {
                (laneAt(clap).Id, 1000.0), (laneAt(kick).Id, 1250.0), (laneAt(clap).Id, 3000.0), (laneAt(kick).Id, 3250.0),
            }));
            AddAssert("pasted ones selected", () => screen.SelectedIds.Count, () => Is.EqualTo(2));

            AddStep("X", () => InputManager.Key(Key.X));
            AddAssert("deleted them", () => screen.Triggers.Select(t => t.Time), () => Is.EqualTo(new[] { 1000.0, 1250 }));

            AddStep("select one, Escape", () =>
            {
                screen.SetSelection(new[] { screen.Triggers[0].Id });
                InputManager.Key(Key.Escape);
            });
            AddAssert("deselected", () => screen.SelectedIds, () => Is.Empty);
            AddAssert("still in the editor", () => Editor.IsCurrentScreen());
        }

        [Test]
        public void TestUndoRedo()
        {
            openEmptyHitsoundDifficulty();
            AddStep("click 1000", () => click(1000, clap));
            AddStep("click 1500", () => click(1500, clap));
            AddAssert("two", () => screen.Triggers.Count, () => Is.EqualTo(2));

            AddStep("ctrl+Z", () =>
            {
                InputManager.PressKey(Key.LControl);
                InputManager.Key(Key.Z);
                InputManager.ReleaseKey(Key.LControl);
            });
            AddAssert("one", () => screen.Triggers.Select(t => t.Time), () => Is.EqualTo(new[] { 1000.0 }));

            AddStep("ctrl+Y", () =>
            {
                InputManager.PressKey(Key.LControl);
                InputManager.Key(Key.Y);
                InputManager.ReleaseKey(Key.LControl);
            });
            AddAssert("two again", () => screen.Triggers.Count, () => Is.EqualTo(2));

            AddStep("delete the clap lane", () => screen.RemoveLane(laneAt(clap)));
            AddAssert("its hits are gone", () => screen.Triggers, () => Is.Empty);
            AddStep("undo", () => screen.Undo());
            AddAssert("lane and hits back", () => screen.Lanes.First().Name == "Soft Clap" && screen.Triggers.Count == 2);
        }

        [Test]
        public void TestTypingInLaneNameDoesNotTriggerKeys()
        {
            openEmptyHitsoundDifficulty();
            placeHits((1000, kick));
            AddStep("select it", () => screen.SetSelection(screen.Triggers.Select(t => t.Id)));

            AddStep("focus the first lane's name", () =>
            {
                var box = screen.Rack.ChildrenOfType<OsuTextBox>().First();
                InputManager.MoveMouseTo(box);
                InputManager.Click(MouseButton.Left);
            });
            AddStep("press W", () => InputManager.Key(Key.W));
            AddAssert("no whistle added", () => hitsOn(whistle), () => Is.Zero);
        }

        [Test]
        public void TestLaneEditChangesWhatIsExported()
        {
            openEmptyHitsoundDifficulty();
            placeHits((1000, kick), (1000, clap), (1500, kick));

            // Equal volumes: the skin plays everything at index 0, so no sample files are made (this test's beatmap is not in the database).
            AddStep("kick as loud as the clap", () => screen.EditLane(laneAt(kick), l => l.Volume = laneAt(clap).Volume));
            AddStep("export", () => screen.Export());
            AddAssert("one circle per moment", () => EditorBeatmap.HitObjects.Select(h => h.StartTime), () => Is.EqualTo(new[] { 1000.0, 1500 }));
            AddAssert("all in the middle", () => EditorBeatmap.HitObjects.Cast<HitCircle>().All(h => h.Position == HitsoundProject.POSITION));
            AddAssert("drum hitnormal and soft clap", () => EditorBeatmap.HitObjects[0].Samples.Select(s => (s.Name, s.Bank)),
                () => Is.EquivalentTo(new[] { (HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_DRUM), (HitSampleInfo.HIT_CLAP, HitSampleInfo.BANK_SOFT) }));
            AddAssert("no stacking", () => EditorBeatmap.StackLeniency, () => Is.Zero);
            AddAssert("nothing pending", () => screen.HasUnexportedChanges.Value, () => Is.False);

            AddStep("make the clap lane a drum finish", () => screen.EditLane(laneAt(clap), l =>
            {
                l.Bank = HitSampleInfo.BANK_DRUM;
                l.Addition = HitsoundAddition.Finish;
            }));
            AddAssert("export pending", () => screen.HasUnexportedChanges.Value);

            AddStep("save the map", () => Editor.Save());
            AddAssert("saving exported it", () => EditorBeatmap.HitObjects[0].Samples.Select(s => (s.Name, s.Bank)),
                () => Is.EquivalentTo(new[] { (HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_DRUM), (HitSampleInfo.HIT_FINISH, HitSampleInfo.BANK_DRUM) }));
        }

        [Test]
        public void TestProjectIsSavedAndReloaded()
        {
            openEmptyHitsoundDifficulty();
            placeHits((1000, kick), (1500, clap));
            AddStep("rename and mute a lane", () =>
            {
                screen.EditLane(laneAt(kick), l => l.Name = "Big kick");
                screen.ToggleMute(laneAt(whistle));
            });

            AddStep("reload the project", () => screen.ReloadProject());
            AddAssert("lanes kept", () => screen.Lanes.Select(l => l.Name), () => Is.EqualTo(new[] { "Soft Clap", "Soft Whistle", "Soft Finish", "Big kick" }));
            AddAssert("mute kept", () => screen.Lanes[whistle].Muted);
            AddAssert("hits kept", () => screen.Triggers.Select(t => t.Time), () => Is.EqualTo(new[] { 1000.0, 1500 }));
        }

        [Test]
        public void TestChangeOutsideTheTabIsNoticed()
        {
            openEmptyHitsoundDifficulty();
            placeHits((1000, kick));
            AddStep("export", () => screen.Export());
            AddAssert("no banner", () => screen.ChangedBannerShown, () => Is.False);

            AddStep("add a circle in the middle elsewhere", () =>
            {
                var circle = new HitCircle { StartTime = 2000, Position = HitsoundProject.POSITION };
                circle.Samples.Add(new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_NORMAL));
                EditorBeatmap.Add(circle);
            });
            AddUntilStep("banner", () => screen.ChangedBannerShown);

            AddStep("reimport", () => screen.ReimportFromDifficulty());
            AddAssert("lanes from the difficulty", () => screen.Triggers.Select(t => t.Time), () => Is.EqualTo(new[] { 1000.0, 2000 }));
            AddAssert("banner gone", () => screen.ChangedBannerShown, () => Is.False);
        }

        [Test]
        public void TestWheelScrollsLanes()
        {
            openEmptyHitsoundDifficulty();
            AddStep("add 30 lanes", () =>
            {
                for (int i = 0; i < 30; i++)
                    screen.AddLane();
            });

            AddStep("wheel over the canvas", () =>
            {
                InputManager.MoveMouseTo(at(1000, 1));
                InputManager.ScrollVerticalBy(-3);
            });
            AddUntilStep("lanes scrolled", () => canvas.ScrollTop.Value, () => Is.GreaterThan(0));

            float scrolled = 0;
            AddStep("wheel back over the rack", () =>
            {
                scrolled = canvas.ScrollTop.Value;
                InputManager.MoveMouseTo(screen.Rack.ScreenSpaceDrawQuad.Centre);
                InputManager.ScrollVerticalBy(1);
            });
            AddAssert("rack scrolls the same lanes", () => canvas.ScrollTop.Value, () => Is.LessThan(scrolled));
        }
    }
}
