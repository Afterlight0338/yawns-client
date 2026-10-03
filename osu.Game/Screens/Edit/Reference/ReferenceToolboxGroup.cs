// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Beatmaps;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets.Edit;
using osu.Game.Screens.Edit.Components;
using osuTK;

namespace osu.Game.Screens.Edit.Reference
{
    /// <summary>
    /// YAWNS: compose screen controls for the map overlay.
    /// </summary>
    public partial class ReferenceToolboxGroup : EditorToolboxGroup
    {
        private readonly Bindable<IBeatmap?> beatmap = new Bindable<IBeatmap?>();

        private EditorToolButton hitsoundsButton = null!;
        private EditorToolButton timingButton = null!;
        private ExpandableSlider<float> opacitySlider = null!;

        public ReferenceToolboxGroup()
            : base("overlay")
        {
        }

        [BackgroundDependencyLoader]
        private void load(EditorReferenceBeatmap reference)
        {
            beatmap.BindTo(reference.Beatmap);

            Child = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Spacing = new Vector2(5),
                Children = new Drawable[]
                {
                    new EditorToolButton("Map",
                        () => new SpriteIcon { Icon = FontAwesome.Solid.LayerGroup },
                        () => new ReferencePickerPopover()),
                    hitsoundsButton = new EditorToolButton("Hitsounds",
                        () => new SpriteIcon { Icon = FontAwesome.Solid.VolumeUp },
                        () => new ReferenceHitsoundsPopover()),
                    timingButton = new EditorToolButton("Timing",
                        () => new SpriteIcon { Icon = FontAwesome.Solid.Clock },
                        () => new ReferenceTimingPopover()),
                    opacitySlider = new ExpandableSlider<float>
                    {
                        Current = reference.Opacity,
                        ExpandedLabelText = "Opacity",
                    },
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            opacitySlider.Current.BindValueChanged(o => opacitySlider.ContractedLabelText = $"Overlay {o.NewValue:P0}", true);

            // Copying needs a reference to copy from.
            beatmap.BindValueChanged(b => hitsoundsButton.Enabled.Value = timingButton.Enabled.Value = b.NewValue != null, true);
        }
    }
}
