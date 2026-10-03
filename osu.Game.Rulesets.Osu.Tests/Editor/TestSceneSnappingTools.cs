// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Osu.Edit;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Tests.Beatmaps;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Osu.Tests.Editor
{
    /// <summary>
    /// YAWNS: placing a circle with Snapping Tools on.
    /// </summary>
    public partial class TestSceneSnappingTools : TestSceneOsuEditor
    {
        private OsuPlayfield playfield = null!;

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset) => new TestBeatmap(Ruleset.Value, false);

        [Test]
        public void TestCircleSnapsToMidpoint()
        {
            AddStep("two circles, a beat apart", () =>
            {
                EditorBeatmap.ControlPointInfo.Clear();
                EditorBeatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
                EditorBeatmap.Add(new HitCircle { StartTime = 1000, Position = new Vector2(100, 200) });
                EditorBeatmap.Add(new HitCircle { StartTime = 1500, Position = new Vector2(200, 200), NewCombo = true });
            });
            AddStep("get playfield", () => playfield = Editor.ChildrenOfType<OsuPlayfield>().First());
            AddStep("seek between them", () => EditorClock.Seek(1250));
            AddStep("disable distance snap", () => InputManager.Key(Key.Q));
            AddStep("turn on snapping tools", () => Editor.ChildrenOfType<SnappingToolsOverlay>().Single().Enabled.Value = true);

            AddStep("enter circle placement mode", () => InputManager.Key(Key.Number2));
            AddStep("move a little off the midpoint", () => InputManager.MoveMouseTo(playfield.ToScreenSpace(new Vector2(152, 204))));
            AddStep("place", () => InputManager.Click(MouseButton.Left));

            AddAssert("placed on the midpoint", () => ((OsuHitObject)EditorBeatmap.HitObjects.Single(h => h.StartTime == 1250)).Position,
                () => Is.EqualTo(new Vector2(150, 200)).Using(Vector2EqualityComparer));
        }

        private static readonly System.Collections.Generic.IEqualityComparer<Vector2> Vector2EqualityComparer =
            System.Collections.Generic.EqualityComparer<Vector2>.Create((a, b) => Vector2.Distance(a, b) < 0.1f, v => 0);
    }
}
