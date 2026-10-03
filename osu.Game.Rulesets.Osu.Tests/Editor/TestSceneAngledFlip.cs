// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Rulesets.Osu.Edit;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit.Components;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Editor
{
    /// <summary>
    /// YAWNS: the angled flip popover: flips in place without adding objects, mirror copy, Keep with undo, Cancel.
    /// </summary>
    public partial class TestSceneAngledFlip : TestSceneOsuEditor
    {
        private MappingToolboxGroup tools => Editor.ChildrenOfType<MappingToolboxGroup>().Single();

        private AngledFlipPopover? popover => this.ChildrenOfType<AngledFlipPopover>().SingleOrDefault();

        private HitCircle[] circles => EditorBeatmap.HitObjects.OfType<HitCircle>().OrderBy(h => h.StartTime).ToArray();

        private static readonly Vector2[] placed = { new Vector2(156, 100), new Vector2(176, 200), new Vector2(196, 300) };

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("place three circles and select them", () =>
            {
                EditorBeatmap.Clear();
                EditorBeatmap.AddRange(placed.Select((p, i) => new HitCircle { StartTime = 1000 + i * 500, Position = p }));
                EditorBeatmap.SelectedHitObjects.AddRange(EditorBeatmap.HitObjects);
                tools.AngledFlip.Angle.Value = 90;
                tools.AngledFlip.Anticlockwise.Value = false;
                tools.AngledFlip.KeepOriginal.Value = false;
            });
        }

        private void open()
        {
            AddUntilStep("tools enabled", () => tools.CanSavePattern.Value);
            AddStep("open angled flip", () => tools.ShowAngledFlip());
            AddUntilStep("popover shown", () => popover, () => Is.Not.Null);
        }

        private void close() => AddStep("close", () => Editor.ChildrenOfType<EditorToolButton>().Single(b => b.Text.ToString() == "Angled flip").TriggerClick());

        [Test]
        public void TestFlipsInPlaceWithoutAddingObjects()
        {
            open();
            AddUntilStep("flipped left to right around the playfield centre", () => circles.Select(c => c.X).ToArray(), () => Is.EqualTo(new[] { 356f, 336f, 316f }));
            AddAssert("still three objects", () => EditorBeatmap.HitObjects.Count, () => Is.EqualTo(3));

            AddStep("change the angle to 0", () => tools.AngledFlip.Angle.Value = 0);
            AddUntilStep("flipped top to bottom instead (from the originals, not the flip)", () => circles.Select(c => c.Position).ToArray(),
                () => Is.EqualTo(new[] { new Vector2(156, 284), new Vector2(176, 184), new Vector2(196, 84) }));
            AddAssert("still three objects", () => EditorBeatmap.HitObjects.Count, () => Is.EqualTo(3));

            close();
            AddUntilStep("popover gone", () => popover, () => Is.Null);
            AddStep("undo", () => Editor.Undo());
            AddAssert("back as placed", () => EditorBeatmap.HitObjects.OfType<HitCircle>().OrderBy(h => h.StartTime).Select(c => c.Position).ToArray(), () => Is.EqualTo(placed));
        }

        [Test]
        public void TestDraggingTheAngleNeverLeavesObjectsBehind()
        {
            open();
            AddStep("drag the angle through 0 to 360 in half degree steps", () =>
            {
                for (float a = 0; a <= 360; a += 0.5f)
                    tools.AngledFlip.Angle.Value = a;
            });
            AddUntilStep("still exactly three objects", () => EditorBeatmap.HitObjects.Count, () => Is.EqualTo(3));

            AddStep("and with keep original on", () => tools.AngledFlip.KeepOriginal.Value = true);
            AddStep("drag again", () =>
            {
                for (float a = 0; a <= 360; a += 0.5f)
                    tools.AngledFlip.Angle.Value = a;
            });
            AddUntilStep("exactly six objects", () => EditorBeatmap.HitObjects.Count, () => Is.EqualTo(6));
        }

        [Test]
        public void TestCancelPutsThemBack()
        {
            open();
            AddUntilStep("flipped", () => circles[0].X, () => Is.EqualTo(356f));
            AddStep("cancel", () => popover!.ChildrenOfType<RoundedButton>().Single(b => b.Text.ToString() == "Cancel").TriggerClick());
            AddUntilStep("popover gone", () => popover, () => Is.Null);
            AddAssert("as placed", () => circles.Select(c => c.Position).ToArray(), () => Is.EqualTo(placed));
        }

        [Test]
        public void TestKeepOriginalAddsAMirroredCopy()
        {
            AddStep("keep the originals", () => tools.AngledFlip.KeepOriginal.Value = true);
            open();
            AddUntilStep("six objects", () => EditorBeatmap.HitObjects.Count, () => Is.EqualTo(6));
            AddAssert("originals untouched", () => circles.Select(c => c.Position).Where(p => p.X < 256).OrderBy(p => p.Y).ToArray(), () => Is.EqualTo(placed));
            AddAssert("copy mirrored", () => circles.Select(c => c.Position).Where(p => p.X > 256).OrderBy(p => p.Y).Select(p => p.X).ToArray(), () => Is.EqualTo(new[] { 356f, 336f, 316f }));

            AddStep("turn keep original off again", () => tools.AngledFlip.KeepOriginal.Value = false);
            AddUntilStep("back to three objects, flipped", () => EditorBeatmap.HitObjects.Count == 3 && circles[0].X == 356f);
        }
    }
}
