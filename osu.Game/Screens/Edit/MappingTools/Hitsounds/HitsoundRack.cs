// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Audio;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osuTK;

namespace osu.Game.Screens.Edit.MappingTools.Hitsounds
{
    /// <summary>
    /// YAWNS: Hitsound Studio's channel rack: the header (lanes, compact, add lane, difficulty, Hitsounds / Ghost only, ghost toggle)
    /// and one row per lane, scrolled in step with the canvas.
    /// </summary>
    public partial class HitsoundRack : CompositeDrawable
    {
        public const float WIDTH = 345;

        private readonly HitsoundsScreen screen;
        private readonly HitsoundCanvas canvas;

        private OsuSpriteText title = null!;
        private StudioButton compactButton = null!;
        private Container<LaneItem> items = null!;

        public HitsoundRack(HitsoundsScreen screen, HitsoundCanvas canvas)
        {
            this.screen = screen;
            this.canvas = canvas;

            Width = WIDTH;
            RelativeSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = StudioColours.PANEL },
                new Box { RelativeSizeAxes = Axes.Y, Width = 1, Anchor = Anchor.TopRight, Origin = Anchor.TopRight, Colour = StudioColours.BORDER },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = HitsoundCanvas.RULER_HEIGHT,
                    Padding = new MarginPadding { Horizontal = 10 },
                    Children = new Drawable[]
                    {
                        // Top line: title and lane actions.
                        new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 24,
                            Y = 7,
                            Children = new Drawable[]
                            {
                                title = new OsuSpriteText
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Font = OsuFont.Default.With(size: 10, weight: FontWeight.Bold),
                                    Colour = StudioColours.MUTED,
                                    Spacing = new Vector2(0.6f, 0),
                                },
                                new FillFlowContainer
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(4),
                                    Children = new Drawable[]
                                    {
                                        compactButton = new StudioButton("Compact", StudioButtonStyle.Ghost, 24, 10)
                                        {
                                            Action = screen.ToggleCompact,
                                            TooltipText = "Compact lanes to see more tracks",
                                        },
                                        new FillFlowContainer
                                        {
                                            AutoSizeAxes = Axes.Both,
                                            Direction = FillDirection.Horizontal,
                                            Children = new Drawable[]
                                            {
                                                new StudioButton("+ Lane", StudioButtonStyle.Normal, 24, 10) { Action = () => screen.AddLane() },
                                                new AddLaneMenuButton(screen),
                                            }
                                        },
                                    }
                                },
                            }
                        },
                        // Sub line: difficulty, what picking one does, ghost notes.
                        new GridContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 24,
                            Y = 35,
                            ColumnDimensions = new[]
                            {
                                new Dimension(),
                                new Dimension(GridSizeMode.AutoSize),
                                new Dimension(GridSizeMode.AutoSize),
                            },
                            Content = new[]
                            {
                                new Drawable[]
                                {
                                    new StudioSelect<GhostChoice>(c => c.Name, 24)
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Items = () => screen.GhostChoices,
                                        Current = { BindTarget = screen.Ghost },
                                        TooltipText = "Difficulty",
                                    },
                                    new StudioSegmented<DiffMode>(
                                        (DiffMode.Hitsounds, "Hitsounds", "Picking a difficulty loads its hitsounds into the lanes and shows its notes as ghosts"),
                                        (DiffMode.GhostOnly, "Ghost only", "Picking a difficulty only changes the ghost notes; lanes stay as they are"))
                                    {
                                        Margin = new MarginPadding { Left = 6 },
                                        Current = { BindTarget = screen.Mode },
                                    },
                                    new GhostToggle(canvas) { Margin = new MarginPadding { Left = 4 } },
                                }
                            }
                        },
                        new Box
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 1,
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            Colour = StudioColours.BORDER,
                            Margin = new MarginPadding { Horizontal = -10 },
                        },
                    }
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Top = HitsoundCanvas.RULER_HEIGHT, Bottom = HitsoundCanvas.SCROLLBAR_HEIGHT, Right = 1 },
                    Child = new LaneList(canvas)
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        Child = items = new Container<LaneItem> { RelativeSizeAxes = Axes.Both },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            screen.LanesChanged += rebuild;
            screen.Compact.BindValueChanged(c =>
            {
                compactButton.Text = c.NewValue ? "Expand" : "Compact";
                compactButton.TooltipText = c.NewValue ? "Expand lanes to show all controls" : "Compact lanes to see more tracks";
                rebuild();
            });

            rebuild();
        }

        private void rebuild()
        {
            title.Text = $"LANES · {screen.Lanes.Count}";

            // Keep rows for lanes that are still there (a text box being typed in keeps its focus).
            var existing = items.ToDictionary(i => i.Lane);
            var wanted = screen.Lanes.ToList();

            foreach (var item in items.ToList())
            {
                if (!wanted.Contains(item.Lane) || item.Compact != screen.Compact.Value)
                {
                    items.Remove(item, true);
                    existing.Remove(item.Lane);
                }
            }

            foreach (var lane in wanted)
            {
                if (!existing.ContainsKey(lane))
                    items.Add(new LaneItem(screen, lane, screen.Compact.Value));
            }

            foreach (var item in items)
                item.Refresh();
        }

        protected override void Update()
        {
            base.Update();

            float height = canvas.LaneHeight;

            foreach (var item in items)
            {
                item.Height = height;
                item.Y = screen.Lanes.IndexOf(item.Lane) * height - canvas.ScrollTop.Value;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            screen.LanesChanged -= rebuild;
        }

        /// <summary>
        /// The wheel over the lane list scrolls the lanes.
        /// </summary>
        private partial class LaneList : Container
        {
            private readonly HitsoundCanvas canvas;

            public LaneList(HitsoundCanvas canvas)
            {
                this.canvas = canvas;
            }

            protected override bool OnScroll(ScrollEvent e)
            {
                canvas.ScrollLanesBy(-e.ScrollDelta.Y * 100);
                return true;
            }
        }

        private partial class GhostToggle : StudioButton
        {
            private readonly HitsoundCanvas canvas;

            public GhostToggle(HitsoundCanvas canvas)
                : base(FontAwesome.Solid.Eye, StudioButtonStyle.Icon, 24)
            {
                this.canvas = canvas;
                TooltipText = "Show ghost notes (G)";
                Action = canvas.ShowGhostNotes.Toggle;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                canvas.ShowGhostNotes.BindValueChanged(v => Active.Value = v.NewValue, true);
            }
        }

        private partial class AddLaneMenuButton : OsuClickableContainer, IHasPopover
        {
            private readonly HitsoundsScreen screen;

            public AddLaneMenuButton(HitsoundsScreen screen)
            {
                this.screen = screen;

                Size = new Vector2(18, 24);
                Masking = true;
                CornerRadius = 6;
                BorderThickness = 1;
                BorderColour = StudioColours.BORDER;
                TooltipText = "Add a specific addition lane";

                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = StudioColours.RAISED },
                    new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(7),
                        Icon = FontAwesome.Solid.ChevronDown,
                        Colour = StudioColours.TEXT,
                    },
                };

                Action = this.ShowPopover;
            }

            public Popover GetPopover() => new AddLanePopover(screen);
        }

        private partial class AddLanePopover : OsuPopover
        {
            public AddLanePopover(HitsoundsScreen screen)
                : base(false)
            {
                var flow = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding(4),
                };

                foreach (var (addition, label) in new[]
                         {
                             (HitsoundAddition.None, "Soft hitnormal"),
                             (HitsoundAddition.Whistle, "Soft whistle"),
                             (HitsoundAddition.Finish, "Soft finish"),
                             (HitsoundAddition.Clap, "Soft clap"),
                         })
                {
                    flow.Add(new StudioButton(label, StudioButtonStyle.Ghost, 26, 12)
                    {
                        Action = () =>
                        {
                            screen.AddLaneWithAddition(addition);
                            this.HidePopover();
                        },
                    });
                }

                Child = flow;
            }
        }

        /// <summary>
        /// One lane: name, custom sample, mute, solo, preview, delete, and (unless compact) sample set, addition, custom index and volume.
        /// </summary>
        private partial class LaneItem : CompositeDrawable
        {
            public readonly HitsoundLane Lane;
            public readonly bool Compact;

            private readonly HitsoundsScreen screen;

            private Box background = null!;
            private Box led = null!;
            private Box border = null!;
            private OsuTextBox nameBox = null!;
            private BadgeButton badge = null!;
            private LaneToggle mute = null!;
            private LaneToggle solo = null!;
            private StudioSelect<string> bankSelect = null!;
            private StudioSelect<HitsoundAddition> additionSelect = null!;
            private StudioNumberBox indexBox = null!;
            private StudioNumberBox volumeBox = null!;

            private bool playing;

            public LaneItem(HitsoundsScreen screen, HitsoundLane lane, bool compact)
            {
                this.screen = screen;
                Lane = lane;
                Compact = compact;

                RelativeSizeAxes = Axes.X;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                float buttonSize = Compact ? 18 : 20;

                var topRow = new GridContainer
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 20,
                    ColumnDimensions = new[]
                    {
                        new Dimension(GridSizeMode.Absolute, 10),
                        new Dimension(),
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension(GridSizeMode.AutoSize),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new CircularContainer
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Size = new Vector2(6),
                                Masking = true,
                                Child = led = new Box { RelativeSizeAxes = Axes.Both, Colour = StudioColours.BORDER },
                            },
                            nameBox = new LaneNameBox
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 20,
                                FontSize = 11,
                                Text = Lane.Name,
                                CommitOnFocusLost = true,
                            },
                            badge = new BadgeButton
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Margin = new MarginPadding { Horizontal = 4 },
                                Action = () => screen.ClearLaneSample(Lane),
                            },
                            new FillFlowContainer
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(2),
                                Children = new Drawable[]
                                {
                                    mute = new LaneToggle("M", StudioColours.DANGER, Colour4.FromHex("1a0a0a"), buttonSize)
                                    {
                                        TooltipText = "Mute lane",
                                        Action = () => screen.ToggleMute(Lane),
                                    },
                                    solo = new LaneToggle("S", StudioColours.WARN, Colour4.FromHex("1a1205"), buttonSize)
                                    {
                                        TooltipText = "Solo lane",
                                        Action = () => screen.ToggleSolo(Lane),
                                    },
                                    new LaneIconButton(FontAwesome.Solid.VolumeUp, buttonSize)
                                    {
                                        TooltipText = "Preview sample",
                                        Action = () => screen.PreviewLane(Lane, true),
                                    },
                                    new LaneIconButton(FontAwesome.Solid.Times, buttonSize)
                                    {
                                        TooltipText = "Delete lane",
                                        Action = () => screen.RemoveLane(Lane),
                                    },
                                }
                            },
                        }
                    }
                };

                Drawable content = topRow;

                if (!Compact)
                {
                    content = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(5),
                        Children = new Drawable[]
                        {
                            topRow,
                            new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(4),
                                Children = new Drawable[]
                                {
                                    bankSelect = new StudioSelect<string>(HitsoundProject.BankName, 22, 10)
                                    {
                                        Width = 64,
                                        Items = () => HitsoundProject.BANKS,
                                        TooltipText = "SampleSet",
                                    },
                                    additionSelect = new StudioSelect<HitsoundAddition>(a => a.ToString(), 22, 10)
                                    {
                                        Width = 70,
                                        Items = () => new[] { HitsoundAddition.None, HitsoundAddition.Clap, HitsoundAddition.Whistle, HitsoundAddition.Finish },
                                        TooltipText = "Addition",
                                    },
                                    controlLabel("#"),
                                    indexBox = new StudioNumberBox(28)
                                    {
                                        Value = { MinValue = 0, MaxValue = 99 },
                                        TooltipText = "Custom sample index (e.g. 1 for soft-hitclap.wav, 2 for soft-hitclap2.wav)",
                                    },
                                    controlLabel("Vol"),
                                    volumeBox = new StudioNumberBox(36)
                                    {
                                        Value = { MinValue = 0, MaxValue = 100 },
                                        TooltipText = "Lane volume percentage",
                                    },
                                    controlLabel("%"),
                                }
                            },
                        }
                    };
                }

                InternalChildren = new Drawable[]
                {
                    background = new Box { RelativeSizeAxes = Axes.Both, Colour = StudioColours.PANEL },
                    border = new Box { RelativeSizeAxes = Axes.Y, Width = 3 },
                    new Box
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 1,
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        Colour = StudioColours.BORDER_SOFT,
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Left = 13, Right = 8 },
                        Child = new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Child = content,
                        },
                    },
                };
            }

            private static Drawable controlLabel(string text) => new OsuSpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Text = text,
                Font = OsuFont.Default.With(size: 10),
                Colour = StudioColours.FAINT,
            };

            protected override void LoadComplete()
            {
                base.LoadComplete();

                nameBox.OnCommit += (_, _) =>
                {
                    if (nameBox.Text != Lane.Name)
                        screen.EditLane(Lane, l => l.Name = nameBox.Text, false);
                };

                if (!Compact)
                {
                    bankSelect.Current.Value = Lane.Bank;
                    additionSelect.Current.Value = Lane.Addition;
                    indexBox.Value.Value = Lane.Index;
                    volumeBox.Value.Value = Lane.Volume;

                    bankSelect.Current.BindValueChanged(v => screen.EditLane(Lane, l => l.Bank = v.NewValue));
                    additionSelect.Current.BindValueChanged(v => screen.EditLane(Lane, l => l.Addition = v.NewValue));
                    indexBox.Value.BindValueChanged(v => screen.EditLane(Lane, l => l.Index = v.NewValue));
                    volumeBox.Value.BindValueChanged(v => screen.EditLane(Lane, l => l.Volume = v.NewValue));
                }

                Refresh();
            }

            /// <summary>
            /// Shows the lane's current state (after an edit, undo, mute or solo).
            /// </summary>
            public void Refresh()
            {
                if (!IsLoaded)
                    return;

                border.Colour = Colour4.TryParseHex(Lane.Colour, out var c) ? c : Colour4.HotPink;

                if (!nameBox.HasFocus)
                    nameBox.Text = Lane.Name;

                badge.Text = Lane.File ?? string.Empty;
                badge.Alpha = Lane.File != null ? 1 : 0;
                mute.Active.Value = Lane.Muted;
                solo.Active.Value = Lane.Solo;

                if (!Compact)
                {
                    bankSelect.Current.Value = Lane.Bank;
                    additionSelect.Current.Value = Lane.Addition;
                    indexBox.Value.Value = Lane.Index;
                    volumeBox.Value.Value = Lane.Volume;
                }

                bool hasSolo = screen.Lanes.Any(l => l.Solo);
                Alpha = Lane.Muted || (hasSolo && !Lane.Solo) ? 0.45f : 1;
            }

            protected override void Update()
            {
                base.Update();

                bool nowPlaying = screen.PlayingLaneIds.Contains(Lane.Id);

                if (nowPlaying != playing)
                {
                    playing = nowPlaying;
                    led.Colour = playing ? StudioColours.ACCENT : StudioColours.BORDER;
                    updateBackground();
                }
            }

            protected override bool OnHover(HoverEvent e)
            {
                updateBackground();
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                updateBackground();
                base.OnHoverLost(e);
            }

            private void updateBackground() =>
                background.Colour = playing ? StudioColours.RAISED : IsHovered ? Colour4.FromHex("171b24") : StudioColours.PANEL;
        }

        /// <summary>
        /// The lane under a screen position, for audio dropped onto the rack.
        /// </summary>
        public HitsoundLane? LaneAt(Vector2 screenSpace) => items.FirstOrDefault(i => i.IsPresent && i.Contains(screenSpace))?.Lane;

        private partial class LaneNameBox : OsuTextBox
        {
            public LaneNameBox()
            {
                LengthLimit = 64;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                BackgroundUnfocused = Colour4.Transparent;
                BackgroundFocused = StudioColours.INPUT;
                BorderColour = StudioColours.ACCENT;
                CornerRadius = 3;
            }

            protected override float LeftRightPadding => 3;
        }

        /// <summary>
        /// Mute (red when on) or solo (amber when on), as .btn-mute and .btn-solo.
        /// </summary>
        private partial class LaneToggle : OsuClickableContainer
        {
            public readonly osu.Framework.Bindables.BindableBool Active = new osu.Framework.Bindables.BindableBool();

            private readonly Colour4 onBackground;
            private readonly Colour4 onText;
            private readonly Box onBox;
            private readonly OsuSpriteText text;

            public LaneToggle(string label, Colour4 onBackground, Colour4 onText, float size)
            {
                this.onBackground = onBackground;
                this.onText = onText;

                Size = new Vector2(size);
                Masking = true;
                CornerRadius = 3;
                BorderThickness = 1;

                Children = new Drawable[]
                {
                    onBox = new Box { RelativeSizeAxes = Axes.Both, Colour = onBackground },
                    text = new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = label,
                        Font = OsuFont.Default.With(size: 9, weight: FontWeight.Bold),
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                Active.BindValueChanged(_ => updateState(), true);
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
                onBox.Alpha = Active.Value ? 1 : 0;
                BorderColour = Active.Value ? onBackground : IsHovered ? StudioColours.BORDER : StudioColours.BORDER_SOFT;
                text.Colour = Active.Value ? onText : IsHovered ? StudioColours.TEXT : StudioColours.MUTED;
            }
        }

        private partial class LaneIconButton : StudioButton
        {
            public LaneIconButton(IconUsage icon, float size)
                : base(icon, StudioButtonStyle.Icon, size)
            {
                CornerRadius = 3;
                BorderThickness = 1;
                BorderColour = StudioColours.BORDER_SOFT;
            }
        }

        private partial class BadgeButton : OsuClickableContainer
        {
            private readonly OsuSpriteText text;

            public BadgeButton()
            {
                AutoSizeAxes = Axes.Both;
                Masking = true;
                CornerRadius = 3;
                TooltipText = "Custom sample. Click to remove it and use standard hitsounds";

                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = StudioColours.INPUT },
                    text = new TruncatingSpriteText
                    {
                        MaxWidth = 110,
                        Font = OsuFont.Default.With(size: 9),
                        Colour = StudioColours.MUTED,
                        Margin = new MarginPadding { Horizontal = 5, Vertical = 1 },
                    },
                };
            }

            public string Text
            {
                set => text.Text = value.Length > 0 ? $"{value} ✕" : string.Empty;
            }

            protected override bool OnHover(HoverEvent e)
            {
                text.Colour = StudioColours.DANGER;
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                text.Colour = StudioColours.MUTED;
                base.OnHoverLost(e);
            }
        }
    }

    public enum DiffMode
    {
        Hitsounds,
        GhostOnly,
    }

    /// <summary>
    /// YAWNS: a choice of ghost notes: none, the overlay map, or another difficulty of this set.
    /// </summary>
    public record GhostChoice(string Name, osu.Game.Beatmaps.BeatmapInfo? Difficulty)
    {
        public static readonly GhostChoice NONE = new GhostChoice("No diff", null);
        public static readonly GhostChoice OVERLAY = new GhostChoice("Overlay map", null);
    }
}
