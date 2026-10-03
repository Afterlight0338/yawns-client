// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Rulesets.Osu.Edit;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Editor
{
    /// <summary>
    /// YAWNS: changing a copy preview's settings many times must replace the previous preview, never pile copies up.
    /// </summary>
    public partial class TestSceneCopyPreviewLeak : TestSceneOsuEditor
    {
        private MappingToolboxGroup tools => Editor.ChildrenOfType<MappingToolboxGroup>().Single();

        [Test]
        public void TestRadialCopyPreviewDoesNotAccumulate()
        {
            AddStep("place three circles and select them", () =>
            {
                EditorBeatmap.Clear();
                EditorBeatmap.AddRange(Enumerable.Range(0, 3).Select(i => new HitCircle { StartTime = 1000 + i * 500, Position = new Vector2(200 + i * 20, 150) }));
                EditorBeatmap.SelectedHitObjects.AddRange(EditorBeatmap.HitObjects);
            });
            AddUntilStep("tools enabled", () => tools.CanSavePattern.Value);
            AddStep("open radial copy", () => tools.ShowRadialCopy());
            AddUntilStep("popover shown", () => this.ChildrenOfType<RadialCopyPopover>().Any());

            AddStep("drag the divisions up and down 40 times", () =>
            {
                for (int i = 0; i < 40; i++)
                    tools.RadialCopy.Count.Value = 3 + i % 6;
            });

            // The last value set is 3 + 39 % 6 = 6 divisions, so 5 copies of 3 objects, plus the 3 originals.
            AddUntilStep("exactly the originals plus one preview", () => EditorBeatmap.HitObjects.Count, () => Is.EqualTo(3 + 5 * 3));
        }
    }
}
