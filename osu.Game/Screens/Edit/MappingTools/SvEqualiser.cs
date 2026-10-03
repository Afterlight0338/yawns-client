// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Screens.Edit.MappingTools
{
    /// <summary>
    /// YAWNS: SV equaliser. A slider's speed on screen is its velocity multiplier divided by the beat length at its time,
    /// so after a BPM change the same multiplier is a different speed. Keeping the speed of a slider at <c>bpmReference</c>
    /// on a slider at <c>bpm</c> takes SV2 = SV1 * BPM1 / BPM2.
    /// </summary>
    public static class SvEqualiser
    {
        public const double MIN_SV = 0.1;
        public const double MAX_SV = 10;

        /// <summary>
        /// The multiplier a slider at <paramref name="bpm"/> needs to move as fast as <paramref name="multiplier"/> does at <paramref name="bpmReference"/>.
        /// Null when that would be outside what a slider can have.
        /// </summary>
        public static double? Equalised(double multiplier, double bpmReference, double bpm)
        {
            if (bpm <= 0 || bpmReference <= 0)
                return null;

            double result = Math.Round(multiplier * bpmReference / bpm, 2);
            return result < MIN_SV || result > MAX_SV ? null : result;
        }
    }
}
