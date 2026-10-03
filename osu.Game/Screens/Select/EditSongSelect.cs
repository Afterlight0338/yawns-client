// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Footer;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// Song select for the mapping-only client. Selecting a beatmap opens it in the editor; gameplay cannot be started from here.
    /// </summary>
    public partial class EditSongSelect : SongSelect
    {
        protected override void OnStart() => this.Push(new EditorLoader());

        public override IEnumerable<OsuMenuItem> GetForwardActions(BeatmapInfo beatmap)
        {
            yield return new OsuMenuItem(ButtonSystemStrings.Edit.ToSentence(), MenuItemType.Highlighted, () => SelectAndRun(beatmap, OnStart)) { Icon = FontAwesome.Solid.PencilAlt };

            // The base "Select" item is replaced by "Edit" above.
            foreach (var item in base.GetForwardActions(beatmap).Skip(1))
                yield return item;
        }

        // Mods only matter for gameplay.
        public override IReadOnlyList<ScreenFooterButton> CreateFooterButtons() => base.CreateFooterButtons().Where(b => b is not FooterButtonMods).ToArray();
    }
}
