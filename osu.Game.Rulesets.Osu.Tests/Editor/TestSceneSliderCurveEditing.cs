// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Edit.Blueprints.Sliders;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit.MappingTools;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Osu.Tests.Editor
{
    /// <summary>
    /// YAWNS: Alt+drag direct curve editing, the Ctrl insertion preview and Shift-insert at the nearer end.
    /// </summary>
    public partial class TestSceneSliderCurveEditing : TestSceneOsuEditor
    {
        private Slider slider = null!;

        private SliderSelectionBlueprint blueprint => this.ChildrenOfType<SliderSelectionBlueprint>().Single();

        private Vector2[] relative => slider.Path.ControlPoints.Select(p => p.Position).ToArray();

        private Vector2 curvePoint(float t) => slider.Position + BezierTools.Evaluate(relative, t);

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("place a bezier slider and select it", () =>
            {
                EditorBeatmap.Clear();
                EditorBeatmap.Add(slider = new Slider
                {
                    StartTime = 1000,
                    Position = new Vector2(100, 150),
                    Path = new SliderPath(new[]
                    {
                        new PathControlPoint(Vector2.Zero, PathType.BEZIER),
                        new PathControlPoint(new Vector2(60, 90)),
                        new PathControlPoint(new Vector2(140, -40)),
                        new PathControlPoint(new Vector2(200, 50)),
                    }),
                });
                EditorBeatmap.SelectedHitObjects.Add(slider);
            });
            AddUntilStep("blueprint shown", () => this.ChildrenOfType<SliderSelectionBlueprint>().Count(), () => Is.EqualTo(1));
        }

        [Test]
        public void TestAltDragMovesTheCurvePointUnderTheCursor()
        {
            Vector2 before = Vector2.Zero;

            AddStep("alt+left press on the curve", () =>
            {
                before = curvePoint(0.5f);
                InputManager.MoveMouseTo(blueprint.ToScreenSpace(before));
                InputManager.PressKey(Key.AltLeft);
                InputManager.PressButton(MouseButton.Left);
            });
            AddStep("drag down 30", () => InputManager.MoveMouseTo(blueprint.ToScreenSpace(before + new Vector2(0, 30))));
            AddStep("release", () =>
            {
                InputManager.ReleaseButton(MouseButton.Left);
                InputManager.ReleaseKey(Key.AltLeft);
            });

            AddAssert("the head did not move", () => slider.Position, () => Is.EqualTo(new Vector2(100, 150)));
            AddAssert("the middle of the curve followed the cursor", () => Vector2.Distance(curvePoint(0.5f), before + new Vector2(0, 30)), () => Is.LessThan(4f));
            AddAssert("same number of control points", () => slider.Path.ControlPoints.Count, () => Is.EqualTo(4));

            AddStep("undo", () => Editor.Undo());
            AddAssert("back as placed", () => EditorBeatmap.HitObjects.OfType<Slider>().Single().Path.ControlPoints[1].Position, () => Is.EqualTo(new Vector2(60, 90)));
        }

        [Test]
        public void TestShiftInsertGoesToTheNearerEnd()
        {
            AddStep("ctrl+shift click near the end", () =>
            {
                InputManager.MoveMouseTo(blueprint.ToScreenSpace(curvePoint(0.9f)));
                InputManager.PressKey(Key.ControlLeft);
                InputManager.PressKey(Key.ShiftLeft);
                InputManager.Click(MouseButton.Left);
                InputManager.ReleaseKey(Key.ShiftLeft);
                InputManager.ReleaseKey(Key.ControlLeft);
            });

            AddAssert("one more control point", () => EditorBeatmap.HitObjects.OfType<Slider>().Single().Path.ControlPoints.Count, () => Is.EqualTo(5));
            AddAssert("it is the last one", () => EditorBeatmap.HitObjects.OfType<Slider>().Single().Path.ControlPoints[^1].Position.X, () => Is.EqualTo(curvePoint(0.9f).X - 100).Within(6));
        }

        [Test]
        public void TestTrueSliderEndIsMarkedWhileSelected()
        {
            Vector2 expected = Vector2.Zero;

            AddStep("work out the legacy last tick", () =>
            {
                double tick = System.Math.Max(slider.Duration / 2, slider.Duration - 36);
                expected = slider.StackedPositionAt(tick / slider.Duration);
            });
            AddUntilStep("ring shown at it", () =>
                blueprint.ChildrenOfType<osu.Framework.Graphics.Containers.CircularContainer>().Any(c => c.Alpha > 0 && c.BorderThickness == 2 && Vector2.Distance(c.Position, expected) < 0.5f && System.Math.Abs(c.Width - slider.Radius * 2) < 0.5f));

            AddStep("deselect", () => EditorBeatmap.SelectedHitObjects.Clear());
            AddUntilStep("ring gone", () => !blueprint.ChildrenOfType<osu.Framework.Graphics.Containers.CircularContainer>().Any(c => c.Alpha > 0 && c.BorderThickness == 2));
        }

        [Test]
        public void TestCtrlShowsThePreviewCurve()
        {
            AddStep("hold ctrl over the slider", () =>
            {
                InputManager.MoveMouseTo(blueprint.ToScreenSpace(curvePoint(0.5f) + new Vector2(0, 20)));
                InputManager.PressKey(Key.ControlLeft);
            });
            AddUntilStep("preview drawn", () => blueprint.ChildrenOfType<Box>().Count(b => b.Colour.TopLeft.Linear.G > 0.9f && b.Colour.TopLeft.Linear.B < 0.1f && b.Alpha > 0), () => Is.GreaterThan(10));
            AddStep("release ctrl", () => InputManager.ReleaseKey(Key.ControlLeft));
            AddUntilStep("preview gone", () => blueprint.ChildrenOfType<Box>().Count(b => b.Colour.TopLeft.Linear.G > 0.9f && b.Colour.TopLeft.Linear.B < 0.1f && b.Alpha > 0), () => Is.EqualTo(0));
        }
    }
}
