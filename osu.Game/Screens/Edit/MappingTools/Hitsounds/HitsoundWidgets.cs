// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osuTK;

namespace osu.Game.Screens.Edit.MappingTools.Hitsounds
{
    /// <summary>
    /// YAWNS: Hitsound Studio's palette (style.css :root), which also matches the canvas.
    /// </summary>
    public static class StudioColours
    {
        public static readonly Colour4 BG = Colour4.FromHex("0f1115");
        public static readonly Colour4 PANEL = Colour4.FromHex("14171f");
        public static readonly Colour4 RAISED = Colour4.FromHex("1a1e28");
        public static readonly Colour4 INPUT = Colour4.FromHex("202533");
        public static readonly Colour4 BORDER = Colour4.FromHex("2b3142");
        public static readonly Colour4 BORDER_SOFT = Colour4.FromHex("222734");
        public static readonly Colour4 TEXT = Colour4.FromHex("e9ecf2");
        public static readonly Colour4 MUTED = Colour4.FromHex("8c96a8");
        public static readonly Colour4 FAINT = Colour4.FromHex("5d6679");
        public static readonly Colour4 ACCENT = Colour4.FromHex("ff66aa");
        public static readonly Colour4 ACCENT_HOVER = Colour4.FromHex("ff4f9c");
        public static readonly Colour4 ACCENT_TEXT = Colour4.FromHex("1a0a12");
        public static readonly Colour4 DANGER = Colour4.FromHex("f05d5d");
        public static readonly Colour4 WARN = Colour4.FromHex("f2b24c");
        public static readonly Colour4 OK = Colour4.FromHex("4fd18b");
    }

    public enum StudioButtonStyle
    {
        Normal,
        Primary,
        Ghost,
        Icon,
        Play,
    }

    /// <summary>
    /// YAWNS: Hitsound Studio's buttons (.btn, .btn-primary, .btn-ghost, .icon-btn, .icon-btn.play).
    /// </summary>
    public partial class StudioButton : OsuClickableContainer
    {
        public readonly BindableBool Active = new BindableBool();

        private readonly StudioButtonStyle style;
        private readonly Box background;
        private readonly OsuSpriteText? text;
        private readonly SpriteIcon? icon;

        public StudioButton(string label, StudioButtonStyle style = StudioButtonStyle.Normal, float height = 30, float fontSize = 11)
            : this(style)
        {
            Height = height;
            AutoSizeAxes = Axes.X;
            Add(text = new OsuSpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = label,
                Font = OsuFont.Default.With(size: fontSize, weight: FontWeight.Bold),
                Margin = new MarginPadding { Horizontal = height < 26 ? 8 : 12 },
            });
        }

        public StudioButton(IconUsage iconUsage, StudioButtonStyle style = StudioButtonStyle.Icon, float size = 30)
            : this(style)
        {
            Size = new Vector2(size);
            Add(icon = new SpriteIcon
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Icon = iconUsage,
                Size = new Vector2(size * 0.42f),
            });
        }

        private StudioButton(StudioButtonStyle style)
        {
            this.style = style;

            Masking = true;
            CornerRadius = 6;
            BorderThickness = style == StudioButtonStyle.Normal ? 1 : 0;
            BorderColour = StudioColours.BORDER;

            Add(background = new Box { RelativeSizeAxes = Axes.Both, Depth = 1 });
        }

        public string Text
        {
            set
            {
                if (text != null)
                    text.Text = value;
            }
        }

        public IconUsage Icon
        {
            set
            {
                if (icon != null)
                    icon.Icon = value;
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Active.BindValueChanged(_ => updateState());
            Enabled.BindValueChanged(_ => updateState(), true);
        }

        protected override bool OnHover(HoverEvent e)
        {
            updateState();
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            updateState();
            base.OnHoverLost(e);
        }

        private void updateState()
        {
            bool hovered = IsHovered && Enabled.Value;
            Colour4 bg, fg;

            switch (style)
            {
                case StudioButtonStyle.Primary:
                case StudioButtonStyle.Play:
                    bg = hovered ? StudioColours.ACCENT_HOVER : StudioColours.ACCENT;
                    fg = StudioColours.ACCENT_TEXT;
                    break;

                case StudioButtonStyle.Ghost:
                case StudioButtonStyle.Icon:
                    bg = hovered ? StudioColours.RAISED : StudioColours.RAISED.Opacity(0);
                    fg = Active.Value ? StudioColours.ACCENT : hovered ? StudioColours.TEXT : StudioColours.MUTED;
                    break;

                default:
                    bg = hovered ? StudioColours.INPUT : StudioColours.RAISED;
                    fg = Active.Value ? StudioColours.ACCENT : StudioColours.TEXT;
                    BorderColour = hovered ? Colour4.FromHex("3a4256") : StudioColours.BORDER;
                    break;
            }

            background.Colour = bg;

            if (text != null) text.Colour = fg;
            if (icon != null) icon.Colour = fg;

            Alpha = Enabled.Value ? 1 : 0.45f;
        }
    }

    /// <summary>
    /// YAWNS: a small select (Hitsound Studio's dropdowns) that opens its options in a popover, so it is never cut off by a scrolled list.
    /// </summary>
    public partial class StudioSelect<T> : OsuClickableContainer, IHasPopover
    {
        public readonly Bindable<T> Current = new Bindable<T>();

        private readonly Func<T, string> labelFor;
        private readonly OsuSpriteText text;
        private readonly Box background;

        public Func<IEnumerable<T>> Items { get; set; } = Array.Empty<T>;

        public StudioSelect(Func<T, string> labelFor, float height = 26, float fontSize = 11)
        {
            this.labelFor = labelFor;

            Height = height;
            Masking = true;
            CornerRadius = 4;
            BorderThickness = 1;
            BorderColour = StudioColours.BORDER;

            Children = new Drawable[]
            {
                background = new Box { RelativeSizeAxes = Axes.Both, Colour = StudioColours.INPUT },
                text = new TruncatingSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    RelativeSizeAxes = Axes.X,
                    Padding = new MarginPadding { Left = 5, Right = 14 },
                    Font = OsuFont.Default.With(size: fontSize),
                    Colour = StudioColours.TEXT,
                },
                new SpriteIcon
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -4,
                    Size = new Vector2(7),
                    Icon = FontAwesome.Solid.ChevronDown,
                    Colour = StudioColours.MUTED,
                },
            };

            Action = this.ShowPopover;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Current.BindValueChanged(v => text.Text = labelFor(v.NewValue), true);
            Enabled.BindValueChanged(e => Alpha = e.NewValue ? 1 : 0.45f, true);
        }

        protected override bool OnHover(HoverEvent e)
        {
            background.Colour = StudioColours.RAISED;
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            background.Colour = StudioColours.INPUT;
            base.OnHoverLost(e);
        }

        public Popover GetPopover() => new OptionsPopover(this);

        private partial class OptionsPopover : OsuPopover
        {
            public OptionsPopover(StudioSelect<T> select)
                : base(false)
            {
                var flow = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding(4),
                };

                foreach (var item in select.Items())
                {
                    var option = item;
                    flow.Add(new OptionButton(select.labelFor(option), EqualityComparer<T>.Default.Equals(option, select.Current.Value))
                    {
                        Action = () =>
                        {
                            select.Current.Value = option;
                            this.HidePopover();
                        },
                    });
                }

                Child = flow;
            }
        }

        private partial class OptionButton : OsuClickableContainer
        {
            private readonly Box hover;

            public OptionButton(string label, bool selected)
            {
                AutoSizeAxes = Axes.X;
                Height = 26;
                Masking = true;
                CornerRadius = 4;

                Children = new Drawable[]
                {
                    hover = new Box { RelativeSizeAxes = Axes.Both, Colour = StudioColours.INPUT, Alpha = 0 },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Margin = new MarginPadding { Horizontal = 8 },
                        Text = label,
                        Font = OsuFont.Default.With(size: 12, weight: selected ? FontWeight.Bold : FontWeight.Regular),
                        Colour = selected ? StudioColours.ACCENT : StudioColours.TEXT,
                    },
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                hover.Alpha = 1;
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                hover.Alpha = 0;
                base.OnHoverLost(e);
            }
        }
    }

    /// <summary>
    /// YAWNS: Hitsound Studio's two-way switch (.segmented).
    /// </summary>
    public partial class StudioSegmented<T> : CompositeDrawable
    {
        public readonly Bindable<T> Current = new Bindable<T>();

        private readonly List<(T Value, StudioButton Button)> buttons = new List<(T, StudioButton)>();

        public StudioSegmented(params (T Value, string Label, string Tooltip)[] options)
        {
            AutoSizeAxes = Axes.Both;
            Masking = true;
            CornerRadius = 5;
            BorderThickness = 1;
            BorderColour = StudioColours.BORDER_SOFT;

            var flow = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(2),
                Padding = new MarginPadding(2),
            };

            foreach (var (value, label, tooltip) in options)
            {
                var button = new StudioButton(label, StudioButtonStyle.Ghost, 18, 10) { TooltipText = tooltip };
                button.Action = () => Current.Value = value;
                buttons.Add((value, button));
                flow.Add(button);
            }

            InternalChildren = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = StudioColours.BG },
                flow,
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Current.BindValueChanged(v =>
            {
                foreach (var (value, button) in buttons)
                    button.Active.Value = EqualityComparer<T>.Default.Equals(value, v.NewValue);
            }, true);
        }
    }

    /// <summary>
    /// YAWNS: a tiny number field (lane index and volume, volume percentages), bound to an integer and clamped.
    /// </summary>
    public partial class StudioNumberBox : OsuNumberBox, IHasTooltip
    {
        public readonly BindableInt Value = new BindableInt();

        public LocalisableString TooltipText { get; set; }

        public StudioNumberBox(float width, float height = 22)
        {
            Width = width;
            Height = height;
            CommitOnFocusLost = true;
            CornerRadius = 4;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Value.BindValueChanged(v => Text = v.NewValue.ToString(), true);
            OnCommit += (_, _) =>
            {
                if (int.TryParse(Text, out int parsed))
                    Value.Value = Math.Clamp(parsed, Value.MinValue, Value.MaxValue);

                Text = Value.Value.ToString();
            };
        }
    }
}
