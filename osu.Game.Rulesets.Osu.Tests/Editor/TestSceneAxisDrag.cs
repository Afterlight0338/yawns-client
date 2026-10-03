// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Edit.Blueprints.HitCircles.Components;
using osu.Game.Screens.Edit.MappingTools;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Osu.Tests.Editor
{
    /// <summary>
    /// YAWNS: dragging with Ctrl+Alt held stays on the nearest base axis.
    /// </summary>
    public partial class TestSceneAxisDrag : TestSceneOsuEditor
    {
        [Test]
        public void TestProjectOntoNearestAxis()
        {
            // The default tilt is 10 degrees: a drag of (100, 5) is closest to the +10 degree axis.
            Vector2 projected = AxisFinder.ProjectOntoNearestAxis(new Vector2(100, 5), 10);
            Assert.That(Math.Atan2(projected.Y, projected.X) * 180 / Math.PI, Is.EqualTo(10).Within(0.01));
            Assert.That(projected.Length, Is.EqualTo(Vector2.Dot(new Vector2(100, 5), projected.Normalized())).Within(0.01f));

            Assert.That(AxisFinder.ProjectOntoNearestAxis(Vector2.Zero, 10), Is.EqualTo(Vector2.Zero));

            // Straight down is closest to the 90 + 10 and 90 - 10 axes: it stays within 10 degrees of vertical, never horizontal.
            Vector2 down = AxisFinder.ProjectOntoNearestAxis(new Vector2(0, -80), 10);
            Assert.That(Math.Abs(down.X), Is.LessThan(Math.Abs(down.Y)));
        }

        [Test]
        public void TestDragWithCtrlAltStaysOnAnAxis()
        {
            Vector2 start = Vector2.Zero;

            AddStep("place a circle and select it", () =>
            {
                EditorBeatmap.Clear();
                var circle = new HitCircle { StartTime = 1000, Position = new Vector2(200, 150) };
                EditorBeatmap.Add(circle);
                EditorBeatmap.SelectedHitObjects.Add(circle);
                start = circle.Position;
            });
            AddUntilStep("blueprint shown", () => this.ChildrenOfType<HitCirclePiece>().Any());

            AddStep("drag with Ctrl+Alt", () =>
            {
                var piece = this.ChildrenOfType<HitCirclePiece>().First();
                InputManager.MoveMouseTo(piece);
                InputManager.PressKey(Key.ControlLeft);
                InputManager.PressKey(Key.AltLeft);
                InputManager.PressButton(MouseButton.Left);
                InputManager.MoveMouseTo(piece.ScreenSpaceDrawQuad.Centre + new Vector2(120, 6));
            });
            AddStep("release", () =>
            {
                InputManager.ReleaseButton(MouseButton.Left);
                InputManager.ReleaseKey(Key.AltLeft);
                InputManager.ReleaseKey(Key.ControlLeft);
            });

            AddAssert("moved along a base axis (10 degrees)", () =>
            {
                Vector2 moved = EditorBeatmap.HitObjects.OfType<HitCircle>().Single().Position - start;
                return moved.Length > 20 && Math.Abs(Math.Atan2(moved.Y, moved.X) * 180 / Math.PI - 10) < 1;
            });
        }
    }
}
