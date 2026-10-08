// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osuTK;

namespace osu.Game.Screens.Edit.MappingTools.Hitsounds
{
    /// <summary>
    /// YAWNS: Hitsound Studio's Copier tab: exports the lanes, then copies this difficulty's hitsounds onto the checked difficulties with the Hitsound Copier and saves them.
    /// The copier and its options are the ones the Tools tab uses.
    /// </summary>
    public partial class HitsoundCopierView : CompositeDrawable
    {
        public readonly HitsoundCopier Copier = new HitsoundCopier();

        private readonly HitsoundsScreen screen;
        private readonly List<(BeatmapInfo Difficulty, BindableBool Checked)> targets = new List<(BeatmapInfo, BindableBool)>();
        private readonly BindableInt leniency = new BindableInt(5) { MinValue = 0, MaxValue = 25 };

        private FillFlowContainer targetList = null!;
        private FillFlowContainer log = null!;
        private Container logContainer = null!;
        private StudioButton runButton = null!;

        public HitsoundCopierView(HitsoundsScreen screen)
        {
            this.screen = screen;

            RelativeSizeAxes = Axes.Both;

            InternalChild = new OsuScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding { Horizontal = 16, Vertical = 32 },
                    Child = new FillFlowContainer
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Width = 860,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(20),
                        Children = new Drawable[]
                        {
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(8),
                                Children = new Drawable[]
                                {
                                    new OsuSpriteText { Text = "Hitsound copier", Font = OsuFont.Default.With(size: 20, weight: FontWeight.Bold), Colour = StudioColours.TEXT },
                                    new OsuTextFlowContainer(s =>
                                    {
                                        s.Font = OsuFont.Default.With(size: 12);
                                        s.Colour = StudioColours.MUTED;
                                    })
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Text = "Copies the studio hitsounds into your difficulties. The lanes are exported to this difficulty first. Slider velocity is left untouched.",
                                    },
                                }
                            },
                            new GridContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                ColumnDimensions = new[] { new Dimension(), new Dimension(GridSizeMode.Absolute, 24), new Dimension() },
                                RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                                Content = new[]
                                {
                                    new[] { createTargets(), Empty(), createOptions() },
                                }
                            },
                            logContainer = new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Masking = true,
                                CornerRadius = 6,
                                BorderThickness = 1,
                                BorderColour = StudioColours.BORDER_SOFT,
                                Alpha = 0,
                                Children = new Drawable[]
                                {
                                    new Box { RelativeSizeAxes = Axes.Both, Colour = StudioColours.PANEL },
                                    log = new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(4),
                                        Padding = new MarginPadding(12),
                                    },
                                }
                            },
                        }
                    },
                },
            };
        }

        private Drawable createTargets() => new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(8),
            Children = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Children = new Drawable[]
                    {
                        sectionLabel("Target difficulties"),
                        new FillFlowContainer
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Children = new Drawable[]
                            {
                                new StudioButton("All", StudioButtonStyle.Ghost, 20, 10) { Action = () => targets.ForEach(t => t.Checked.Value = true) },
                                new StudioButton("None", StudioButtonStyle.Ghost, 20, 10) { Action = () => targets.ForEach(t => t.Checked.Value = false) },
                            }
                        },
                    }
                },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Masking = true,
                    CornerRadius = 6,
                    BorderThickness = 1,
                    BorderColour = StudioColours.BORDER_SOFT,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = StudioColours.PANEL },
                        targetList = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(2),
                            Padding = new MarginPadding(8),
                        },
                    }
                },
            }
        };

        private Drawable createOptions() => new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(10),
            Children = new Drawable[]
            {
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(8),
                    Children = new Drawable[]
                    {
                        sectionLabel("Snap tolerance").With(d => d.Anchor = d.Origin = Anchor.CentreLeft),
                        new StudioNumberBox(56, 28)
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Value = { BindTarget = leniency },
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = "ms",
                            Font = OsuFont.Default.With(size: 11),
                            Colour = StudioColours.MUTED,
                        },
                    }
                },
                option("Copy sample sets and custom indices (off: only whistles, finishes and claps)", Copier.CopySampleSets),
                option("Copy volumes", Copier.CopyVolumes),
                option("Copy slider body hitsounds", Copier.CopySliderBodyHitsounds),
                option("Clear hitsounds on unmatched notes", Copier.OverwriteEverything),
                runButton = new StudioButton("Copy and save difficulties", StudioButtonStyle.Primary, 38, 12)
                {
                    Margin = new MarginPadding { Top = 8 },
                    Action = run,
                },
            }
        };

        private static Drawable sectionLabel(string text) => new OsuSpriteText
        {
            Text = text,
            Font = OsuFont.Default.With(size: 11, weight: FontWeight.Bold),
            Colour = StudioColours.MUTED,
        };

        private static Drawable option(string label, Bindable<bool> current) => new OsuCheckbox(nubOnRight: false)
        {
            RelativeSizeAxes = Axes.X,
            LabelText = label,
            Current = current,
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();

            leniency.Value = (int)Math.Round(Copier.TemporalLeniency.Value);
            leniency.BindValueChanged(l => Copier.TemporalLeniency.Value = l.NewValue);

            Refresh();
        }

        /// <summary>
        /// Lists the set's other difficulties (shown when the tab opens).
        /// </summary>
        public void Refresh()
        {
            var previous = targets.ToDictionary(t => t.Difficulty.ID, t => t.Checked.Value);

            targets.Clear();
            targetList.Clear();

            foreach (var difficulty in screen.OtherDifficulties)
            {
                var check = new BindableBool(previous.GetValueOrDefault(difficulty.ID, true));
                targets.Add((difficulty, check));
                targetList.Add(new OsuCheckbox(nubOnRight: false) { RelativeSizeAxes = Axes.X, LabelText = difficulty.DifficultyName, Current = check });
            }

            if (targets.Count == 0)
            {
                targetList.Add(new OsuSpriteText
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Text = "This set has no other difficulties.",
                    Font = OsuFont.Default.With(size: 11),
                    Colour = StudioColours.FAINT,
                    Margin = new MarginPadding { Vertical = 16 },
                });
            }

            runButton.Enabled.Value = screen.Editable;
        }

        private void run()
        {
            if (!screen.Editable)
                return;

            log.Clear();
            logLines.Clear();
            logContainer.Alpha = 1;
            addLog("Running hitsound copier…", StudioColours.MUTED);

            var chosen = targets.Where(t => t.Checked.Value).Select(t => t.Difficulty).ToList();

            if (chosen.Count == 0)
            {
                if (screen.HasUnexportedChanges.Value)
                    screen.Export();

                addLog("No target difficulties checked, only this difficulty was written.", StudioColours.MUTED);
                return;
            }

            int copied = screen.CopyTo(chosen, Copier);

            addLog($"Copied onto {copied} of {chosen.Count} difficulties ({string.Join(", ", chosen.Select(d => d.DifficultyName))}) and saved them.",
                copied == chosen.Count ? StudioColours.OK : StudioColours.DANGER);
        }

        private readonly List<string> logLines = new List<string>();

        private void addLog(string text, Colour4 colour)
        {
            logLines.Add(text);
            log.Add(new OsuTextFlowContainer(s =>
            {
                s.Font = OsuFont.Default.With(size: 11, fixedWidth: true);
                s.Colour = colour;
            })
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Text = text,
            });
        }

        /// <summary>
        /// The log lines (for tests).
        /// </summary>
        public IReadOnlyList<string> LogLines => logLines;
    }
}
