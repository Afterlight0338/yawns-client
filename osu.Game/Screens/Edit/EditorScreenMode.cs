// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Screens.Edit
{
    public enum EditorScreenMode
    {
        [LocalisableDescription(typeof(EditorStrings), nameof(EditorStrings.SetupScreen))]
        SongSetup,

        [LocalisableDescription(typeof(EditorStrings), nameof(EditorStrings.ComposeScreen))]
        Compose,

        [LocalisableDescription(typeof(EditorStrings), nameof(EditorStrings.DesignScreen))]
        Design,

        [LocalisableDescription(typeof(EditorStrings), nameof(EditorStrings.TimingScreen))]
        Timing,

        [LocalisableDescription(typeof(EditorStrings), nameof(EditorStrings.VerifyScreen))]
        Verify,

        // YAWNS: whole-map mapping tools (Hitsound Copier, Timing Copier, ...).
        [Description("tools")]
        Tools,

        // YAWNS: Hitsound Studio's lanes, on a hitsound difficulty.
        [Description("hitsounds")]
        Hitsounds,

        // YAWNS: explains every YAWNS feature, with diagrams.
        [Description("tutorial")]
        Tutorial,
    }
}
