// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Edit;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit.MappingTools;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Editor
{
    /// <summary>
    /// YAWNS: the axis guide and "Align to axis".
    /// </summary>
    public partial class TestSceneAxisGuide : TestSceneOsuEditor
    {
        private MappingToolboxGroup tools => Editor.ChildrenOfType<MappingToolboxGroup>().Single();

        private Slider crooked = null!;

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("place sliders on a 12 degree tilt and one crooked slider", () =>
            {
                EditorBeatmap.Clear();

                double time = 1000;
                foreach (double angle in new[] { 12, -12, 102, 78, 192 })
                    EditorBeatmap.Add(straight(time += 1000, angle));

                EditorBeatmap.Add(crooked = straight(time + 1000, 25));
            });
        }

        [Test]
        public void TestAlignToAxis()
        {
            AddStep("show axis guide", () => tools.AxisGuide.Enabled.Value = true);
            AddUntilStep("tilt detected", () => tools.AxisGuide.Tilt.Value, () => Is.EqualTo(12).Within(0.5));

            AddStep("select crooked slider", () => EditorBeatmap.SelectedHitObjects.Add(crooked));
            AddUntilStep("says it is off by 13", () => label().Contains("off by 13°", StringComparison.Ordinal));
            AddUntilStep("align enabled", () => tools.CanAlignToAxis.Value);

            AddStep("align to axis", () => tools.AlignToAxis());
            AddAssert("slider now on 12 degrees", () => angleOf(crooked), () => Is.EqualTo(12).Within(0.5));
            AddUntilStep("says it is on axis", () => label().Contains("on axis", StringComparison.Ordinal));

            AddStep("undo", () => Editor.Undo());
            AddAssert("back to 25 degrees", () => angleOf((Slider)EditorBeatmap.HitObjects.Last()), () => Is.EqualTo(25).Within(0.5));
        }

        [Test]
        public void TestFollowsObjectWhileMoving()
        {
            AddStep("show axis guide", () => tools.AxisGuide.Enabled.Value = true);
            AddStep("select crooked slider", () => EditorBeatmap.SelectedHitObjects.Add(crooked));
            AddUntilStep("label next to it", () => labelText().Position.X, () => Is.EqualTo(crooked.StackedPosition.X + 12).Within(0.5));

            // Dragging moves the object every frame but only commits when the drag ends.
            AddStep("move it without committing", () => crooked.Position += new Vector2(80, 0));
            AddUntilStep("label followed", () => labelText().Position.X, () => Is.EqualTo(crooked.StackedPosition.X + 12).Within(0.5));
        }

        [Test]
        public void TestSingleCircleHasNoAxis()
        {
            AddStep("add and select a circle", () =>
            {
                var circle = new HitCircle { StartTime = 500, Position = new Vector2(100, 100) };
                EditorBeatmap.Add(circle);
                EditorBeatmap.SelectedHitObjects.Add(circle);
            });
            AddUntilStep("align disabled", () => !tools.CanAlignToAxis.Value);
        }

        private OsuSpriteText labelText() => tools.AxisGuide.ChildrenOfType<OsuSpriteText>().Single();

        private string label() => labelText().Text.ToString();

        private static double angleOf(Slider slider) => AxisFinder.AngleOf(slider.Position, slider.EndPosition);

        private static Slider straight(double time, double angle)
        {
            double rad = MathHelper.DegreesToRadians(angle);

            return new Slider
            {
                StartTime = time,
                Position = new Vector2(256, 192),
                Path = new SliderPath(new[]
                {
                    new PathControlPoint(Vector2.Zero, PathType.LINEAR),
                    new PathControlPoint(100 * new Vector2((float)Math.Cos(rad), (float)Math.Sin(rad))),
                }),
            };
        }
    }
}
