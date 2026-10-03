// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.Screens.Edit.MappingTools.Hitsounds
{
    /// <summary>
    /// YAWNS: a lane's name, mute and solo, volume for new hits, and remove.
    /// </summary>
    public partial class HitsoundLaneHeader : CompositeDrawable
    {
        public readonly HitsoundLane Lane;

        private readonly Action remove;

        public HitsoundLaneHeader(HitsoundLane lane, Action remove)
        {
            Lane = lane;
            this.remove = remove;

            RelativeSizeAxes = Axes.X;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            InternalChildren = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = Lane.Colour.Opacity(0.12f) },
                new Box { RelativeSizeAxes = Axes.Y, Width = 4, Colour = Lane.Colour },
                new TruncatingSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 10,
                    Width = 110,
                    Text = Lane.Sound.Name,
                    Font = OsuFont.Default.With(size: 13, weight: FontWeight.SemiBold),
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(3),
                    Padding = new MarginPadding { Right = 4 },
                    Children = new Drawable[]
                    {
                        new LaneToggle("M", Lane.Muted, colourProvider.Colour2),
                        new LaneToggle("S", Lane.Solo, colourProvider.Colour1),
                        new RoundedSliderBar<int>
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Width = 60,
                            Current = Lane.Volume,
                        },
                        new DangerousRoundedButton
                        {
                            Width = 24,
                            Height = 24,
                            Text = "x",
                            Action = remove,
                        },
                    }
                },
            };
        }

        private partial class LaneToggle : RoundedButton
        {
            private readonly BindableBool current;
            private readonly Colour4 onColour;

            public LaneToggle(string text, BindableBool current, Colour4 onColour)
            {
                this.current = current;
                this.onColour = onColour;

                Text = text;
                Width = 24;
                Height = 24;
                Action = current.Toggle;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                current.BindValueChanged(on => BackgroundColour = on.NewValue ? onColour : Colour4.Gray.Darken(1), true);
            }
        }
    }
}
