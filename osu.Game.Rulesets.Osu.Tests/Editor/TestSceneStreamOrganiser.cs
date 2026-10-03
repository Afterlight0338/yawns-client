// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Edit;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit.Components;
using osu.Game.Screens.Edit.MappingTools;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Editor
{
    /// <summary>
    /// YAWNS: the stream organiser on the compose screen.
    /// </summary>
    public partial class TestSceneStreamOrganiser : TestSceneOsuEditor
    {
        private static readonly float[] wobbly_x = { 100, 110, 130, 135, 160, 200 };

        private MappingToolboxGroup tools => Editor.ChildrenOfType<MappingToolboxGroup>().Single();

        private StreamOrganiserPopover? popover => this.ChildrenOfType<StreamOrganiserPopover>().SingleOrDefault();

        private HitCircle[] stream => EditorBeatmap.HitObjects.OfType<HitCircle>().OrderBy(h => h.StartTime).ToArray();

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("place a wobbly stream", () =>
            {
                EditorBeatmap.Clear();
                EditorBeatmap.AddRange(wobbly_x.Select((x, i) => new HitCircle { StartTime = 1000 + i * 125, Position = new Vector2(x, 100) }));
                EditorBeatmap.SelectedHitObjects.AddRange(EditorBeatmap.HitObjects);
            });
        }

        [Test]
        public void TestOrganiseThenUndo()
        {
            AddUntilStep("tool enabled", () => tools.CanOrganiseStream.Value);
            AddStep("set even spacing", () => tools.StreamOrganiser.Speed.Value = StreamOrganiser.SpeedMode.Even);

            AddStep("open organiser", () => tools.ShowStreamOrganiser());
            AddUntilStep("popover shown", () => popover, () => Is.Not.Null);
            AddUntilStep("evenly spaced", () => gaps().All(g => near(g, 20)));

            AddStep("accelerate", () => tools.StreamOrganiser.Speed.Value = StreamOrganiser.SpeedMode.Accelerate);
            AddUntilStep("gaps grow", () =>
            {
                float[] g = gaps();
                return g.Zip(g.Skip(1), (a, b) => b > a).All(x => x);
            });

            AddStep("close organiser", () => Editor.ChildrenOfType<EditorToolButton>().Single(b => b.Text.ToString() == "Stream").TriggerClick());
            AddUntilStep("popover gone", () => popover, () => Is.Null);

            AddStep("undo once", () => Editor.Undo());
            AddAssert("back to the hand-placed stream", () => stream.Select(h => h.X), () => Is.EqualTo(wobbly_x));
        }

        [Test]
        public void TestNeedsOnlyCircles()
        {
            AddStep("add a slider to the selection", () =>
            {
                var slider = new Slider
                {
                    StartTime = 2000,
                    Position = new Vector2(300, 300),
                    Path = new SliderPath(new[] { new PathControlPoint(Vector2.Zero), new PathControlPoint(new Vector2(100, 0)) }),
                };
                EditorBeatmap.Add(slider);
                EditorBeatmap.SelectedHitObjects.Add(slider);
            });
            AddUntilStep("tool disabled", () => !tools.CanOrganiseStream.Value);

            AddStep("select two circles", () =>
            {
                EditorBeatmap.SelectedHitObjects.Clear();
                EditorBeatmap.SelectedHitObjects.AddRange(stream.Take(2));
            });
            AddUntilStep("still disabled", () => !tools.CanOrganiseStream.Value);
        }

        private float[] gaps() => stream.Zip(stream.Skip(1), (a, b) => Vector2.Distance(a.Position, b.Position)).ToArray();

        private static bool near(float value, float expected) => System.Math.Abs(value - expected) < 0.5f;
    }
}
