// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Osu.Tests.Editor
{
    /// <summary>
    /// YAWNS: [ and ] step the selected sliders' velocity, \ restores the previous slider's.
    /// </summary>
    public partial class TestSceneSliderVelocityHotkeys : TestSceneOsuEditor
    {
        private Slider[] sliders => EditorBeatmap.HitObjects.OfType<Slider>().OrderBy(s => s.StartTime).ToArray();

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("place three sliders, select the last two", () =>
            {
                EditorBeatmap.Clear();

                for (int i = 0; i < 3; i++)
                {
                    EditorBeatmap.Add(new Slider
                    {
                        StartTime = 1000 + i * 1000,
                        Position = new Vector2(100, 100 + i * 60),
                        SliderVelocityMultiplier = i == 0 ? 1.5 : 1,
                        Path = new SliderPath(new[] { new PathControlPoint(Vector2.Zero), new PathControlPoint(new Vector2(100, 0)) }),
                    });
                }

                EditorBeatmap.SelectedHitObjects.AddRange(sliders.Skip(1));
            });
        }

        private void press(Key key, params Key[] modifiers) => AddStep($"press {string.Join("+", modifiers.Append(key))}", () =>
        {
            foreach (var m in modifiers)
                InputManager.PressKey(m);

            InputManager.Key(key);

            foreach (var m in modifiers)
                InputManager.ReleaseKey(m);
        });

        [Test]
        public void TestStepsAndUndo()
        {
            press(Key.BracketRight);
            AddAssert("+0.1 on the selected", () => sliders.Select(s => s.SliderVelocityMultiplier).ToArray(), () => Is.EqualTo(new[] { 1.5, 1.1, 1.1 }));

            press(Key.BracketRight, Key.ControlLeft);
            AddAssert("+0.25 with ctrl", () => sliders[1].SliderVelocityMultiplier, () => Is.EqualTo(1.35).Within(1e-6));

            press(Key.BracketLeft, Key.AltLeft);
            AddAssert("-0.01 with alt", () => sliders[1].SliderVelocityMultiplier, () => Is.EqualTo(1.34));

            AddStep("undo", () => Editor.Undo());
            AddAssert("one step undone", () => EditorBeatmap.HitObjects.OfType<Slider>().OrderBy(s => s.StartTime).Skip(1).First().SliderVelocityMultiplier, () => Is.EqualTo(1.35).Within(1e-6));
        }

        [Test]
        public void TestRestorePreviousVelocity()
        {
            AddStep("select only the middle one", () =>
            {
                EditorBeatmap.SelectedHitObjects.Clear();
                EditorBeatmap.SelectedHitObjects.Add(sliders[1]);
            });
            press(Key.BackSlash);
            AddAssert("takes the first slider's velocity", () => sliders[1].SliderVelocityMultiplier, () => Is.EqualTo(1.5));
            AddAssert("others untouched", () => sliders[2].SliderVelocityMultiplier, () => Is.EqualTo(1));
        }
    }
}
