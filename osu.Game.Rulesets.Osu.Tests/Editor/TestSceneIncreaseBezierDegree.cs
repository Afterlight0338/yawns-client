// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Edit.Blueprints.Sliders.Components;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit.MappingTools;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Osu.Tests.Editor
{
    /// <summary>
    /// YAWNS: pressing I on a selected Bezier control point adds one control point and keeps the curve.
    /// </summary>
    public partial class TestSceneIncreaseBezierDegree : TestSceneOsuEditor
    {
        private Slider slider = null!;
        private Vector2[] before = null!;

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("place a bezier slider", () =>
            {
                EditorBeatmap.Clear();
                EditorBeatmap.Add(slider = new Slider
                {
                    StartTime = 1000,
                    Position = new Vector2(100, 100),
                    Path = new SliderPath(new[]
                    {
                        new PathControlPoint(Vector2.Zero, PathType.BEZIER),
                        new PathControlPoint(new Vector2(60, 90)),
                        new PathControlPoint(new Vector2(140, -40)),
                        new PathControlPoint(new Vector2(200, 50)),
                    }),
                });
                EditorBeatmap.SelectedHitObjects.Add(slider);
                before = slider.Path.ControlPoints.Select(p => p.Position).ToArray();
            });
        }

        private PathControlPointVisualiser<Slider> visualiser => this.ChildrenOfType<PathControlPointVisualiser<Slider>>().Single();

        [Test]
        public void TestIncreaseDegreeKeepsTheCurveAndUndoes()
        {
            AddUntilStep("visualiser shown", () => this.ChildrenOfType<PathControlPointVisualiser<Slider>>().Count(), () => Is.EqualTo(1));
            AddStep("select the second point", () => visualiser.SetSelectionTo(slider.Path.ControlPoints[1]));
            AddStep("press I", () => InputManager.Key(Key.I));

            AddAssert("one more control point", () => slider.Path.ControlPoints, () => Has.Count.EqualTo(5));
            AddAssert("same curve", () =>
            {
                var after = slider.Path.ControlPoints.Select(p => p.Position).ToArray();
                return Enumerable.Range(0, 21).All(i => Vector2.Distance(BezierTools.Evaluate(after, i / 20f), BezierTools.Evaluate(before, i / 20f)) < 0.01f);
            });
            AddAssert("length unchanged", () => slider.Path.Distance, () => Is.EqualTo(slider.Path.Distance).Within(0.01));

            AddStep("undo", () => Editor.Undo());
            AddAssert("back to four points", () => EditorBeatmap.HitObjects.OfType<Slider>().Single().Path.ControlPoints.Select(p => p.Position).ToArray(), () => Is.EqualTo(before)); // undo restores new instances
        }
    }
}
