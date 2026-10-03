// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics;

namespace osu.Game.Screens.Edit.MappingTools.Hitsounds
{
    /// <summary>
    /// YAWNS: a row in the Hitsounds tab. Mute, solo and the volume for new hits only live in the editor session;
    /// the hits themselves are saved in the hitsound difficulty.
    /// </summary>
    public class HitsoundLane
    {
        private static readonly Colour4[] colours =
        {
            Colour4.FromHex("ff4081"), Colour4.FromHex("00e5ff"), Colour4.FromHex("ffc400"), Colour4.FromHex("76ff03"), Colour4.FromHex("e040fb"),
            Colour4.FromHex("ff6e40"), Colour4.FromHex("40c4ff"), Colour4.FromHex("b2ff59"), Colour4.FromHex("ffd740"), Colour4.FromHex("69f0ae"),
        };

        public readonly HitsoundSound Sound;

        public readonly Colour4 Colour;

        public readonly BindableBool Muted = new BindableBool();

        public readonly BindableBool Solo = new BindableBool();

        /// <summary>
        /// Volume of hits added on this lane.
        /// </summary>
        public readonly BindableInt Volume = new BindableInt(80)
        {
            MinValue = 5,
            MaxValue = 100,
        };

        public HitsoundLane(HitsoundSound sound, int index)
        {
            Sound = sound;
            Colour = colours[index % colours.Length];
        }
    }
}
