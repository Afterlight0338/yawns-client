// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Compose.Components;
using osu.Game.Screens.Edit.Components;
using osu.Game.Screens.Edit.MappingTools;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// YAWNS: selection tools on the compose screen. Also reachable from the right-click "Tools" menu.
    /// </summary>
    public partial class MappingToolboxGroup : EditorToolboxGroup
    {
        public readonly StreamOrganiser StreamOrganiser = new StreamOrganiser();

        public readonly RadialCopy RadialCopy = new RadialCopy();

        public readonly SliderCompletionator SliderCompletionator = new SliderCompletionator();

        public readonly TumourGenerator TumourGenerator = new TumourGenerator();

        public readonly BindableBool CanCompleteSliders = new BindableBool();

        public readonly BindableBool CanOrganiseStream = new BindableBool();

        public readonly BindableBool CanSavePattern = new BindableBool();

        public readonly BindableBool CanAlignToAxis = new BindableBool();

        public AxisGuide AxisGuide { get; set; } = null!;

        public SnappingToolsOverlay SnappingTools { get; set; } = null!;

        public SelectionRotationHandler RotationHandler { get; set; } = null!;

        private readonly BindableList<HitObject> selectedHitObjects = new BindableList<HitObject>();

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        private EditorToolButton streamButton = null!;
        private EditorToolButton patternButton = null!;
        private EditorToolButton axisButton = null!;
        private EditorToolButton snappingButton = null!;
        private EditorToolButton radialButton = null!;
        private EditorToolButton completeButton = null!;
        private EditorToolButton slideratorButton = null!;
        private EditorToolButton tumourButton = null!;

        public MappingToolboxGroup()
            : base("mapping tools")
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Child = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Spacing = new Vector2(5),
                Children = new Drawable[]
                {
                    streamButton = new EditorToolButton("Stream",
                        () => new SpriteIcon { Icon = FontAwesome.Solid.EllipsisH },
                        () => new StreamOrganiserPopover(StreamOrganiser)),
                    patternButton = new EditorToolButton("Save pattern",
                        () => new SpriteIcon { Icon = FontAwesome.Solid.Shapes },
                        () => new SavePatternPopover()),
                    axisButton = new EditorToolButton("Axis guide",
                        () => new SpriteIcon { Icon = FontAwesome.Solid.DraftingCompass },
                        () => null),
                    snappingButton = new EditorToolButton("Snapping tools",
                        () => new SpriteIcon { Icon = FontAwesome.Solid.Magnet },
                        () => null),
                    radialButton = new EditorToolButton("Radial copy",
                        () => new SpriteIcon { Icon = FontAwesome.Solid.SyncAlt },
                        () => new RadialCopyPopover(RadialCopy)),
                    completeButton = new EditorToolButton("Complete sliders",
                        () => new SpriteIcon { Icon = FontAwesome.Solid.RulerHorizontal },
                        () => new SliderCompletionatorPopover(SliderCompletionator)),
                    slideratorButton = new EditorToolButton("Sliderator",
                        () => new SpriteIcon { Icon = FontAwesome.Solid.TachometerAlt },
                        () => new SlideratorPopover()),
                    tumourButton = new EditorToolButton("Tumours",
                        () => new SpriteIcon { Icon = FontAwesome.Solid.Splotch },
                        () => new TumourGeneratorPopover(TumourGenerator)),
                }
            };

            selectedHitObjects.BindTo(editorBeatmap.SelectedHitObjects);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Only plain circles for now: moving a slider's head would also need its body moved.
            selectedHitObjects.BindCollectionChanged((_, _) => CanOrganiseStream.Value = selectedHitObjects.Count >= 3 && selectedHitObjects.All(h => h is HitCircle), true);
            selectedHitObjects.BindCollectionChanged((_, _) => CanSavePattern.Value = selectedHitObjects.Count > 0, true);
            CanOrganiseStream.BindValueChanged(can => streamButton.Enabled.Value = can.NewValue, true);
            CanSavePattern.BindValueChanged(can => patternButton.Enabled.Value = can.NewValue, true);
            CanSavePattern.BindValueChanged(can => radialButton.Enabled.Value = can.NewValue, true);
            selectedHitObjects.BindCollectionChanged((_, _) => CanCompleteSliders.Value = selectedHitObjects.Any(h => h is Slider), true);
            CanCompleteSliders.BindValueChanged(can => completeButton.Enabled.Value = can.NewValue, true);
            CanCompleteSliders.BindValueChanged(can => slideratorButton.Enabled.Value = can.NewValue, true);
            CanCompleteSliders.BindValueChanged(can => tumourButton.Enabled.Value = can.NewValue, true);

            axisButton.Selected.BindTo(AxisGuide.Enabled);
            snappingButton.Selected.BindTo(SnappingTools.Enabled);
            selectedHitObjects.BindCollectionChanged((_, _) => CanAlignToAxis.Value = AxisGuide.SelectionAngle(selectedHitObjects) != null, true);
        }

        public void ShowStreamOrganiser()
        {
            if (CanOrganiseStream.Value && !streamButton.Selected.Value)
                streamButton.TriggerClick();
        }

        public void ShowSavePattern()
        {
            if (CanSavePattern.Value && !patternButton.Selected.Value)
                patternButton.TriggerClick();
        }

        public void ShowRadialCopy()
        {
            if (CanSavePattern.Value && !radialButton.Selected.Value)
                radialButton.TriggerClick();
        }

        public void ShowTumourGenerator()
        {
            if (CanCompleteSliders.Value && !tumourButton.Selected.Value)
                tumourButton.TriggerClick();
        }

        public void ShowSliderator()
        {
            if (CanCompleteSliders.Value && !slideratorButton.Selected.Value)
                slideratorButton.TriggerClick();
        }

        public void ShowSliderCompletionator()
        {
            if (CanCompleteSliders.Value && !completeButton.Selected.Value)
                completeButton.TriggerClick();
        }

        /// <summary>
        /// Rotates the selection so its axis (first object to last object's end) lies on the nearest of the map's base axes.
        /// </summary>
        public void AlignToAxis()
        {
            if (AxisGuide.SelectionAngle(selectedHitObjects) is not double angle)
                return;

            double tilt = AxisFinder.DetectTilt(editorBeatmap.HitObjects) ?? AxisFinder.DEFAULT_TILT;
            RotationHandler.Rotate((float)AxisFinder.RotationToNearestAxis(angle, tilt));
        }
    }
}
