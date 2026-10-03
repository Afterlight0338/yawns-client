// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// YAWNS: settings for the <see cref="RadialGuide"/>. Opening it shows the guide.
    /// </summary>
    public partial class RadialGuidePopover : OsuPopover
    {
        private readonly RadialGuide guide;

        public RadialGuidePopover(RadialGuide guide)
        {
            this.guide = guide;
            AllowableAnchors = new[] { Anchor.CentreLeft, Anchor.CentreRight };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Child = new FillFlowContainer
            {
                Width = 300,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(5),
                Children = new Drawable[]
                {
                    new FormCheckBox { Caption = "Show guide", Current = guide.Enabled },
                    new FormSliderBar<int>
                    {
                        Caption = "Spokes",
                        HintText = "The full circle in this many steps: 6 is every 60 degrees, 8 every 45.",
                        Current = guide.Spokes,
                        TabbableContentContainer = this,
                    },
                    new FormSliderBar<float> { Caption = "First spoke angle", Current = guide.Offset, TabbableContentContainer = this },
                    new FormSliderBar<int> { Caption = "Rings", Current = guide.Rings, TabbableContentContainer = this },
                    new FormSliderBar<float> { Caption = "Ring spacing (px)", Current = guide.RingSpacing, TabbableContentContainer = this },
                    new OsuTextFlowContainer { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Text = "Drag the centre dot on the playfield. Placing and dragging snap to crossings, spokes and rings. Radial copy can turn around the centre (\"Radial guide centre\")." },
                }
            };
        }

        protected override void PopIn()
        {
            base.PopIn();
            guide.Enabled.Value = true;
        }
    }
}
