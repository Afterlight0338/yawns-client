// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osuTK;

namespace osu.Game.Screens.Edit.MappingTools
{
    /// <summary>
    /// YAWNS: saves the selected objects to the pattern gallery.
    /// </summary>
    public partial class SavePatternPopover : OsuPopover
    {
        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        private readonly Bindable<string> name = new Bindable<string>(string.Empty);

        private PatternGallery gallery = null!;
        private OsuSpriteText result = null!;

        public SavePatternPopover()
        {
            AllowableAnchors = new[] { Anchor.CentreLeft, Anchor.CentreRight };
        }

        [BackgroundDependencyLoader]
        private void load(Storage storage)
        {
            gallery = new PatternGallery(storage);

            FormTextBox nameBox;

            Child = new FillFlowContainer
            {
                Width = 280,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(5),
                Children = new Drawable[]
                {
                    nameBox = new FormTextBox
                    {
                        Caption = "Pattern name",
                        HintText = "Saving under an existing name replaces that pattern.",
                        Current = name,
                        TabbableContentContainer = this,
                    },
                    new RoundedButton
                    {
                        RelativeSizeAxes = Axes.X,
                        Text = "Save to the pattern gallery",
                        Action = save,
                    },
                    result = new OsuSpriteText(),
                }
            };

            nameBox.OnCommit += (_, _) => save();
        }

        private void save()
        {
            if (editorBeatmap.SelectedHitObjects.Count == 0)
            {
                result.Text = "Select some objects first.";
                return;
            }

            string saved = gallery.Save(name.Value, editorBeatmap.SelectedHitObjects.ToList(), editorBeatmap);
            result.Text = $"Saved as \"{saved}\".";
        }
    }
}
