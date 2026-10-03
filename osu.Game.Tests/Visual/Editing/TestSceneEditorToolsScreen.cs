// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components;
using osu.Game.Screens.Edit.MappingTools;
using osu.Game.Screens.Edit.Reference;

namespace osu.Game.Tests.Visual.Editing
{
    /// <summary>
    /// YAWNS: the Tools tab.
    /// </summary>
    public partial class TestSceneEditorToolsScreen : EditorTestScene
    {
        protected override Ruleset CreateEditorRuleset() => new OsuRuleset();

        private ToolsScreen? screen => Editor.ChildrenOfType<ToolsScreen>().SingleOrDefault();

        [Test]
        public void TestSwitchToolsAndCopyWithoutOverlay()
        {
            AddStep("open tools tab", () => Editor.Mode.Value = EditorScreenMode.Tools);
            AddUntilStep("tools screen loaded", () => screen?.IsLoaded == true);
            AddUntilStep("hitsound copier shown first", () => screen!.ChildrenOfType<HitsoundCopierPanel>().Count(), () => Is.EqualTo(1));

            AddStep("pick timing copier", () => toolButton("Timing Copier").TriggerClick());
            AddUntilStep("timing copier shown", () => screen!.ChildrenOfType<TimingCopierPanel>().Count(), () => Is.EqualTo(1));
            AddAssert("hitsound copier gone", () => screen!.ChildrenOfType<HitsoundCopierPanel>().Any(), () => Is.False);

            AddStep("click timing copier again", () => toolButton("Timing Copier").TriggerClick());
            AddUntilStep("it stays open", () => toolButton("Timing Copier").Selected.Value);

            AddStep("copy with nothing overlaid", () => screen!.ChildrenOfType<TimingCopierPanel>().Single().ChildrenOfType<RoundedButton>().Single().TriggerClick());
            AddAssert("asks for an overlay map", () => screen!.ChildrenOfType<OsuSpriteText>().Any(t => t.Text.ToString() == "Pick a map to overlay first."));
        }

        [Test]
        public void TestPropertyTransformer()
        {
            double[] before = null!;

            AddStep("remember object times", () => before = EditorBeatmap.HitObjects.Select(h => h.StartTime).ToArray());
            AddStep("open tools tab", () => Editor.Mode.Value = EditorScreenMode.Tools);
            AddUntilStep("tools screen loaded", () => screen?.IsLoaded == true);
            AddStep("pick property transformer", () => toolButton("Property Transformer").TriggerClick());
            AddUntilStep("panel shown", () => screen!.ChildrenOfType<PropertyTransformerPanel>().Count(), () => Is.EqualTo(1));

            AddStep("object time + 100", () =>
            {
                var panel = screen!.ChildrenOfType<PropertyTransformerPanel>().Single();
                // The sixth row is object time; its second box is the offset.
                panel.ChildrenOfType<FormNumberBox>().ElementAt(5 * 2 + 1).Current.Value = "100";
                panel.ChildrenOfType<RoundedButton>().Single(b => b.Text.ToString() == "Transform this difficulty").TriggerClick();
            });
            AddAssert("every object 100ms later", () => EditorBeatmap.HitObjects.Select(h => h.StartTime), () => Is.EqualTo(before.Select(t => t + 100)));

            AddStep("undo", () => Editor.Undo());
            AddAssert("back", () => EditorBeatmap.HitObjects.Select(h => h.StartTime), () => Is.EqualTo(before));
        }

        [TestCase("Map Cleaner", typeof(MapCleanerPanel))]
        [TestCase("Mapset Merger", typeof(MapsetMergerPanel))]
        [TestCase("Combo Colour Studio", typeof(ComboColourStudioPanel))]
        [TestCase("Timing Helper", typeof(TimingHelperPanel))]
        public void TestNewToolPanelsOpen(string name, System.Type panelType)
        {
            AddStep("open tools tab", () => Editor.Mode.Value = EditorScreenMode.Tools);
            AddUntilStep("tools screen loaded", () => screen?.IsLoaded == true);
            AddStep($"pick {name}", () => toolButton(name).TriggerClick());
            AddUntilStep("panel loaded", () => screen!.ChildrenOfType<osu.Framework.Graphics.Drawable>().Any(d => d.GetType() == panelType && d.IsLoaded));
        }

        [Test]
        public void TestMapCleanerResnapsFromPanel()
        {
            double snapped = 0;

            AddStep("unsnap the first object by 3ms", () =>
            {
                var first = EditorBeatmap.HitObjects.First();
                snapped = first.StartTime;
                first.StartTime += 3;
                EditorBeatmap.Update(first);
            });
            AddStep("open tools tab", () => Editor.Mode.Value = EditorScreenMode.Tools);
            AddUntilStep("tools screen loaded", () => screen?.IsLoaded == true);
            AddStep("pick map cleaner", () => toolButton("Map Cleaner").TriggerClick());
            AddUntilStep("panel shown", () => screen!.ChildrenOfType<MapCleanerPanel>().Count(), () => Is.EqualTo(1));
            AddStep("clean", () => screen!.ChildrenOfType<MapCleanerPanel>().Single().ChildrenOfType<RoundedButton>().Single().TriggerClick());
            AddAssert("back on the beat", () => EditorBeatmap.HitObjects.First().StartTime, () => Is.EqualTo(snapped));
        }

        private EditorToolButton toolButton(string name) => screen!.ChildrenOfType<EditorToolButton>().Single(b => b.Text.ToString() == name);
    }
}
