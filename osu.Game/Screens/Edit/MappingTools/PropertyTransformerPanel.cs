// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osuTK;

namespace osu.Game.Screens.Edit.MappingTools
{
    /// <summary>
    /// YAWNS: the Property Transformer in the Tools tab.
    /// </summary>
    public partial class PropertyTransformerPanel : FillFlowContainer
    {
        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        private readonly List<(string Name, Bindable<string> Multiplier, Bindable<string> Offset)> rows = new List<(string, Bindable<string>, Bindable<string>)>();

        private readonly Bindable<string> minTime = new Bindable<string>(string.Empty);
        private readonly Bindable<string> maxTime = new Bindable<string>(string.Empty);
        private readonly Bindable<string> onlyValues = new Bindable<string>(string.Empty);
        private readonly Bindable<string> exceptValues = new Bindable<string>(string.Empty);
        private readonly BindableBool clip = new BindableBool(true);

        private OsuSpriteText result = null!;

        private static readonly string[] properties =
        {
            "Timing point time", "BPM", "Slider velocity", "Hitsound volume", "Hitsound index", "Object time", "Bookmark time", "Break time", "Preview time",
        };

        [BackgroundDependencyLoader]
        private void load()
        {
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(5);

            Add(new OsuTextFlowContainer(s => s.Font = OsuFont.Default.With(size: 13))
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Text = "New value = old value × multiplier + offset (multiplier first). Leave 1 and 0 to keep a property as it is.",
            });

            foreach (string name in properties)
            {
                var row = (name, new Bindable<string>("1"), new Bindable<string>("0"));
                rows.Add(row);

                Add(new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(5),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText { Text = name, Width = 140, Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft },
                        new FormNumberBox(allowDecimals: true) { Caption = "×", Width = 145, Current = row.Item2 },
                        new FormNumberBox(allowDecimals: true) { Caption = "+", Width = 145, Current = row.Item3 },
                    }
                });
            }

            AddRange(new Drawable[]
            {
                new FormNumberBox(allowDecimals: true) { Caption = "Only from time (ms)", HintText = "Empty for the start of the map.", RelativeSizeAxes = Axes.X, Current = minTime },
                new FormNumberBox(allowDecimals: true) { Caption = "Only until time (ms)", HintText = "Empty for the end of the map.", RelativeSizeAxes = Axes.X, Current = maxTime },
                new FormTextBox { Caption = "Only these values", HintText = "Comma separated, for example 60, 70. Empty for all values.", RelativeSizeAxes = Axes.X, Current = onlyValues },
                new FormTextBox { Caption = "Except these values", HintText = "Comma separated.", RelativeSizeAxes = Axes.X, Current = exceptValues },
                new FormCheckBox { Caption = "Keep results in each property's valid range", Current = clip },
                new RoundedButton { RelativeSizeAxes = Axes.X, Text = "Transform this difficulty", Action = apply },
                result = new OsuSpriteText(),
            });
        }

        private void apply()
        {
            try
            {
                PropertyTransformer.Transform t(int i) => new PropertyTransformer.Transform(parse(rows[i].Multiplier.Value, 1), parse(rows[i].Offset.Value, 0));

                var transformer = new PropertyTransformer
                {
                    TimingPointTime = t(0),
                    Bpm = t(1),
                    SliderVelocity = t(2),
                    HitsoundVolume = t(3),
                    HitsoundIndex = t(4),
                    ObjectTime = t(5),
                    BookmarkTime = t(6),
                    BreakTime = t(7),
                    PreviewTime = t(8),
                    Clip = clip.Value,
                    MinTime = string.IsNullOrWhiteSpace(minTime.Value) ? null : parse(minTime.Value, 0),
                    MaxTime = string.IsNullOrWhiteSpace(maxTime.Value) ? null : parse(maxTime.Value, 0),
                    OnlyValues = list(onlyValues.Value),
                    ExceptValues = list(exceptValues.Value),
                };

                transformer.Apply(editorBeatmap);
                result.Text = "Done. Undo reverts it.";
            }
            catch (FormatException e)
            {
                result.Text = e.Message;
            }
        }

        private static double parse(string text, double empty)
        {
            if (string.IsNullOrWhiteSpace(text))
                return empty;

            if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                throw new FormatException($"\"{text}\" is not a number.");

            return value;
        }

        private static double[] list(string text) =>
            text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(v => parse(v, 0)).ToArray();
    }
}
