// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Rulesets.Osu.Edit;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Osu.Tests.Editor
{
    /// <summary>
    /// YAWNS: the quick rotate hotkeys and Ctrl+Shift+Scroll live rotate.
    /// </summary>
    public partial class TestSceneQuickRotate : TestSceneOsuEditor
    {
        private MappingToolboxGroup tools => Editor.ChildrenOfType<MappingToolboxGroup>().Single();

        private HitCircle[] circles => EditorBeatmap.HitObjects.OfType<HitCircle>().OrderBy(h => h.StartTime).ToArray();

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("place two circles and select them", () =>
            {
                EditorBeatmap.Clear();
                EditorBeatmap.AddRange(new[]
                {
                    new HitCircle { StartTime = 1000, Position = new Vector2(100, 100) },
                    new HitCircle { StartTime = 1500, Position = new Vector2(200, 100) },
                });
                EditorBeatmap.SelectedHitObjects.AddRange(EditorBeatmap.HitObjects);
                tools.QuickRotateStep.Value = 90;
            });
        }

        private bool near(Vector2 a, Vector2 b) => Vector2.Distance(a, b) < 0.1f;

        [Test]
        public void TestQuickRotateHotkeys()
        {
            AddUntilStep("tools enabled", () => tools.CanSavePattern.Value);

            AddStep("Ctrl+Alt+. (clockwise)", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.PressKey(Key.AltLeft);
                InputManager.Key(Key.Period);
                InputManager.ReleaseKey(Key.AltLeft);
                InputManager.ReleaseKey(Key.ControlLeft);
            });
            AddAssert("turned a quarter clockwise around their centre", () => near(circles[0].Position, new Vector2(150, 50)) && near(circles[1].Position, new Vector2(150, 150)));

            AddStep("Ctrl+Alt+, (anticlockwise)", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.PressKey(Key.AltLeft);
                InputManager.Key(Key.Comma);
                InputManager.ReleaseKey(Key.AltLeft);
                InputManager.ReleaseKey(Key.ControlLeft);
            });
            AddAssert("and back", () => near(circles[0].Position, new Vector2(100, 100)) && near(circles[1].Position, new Vector2(200, 100)));
        }

        [Test]
        public void TestCtrlShiftScrollRotates()
        {
            AddStep("Ctrl+Shift+scroll (horizontal, as Shift often makes it), 18 notches", () =>
            {
                InputManager.MoveMouseTo(Editor.ChildrenOfType<osu.Game.Screens.Edit.Compose.Components.ComposeBlueprintContainer>().First().ScreenSpaceDrawQuad.Centre);
                InputManager.PressKey(Key.ControlLeft);
                InputManager.PressKey(Key.ShiftLeft);

                for (int i = 0; i < 18; i++)
                    InputManager.ScrollHorizontalBy(1);

                InputManager.ReleaseKey(Key.ShiftLeft);
                InputManager.ReleaseKey(Key.ControlLeft);
            });
            AddAssert("a quarter turn (5 degrees a notch)", () => near(circles[0].Position, new Vector2(150, 50)) || near(circles[0].Position, new Vector2(150, 150)));
        }

        [Test]
        public void TestRotateByNotchesAddsUp()
        {
            // The wheel binding itself (Ctrl+Shift+Scroll) cannot be driven from a headless test, so this drives what it calls.
            AddStep("rotate by 90 in 18 notches of 5", () =>
            {
                for (int i = 0; i < 18; i++)
                    tools.RotateBy(5);
            });
            AddAssert("a quarter turn clockwise around their centre", () => near(circles[0].Position, new Vector2(150, 50)) && near(circles[1].Position, new Vector2(150, 150)));
        }
    }
}
