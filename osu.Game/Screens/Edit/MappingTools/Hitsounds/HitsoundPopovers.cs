// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osuTK;

namespace osu.Game.Screens.Edit.MappingTools.Hitsounds
{
    /// <summary>
    /// YAWNS: adds a lane for a skin sample (bank, sample, custom index) or a custom sample file.
    /// </summary>
    public partial class AddLanePopover : OsuPopover
    {
        private readonly HitsoundsScreen screen;

        private readonly Bindable<string> bank = new Bindable<string>(HitSampleInfo.BANK_SOFT);
        private readonly Bindable<string> sample = new Bindable<string>(HitSampleInfo.HIT_CLAP);
        private readonly BindableInt index = new BindableInt { MinValue = 0, MaxValue = 30 };
        private readonly Bindable<string> file = new Bindable<string>(string.Empty);

        public AddLanePopover(HitsoundsScreen screen)
        {
            this.screen = screen;
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
                    new FormDropdown<string>
                    {
                        Caption = "Bank",
                        Items = HitSampleInfo.ALL_BANKS,
                        Current = bank,
                    },
                    new FormDropdown<string>
                    {
                        Caption = "Sample",
                        Items = new[] { HitSampleInfo.HIT_NORMAL }.Concat(HitSampleInfo.ALL_ADDITIONS).ToArray(),
                        Current = sample,
                    },
                    new FormSliderBar<int>
                    {
                        Caption = "Custom index",
                        HintText = "0 plays the skin's sample, 1 the map's soft-hitclap.wav, 2 and up soft-hitclap2.wav and so on.",
                        Current = index,
                        TabbableContentContainer = this,
                    },
                    new FormTextBox
                    {
                        Caption = "Or a custom sample file",
                        HintText = "A file in the beatmap folder, such as kick.wav. Leave empty to use the bank and sample above.",
                        Current = file,
                        TabbableContentContainer = this,
                    },
                    new RoundedButton
                    {
                        RelativeSizeAxes = Axes.X,
                        Text = "Add lane",
                        Action = () =>
                        {
                            string name = file.Value.Trim();
                            screen.AddLane(name.Length > 0
                                ? new HitsoundSound(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_NORMAL, File: name)
                                : new HitsoundSound(sample.Value, bank.Value, index.Value));
                            Hide();
                        },
                    },
                }
            };
        }
    }

    /// <summary>
    /// YAWNS: lists the set's other difficulties, to import hitsounds from one or copy them onto several.
    /// </summary>
    public partial class DifficultyListPopover : OsuPopover
    {
        public enum Purpose
        {
            Import,
            CopyTo,
        }

        private readonly HitsoundsScreen screen;
        private readonly Purpose purpose;

        private readonly HitsoundCopier copier = new HitsoundCopier();
        private readonly Dictionary<BeatmapInfo, BindableBool> chosen = new Dictionary<BeatmapInfo, BindableBool>();

        private OsuSpriteText result = null!;

        public DifficultyListPopover(HitsoundsScreen screen, Purpose purpose)
        {
            this.screen = screen;
            this.purpose = purpose;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var flow = new FillFlowContainer
            {
                Width = 320,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(5),
            };

            var difficulties = screen.OtherDifficulties.ToList();

            if (difficulties.Count == 0)
                flow.Add(new OsuSpriteText { Text = "This set has no other difficulties." });

            if (purpose == Purpose.Import)
            {
                if (!screen.Editable)
                    flow.Add(new OsuSpriteText { Text = "Create a hitsound difficulty first." });
                else
                {
                    foreach (var difficulty in difficulties)
                    {
                        flow.Add(new RoundedButton
                        {
                            RelativeSizeAxes = Axes.X,
                            Text = difficulty.DifficultyName,
                            Action = () => result.Text = $"Imported {screen.ImportFrom(difficulty)} hits from {difficulty.DifficultyName}. Undo reverts it.",
                        });
                    }
                }
            }
            else
            {
                flow.Add(new FormSliderBar<double>
                {
                    Caption = "Temporal leniency (ms)",
                    HintText = "How far apart a hit and an object can be and still count as the same moment.",
                    Current = copier.TemporalLeniency,
                    TabbableContentContainer = this,
                });

                foreach (var difficulty in difficulties)
                {
                    var check = chosen[difficulty] = new BindableBool(true);
                    flow.Add(new FormCheckBox { Caption = difficulty.DifficultyName, Current = check });
                }

                flow.Add(new RoundedButton
                {
                    RelativeSizeAxes = Axes.X,
                    Text = "Copy hitsounds and save those difficulties",
                    Action = () =>
                    {
                        var targets = chosen.Where(c => c.Value.Value).Select(c => c.Key).ToList();
                        result.Text = $"Copied onto {screen.CopyTo(targets, copier)} of {targets.Count} difficulties.";
                    },
                });
            }

            flow.Add(result = new OsuSpriteText());
            Child = flow;
        }
    }

    /// <summary>
    /// YAWNS: which map's objects are drawn as ghost notes behind the lanes (as Hitsound Studio's ghost notes).
    /// </summary>
    public record GhostChoice(string Name, BeatmapInfo? Difficulty)
    {
        public static readonly GhostChoice NONE = new GhostChoice("None", null);
        public static readonly GhostChoice OVERLAY = new GhostChoice("Overlay map", null);

        public override string ToString() => Name;
    }

    /// <summary>
    /// YAWNS: picks the ghost notes: none, the overlay map, or another difficulty of this set.
    /// </summary>
    public partial class GhostPopover : OsuPopover
    {
        private readonly HitsoundsScreen screen;

        public GhostPopover(HitsoundsScreen screen)
        {
            this.screen = screen;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var flow = new FillFlowContainer
            {
                Width = 320,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(5),
            };

            foreach (var choice in screen.GhostChoices)
            {
                flow.Add(new RoundedButton
                {
                    RelativeSizeAxes = Axes.X,
                    Text = choice.Name,
                    Action = () =>
                    {
                        screen.Ghost.Value = choice;
                        this.HidePopover();
                    },
                });
            }

            Child = flow;
        }
    }
}
