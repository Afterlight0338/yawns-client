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
    /// YAWNS: the step of the quick rotate hotkeys (Ctrl+Alt+. and Ctrl+Alt+,), and buttons that do the same rotation.
    /// </summary>
    public partial class QuickRotatePopover : OsuPopover
    {
        private readonly MappingToolboxGroup tools;

        public QuickRotatePopover(MappingToolboxGroup tools)
        {
            this.tools = tools;
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
                    new FormSliderBar<float>
                    {
                        Caption = "Step (degrees)",
                        HintText = "Decimals are fine: type the angle you want.",
                        Current = tools.QuickRotateStep,
                        TabbableContentContainer = this,
                    },
                    new RoundedButton { RelativeSizeAxes = Axes.X, Text = "Rotate clockwise", Action = () => tools.QuickRotate(1) },
                    new RoundedButton { RelativeSizeAxes = Axes.X, Text = "Rotate anticlockwise", Action = () => tools.QuickRotate(-1) },
                    new OsuTextFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Text = "Hotkeys: Ctrl+Alt+. clockwise, Ctrl+Alt+, anticlockwise. Ctrl+Shift+Scroll rotates live, 5 degrees per notch (hold Alt for 1). Around the origin chosen in the rotate popover.",
                    },
                }
            };
        }
    }
}
