// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Rulesets.Osu.Edit;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components;
using osu.Game.Screens.Edit.MappingTools;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Editor
{
    /// <summary>
    /// YAWNS: saving a pattern on the compose screen and inserting it from the Tools tab.
    /// </summary>
    public partial class TestScenePatternGallery : TestSceneOsuEditor
    {
        private MappingToolboxGroup tools => Editor.ChildrenOfType<MappingToolboxGroup>().Single();

        [Test]
        public void TestSaveThenInsert()
        {
            AddStep("place a triangle", () =>
            {
                EditorBeatmap.Clear();
                EditorBeatmap.ControlPointInfo.Clear();
                EditorBeatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
                EditorBeatmap.AddRange(new[]
                {
                    new HitCircle { StartTime = 1000, Position = new Vector2(100, 100), NewCombo = true },
                    new HitCircle { StartTime = 1250, Position = new Vector2(200, 100) },
                    new HitCircle { StartTime = 1500, Position = new Vector2(150, 180) },
                });
                EditorBeatmap.SelectedHitObjects.AddRange(EditorBeatmap.HitObjects);
            });

            AddStep("open save pattern", () => tools.ShowSavePattern());
            AddUntilStep("popover shown", () => this.ChildrenOfType<SavePatternPopover>().SingleOrDefault(), () => Is.Not.Null);
            AddStep("name it and save", () =>
            {
                var popover = this.ChildrenOfType<SavePatternPopover>().Single();
                popover.ChildrenOfType<FormTextBox>().Single().Current.Value = "triangle";
                popover.ChildrenOfType<RoundedButton>().Single().TriggerClick();
            });

            AddStep("seek to 5000", () => EditorClock.Seek(5000));
            AddStep("open tools tab", () => Editor.Mode.Value = EditorScreenMode.Tools);
            AddUntilStep("tools loaded", () => Editor.ChildrenOfType<ToolsScreen>().SingleOrDefault()?.IsLoaded == true);
            AddStep("open pattern gallery", () => Editor.ChildrenOfType<EditorToolButton>().Single(b => b.Text.ToString() == "Pattern Gallery").TriggerClick());
            AddUntilStep("pattern listed", () => insertButton() != null);
            AddAssert("card has a preview", () => Editor.ChildrenOfType<PatternPreview>().Count(), () => Is.EqualTo(1));

            AddStep("insert it", () => insertButton()!.TriggerClick());
            AddUntilStep("back on compose", () => Editor.Mode.Value, () => Is.EqualTo(EditorScreenMode.Compose));
            AddAssert("six objects", () => EditorBeatmap.HitObjects.Count, () => Is.EqualTo(6));
            AddAssert("inserted copy is selected at 5000",
                () => EditorBeatmap.SelectedHitObjects.Select(h => h.StartTime).Order(), () => Is.EqualTo(new double[] { 5000, 5250, 5500 }));
            AddAssert("same shape", () => EditorBeatmap.SelectedHitObjects.OfType<HitCircle>().OrderBy(h => h.StartTime).Select(h => h.Position),
                () => Is.EqualTo(new[] { new Vector2(100, 100), new Vector2(200, 100), new Vector2(150, 180) }));

            AddStep("undo", () => Editor.Undo());
            AddAssert("back to three", () => EditorBeatmap.HitObjects.Count, () => Is.EqualTo(3));
        }

        private RoundedButton? insertButton() => Editor.ChildrenOfType<PatternGalleryPanel>().SingleOrDefault()?
                                                       .ChildrenOfType<RoundedButton>().FirstOrDefault(b => b.Text.ToString() == "Insert");
    }
}
