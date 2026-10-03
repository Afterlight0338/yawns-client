// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Edit;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Editor
{
    /// <summary>
    /// YAWNS: scaling a group with sliders in it: keep shapes (lazer), scale shapes, scale shapes and adjust SV.
    /// </summary>
    public partial class TestSceneScaleSliderModes : TestSceneOsuEditor
    {
        private OsuSelectionScaleHandler scaleHandler => Editor.ChildrenOfType<OsuSelectionScaleHandler>().Single();

        private Slider slider => EditorBeatmap.HitObjects.OfType<Slider>().Single();

        private double originalDuration;

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("place a circle and a slider and select both", () =>
            {
                EditorBeatmap.Clear();
                EditorBeatmap.Add(new HitCircle { StartTime = 500, Position = new Vector2(200, 200) });
                EditorBeatmap.Add(new Slider
                {
                    StartTime = 1000,
                    Position = new Vector2(100, 100),
                    Path = new SliderPath(new[] { new PathControlPoint(Vector2.Zero), new PathControlPoint(new Vector2(160, 0)) }),
                });
                EditorBeatmap.SelectedHitObjects.AddRange(EditorBeatmap.HitObjects);
                originalDuration = slider.Duration;
            });
        }

        private void scale(float factor) => AddStep($"scale by {factor}", () =>
        {
            scaleHandler.Begin();
            scaleHandler.Update(new Vector2(factor), new Vector2(0, 0));
            scaleHandler.Commit();
        });

        [Test]
        public void TestKeepShapesMovesHeadsOnly()
        {
            scale(0.5f);
            AddAssert("head moved", () => slider.Position, () => Is.EqualTo(new Vector2(50, 50)));
            AddAssert("shape unchanged", () => slider.Path.Distance, () => Is.EqualTo(160).Within(0.01));
        }

        [Test]
        public void TestScaleShapes()
        {
            AddStep("scale shapes", () => scaleHandler.Sliders.Value = OsuSelectionScaleHandler.SliderScaling.ScaleShapes);
            scale(0.5f);
            AddAssert("shape halved", () => slider.Path.Distance, () => Is.EqualTo(80).Within(0.01));
            AddAssert("velocity kept, so duration halved", () => slider.Duration, () => Is.EqualTo(originalDuration / 2).Within(1));
        }

        [Test]
        public void TestScaleShapesAdjustSpeed()
        {
            AddStep("scale shapes and speed", () => scaleHandler.Sliders.Value = OsuSelectionScaleHandler.SliderScaling.ScaleShapesAdjustSpeed);
            scale(0.5f);
            AddAssert("shape halved", () => slider.Path.Distance, () => Is.EqualTo(80).Within(0.01));
            AddAssert("duration kept", () => slider.Duration, () => Is.EqualTo(originalDuration).Within(1));
        }
    }
}
