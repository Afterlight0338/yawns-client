// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// YAWNS: settings for <see cref="RadialCopy"/>. Adding the copies is one undo step, and they are selected afterwards.
    /// </summary>
    public partial class RadialCopyPopover : OsuPopover
    {
        private readonly RadialCopy radialCopy;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        private OsuSpriteText result = null!;

        public RadialCopyPopover(RadialCopy radialCopy)
        {
            this.radialCopy = radialCopy;
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
                    new FormSliderBar<int>
                    {
                        Caption = "Copies around the circle",
                        HintText = "Including the selection: 3 rotates by 120 degrees, 4 by 90, 5 by 72, 6 by 60...",
                        Current = radialCopy.Count,
                        TabbableContentContainer = this,
                    },
                    new FormEnumDropdown<RadialCopy.Centre>
                    {
                        Caption = "Rotate around",
                        Current = radialCopy.Around,
                    },
                    new FormSliderBar<double>
                    {
                        Caption = "Beats between copies",
                        Current = radialCopy.Beats,
                        TabbableContentContainer = this,
                    },
                    new RoundedButton
                    {
                        RelativeSizeAxes = Axes.X,
                        Text = "Add copies",
                        Action = apply,
                    },
                    result = new OsuSpriteText(),
                }
            };
        }

        private void apply()
        {
            var selection = editorBeatmap.SelectedHitObjects.OfType<OsuHitObject>().ToList();

            if (selection.Count == 0)
                return;

            double beatLength = editorBeatmap.ControlPointInfo.TimingPointAt(selection.Min(h => h.StartTime)).BeatLength;
            var copies = radialCopy.Create(selection, beatLength);

            editorBeatmap.BeginChange();
            editorBeatmap.AddRange(copies);
            editorBeatmap.SelectedHitObjects.Clear();
            editorBeatmap.SelectedHitObjects.AddRange(copies);
            editorBeatmap.EndChange();

            result.Text = $"Added {copies.Count} objects. Undo removes them.";
        }
    }
}
