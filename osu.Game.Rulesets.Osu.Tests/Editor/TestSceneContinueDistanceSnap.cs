// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Osu.Edit;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Graphics.UserInterface;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Editor
{
    /// <summary>
    /// YAWNS: "continue the distance between the last two objects" sets the distance spacing from them.
    /// </summary>
    public partial class TestSceneContinueDistanceSnap : TestSceneOsuEditor
    {
        private OsuDistanceSnapProvider provider => Editor.ChildrenOfType<OsuDistanceSnapProvider>().Single();

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("two circles one beat and 70 px apart", () =>
            {
                EditorBeatmap.Clear();
                EditorBeatmap.ControlPointInfo.Clear();
                EditorBeatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
                EditorBeatmap.Difficulty.SliderMultiplier = 1.4;
                EditorBeatmap.Add(new HitCircle { StartTime = 1000, Position = new Vector2(100, 100) });
                EditorBeatmap.Add(new HitCircle { StartTime = 1500, Position = new Vector2(170, 100) });
            });
        }

        [Test]
        public void TestUsesTheSelectedObjectAndTheOneBefore()
        {
            AddStep("select the second circle and continue", () =>
            {
                EditorBeatmap.SelectedHitObjects.Add(EditorBeatmap.HitObjects.Last());
                Assert.That(provider.ContinueDistanceSnap(), Is.True);
            });
            // One beat is 100 * 1.4 = 140 px at spacing 1, so 70 px is 0.5.
            AddAssert("spacing is 0.5", () => provider.DistanceSpacingMultiplier.Value, () => Is.EqualTo(0.5).Within(0.001));
            AddAssert("distance snap is on", () => provider.DistanceSnapToggle.Value, () => Is.EqualTo(TernaryState.True));
        }

        [Test]
        public void TestNeedsTwoObjects()
        {
            AddStep("remove the first", () => EditorBeatmap.Remove(EditorBeatmap.HitObjects.First()));
            AddAssert("nothing to continue", () => provider.ContinueDistanceSnap(), () => Is.False);
        }
    }
}
