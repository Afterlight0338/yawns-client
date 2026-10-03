// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Rulesets.Osu.Edit;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit.Components;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Editor
{
    /// <summary>
    /// YAWNS: "perfect it" on the compose screen: five rough circles become a pentagon, with live preview, Keep (one undo step) and Cancel.
    /// </summary>
    public partial class TestScenePerfectIt : TestSceneOsuEditor
    {
        private static readonly Vector2 centre = new Vector2(256, 192);

        private MappingToolboxGroup tools => Editor.ChildrenOfType<MappingToolboxGroup>().Single();

        private PerfectItPopover? popover => this.ChildrenOfType<PerfectItPopover>().SingleOrDefault();

        private HitCircle[] circles => EditorBeatmap.HitObjects.OfType<HitCircle>().OrderBy(h => h.StartTime).ToArray();

        private Vector2[] roughPositions = Array.Empty<Vector2>();

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("place five rough circles", () =>
            {
                roughPositions = Enumerable.Range(0, 5).Select(i =>
                {
                    double angle = double.DegreesToRadians(i * 72 - 90);
                    return centre + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * (100 + (i % 2 == 0 ? 9 : -7));
                }).ToArray();

                EditorBeatmap.Clear();
                EditorBeatmap.AddRange(roughPositions.Select((p, i) => new HitCircle { StartTime = 1000 + i * 500, Position = p }));
                EditorBeatmap.SelectedHitObjects.AddRange(EditorBeatmap.HitObjects);
            });
        }

        private bool isPentagon() => circles.All(c => Math.Abs(Vector2.Distance(c.Position, circles.Aggregate(Vector2.Zero, (s, h) => s + h.Position) / 5) - Vector2.Distance(circles[0].Position, circles.Aggregate(Vector2.Zero, (s, h) => s + h.Position) / 5)) < 0.1f);

        [Test]
        public void TestKeepThenUndo()
        {
            AddStep("open perfect it", () => tools.ShowPerfectIt());
            AddUntilStep("popover shown", () => popover, () => Is.Not.Null);
            AddUntilStep("circles are on one circle", isPentagon);

            AddStep("close it", () => Editor.ChildrenOfType<EditorToolButton>().Single(b => b.Text.ToString() == "Perfect it").TriggerClick());
            AddUntilStep("popover gone", () => popover, () => Is.Null);
            AddAssert("still a pentagon", isPentagon);

            AddStep("undo once", () => Editor.Undo());
            AddAssert("back to the rough circles", () => circles.Select(c => c.Position).ToArray(), () => Is.EqualTo(roughPositions));
        }

        [Test]
        public void TestCancelPutsThemBack()
        {
            AddStep("open perfect it", () => tools.ShowPerfectIt());
            AddUntilStep("circles are on one circle", isPentagon);

            AddStep("cancel", () => this.ChildrenOfType<PerfectItPopover>().Single().ChildrenOfType<osu.Game.Graphics.UserInterfaceV2.RoundedButton>().Single(b => b.Text.ToString() == "Cancel").TriggerClick());
            AddUntilStep("popover gone", () => popover, () => Is.Null);
            AddAssert("rough circles restored", () => circles.Select(c => c.Position).ToArray(), () => Is.EqualTo(roughPositions));
        }
    }
}
