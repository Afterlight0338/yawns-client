// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Screens.Edit.MappingTools
{
    /// <summary>
    /// YAWNS: port of Mapping Tools' Combo Colour Studio ("colour haxing").
    /// Colour points work like timing points for combo colours: from a point on, combos cycle through its sequence of colours.
    /// A burst point colours only the one short combo it sits on, then the sequence before it carries on.
    /// Lazer keeps the colour skips on each object (combo offset), which is what applying writes.
    /// </summary>
    public static class ComboColourStudio
    {
        /// <summary>
        /// From <c>Time</c> on, combos cycle through <c>Sequence</c> (0-based indices into the combo colours as listed in setup).
        /// A <c>Burst</c> point is only for the combo starting at its time, if that combo is no longer than the burst length.
        /// </summary>
        public record ColourPoint(double Time, int[] Sequence, bool Burst = false);

        /// <summary>
        /// Objects that start a coloured combo, in order (spinners never take a colour).
        /// </summary>
        public static List<HitObject> ComboStarts(IBeatmap beatmap)
            => beatmap.HitObjects.Where(h => h is IHasComboInformation c && c.IndexInCurrentCombo == 0 && !isSpinner(h)).ToList();

        /// <summary>
        /// The 0-based combo colour a combo-starting object currently shows.
        /// </summary>
        public static int ColourOf(HitObject comboStart, int colourCount) => mod(((IHasComboInformation)comboStart).ComboIndexWithOffsets - 1, colourCount);

        /// <summary>
        /// Sets the colour skips of every combo so the combos follow the points.
        /// Combos with at most <c>maxBurstLength</c> objects can take a burst point.
        /// </summary>
        /// <returns>How many objects changed.</returns>
        public static int Apply(EditorBeatmap beatmap, IReadOnlyList<ColourPoint> points, int colourCount, int maxBurstLength = 1)
        {
            if (points.Count == 0 || colourCount < 1)
                return 0;

            var ordered = points.OrderBy(p => p.Time).ToList();
            var exceptions = new List<ColourPoint>();
            var starts = ComboStarts(beatmap);
            var lengths = comboLengths(beatmap);

            ColourPoint lastPoint = ordered[0];
            int lastPointColourIndex = -1;
            int lastColour = -1;
            int changed = 0;

            beatmap.BeginChange();

            foreach (var start in starts)
            {
                var point = colourPointAt(ordered, start.StartTime, exceptions, lengths.GetValueOrDefault(start, 1) <= maxBurstLength);
                int[] sequence = point.Sequence;

                if (point.Burst)
                    exceptions.Add(point);

                if (lastPointColourIndex != -1 && !ReferenceEquals(lastPoint, point))
                    lastPointColourIndex = Array.IndexOf(sequence, lastColour);

                // Moving to another point starts its sequence again, or at its second colour when the first was just shown.
                int pointColourIndex = lastPointColourIndex == -1 || sequence.Length == 0 ? 0
                    : ReferenceEquals(lastPoint, point) ? mod(lastPointColourIndex + 1, sequence.Length)
                    : lastPointColourIndex == 0 && sequence.Length > 1 ? 1 : 0;

                int colour = sequence.Length == 0 ? mod(lastColour + 1, colourCount) : mod(sequence[pointColourIndex], colourCount);

                // Each combo start moves one colour on by itself, the offset skips the rest of the way.
                int offset = mod(colour - lastColour - 1, colourCount);
                var combo = (IHasComboInformation)start;

                if (combo.ComboOffset != offset || (offset != 0 && !combo.NewCombo))
                {
                    combo.ComboOffset = offset;
                    if (offset != 0)
                        combo.NewCombo = true;

                    beatmap.Update(start);
                    changed++;
                }

                lastPointColourIndex = pointColourIndex;
                lastPoint = point;
                lastColour = colour;
            }

            beatmap.EndChange();
            return changed;
        }

        private static ColourPoint colourPointAt(List<ColourPoint> ordered, double time, List<ColourPoint> exceptions, bool includeBurst)
        {
            var candidates = ordered.Except(exceptions).ToList();

            return candidates.LastOrDefault(p => p.Time <= time + 5 && (!p.Burst || (p.Time >= time - 5 && includeBurst)))
                   ?? candidates.FirstOrDefault(p => !p.Burst)
                   ?? ordered[0];
        }

        /// <summary>
        /// Reads colour points back from a map's current colours (Mapping Tools' "import colour hax"), using as few points as it can.
        /// </summary>
        public static List<ColourPoint> Read(IBeatmap beatmap, int colourCount, int maxBurstLength = 1)
        {
            var points = new List<ColourPoint>();

            if (colourCount < 1)
                return points;

            var starts = ComboStarts(beatmap);
            int[] colours = starts.Select(s => ColourOf(s, colourCount)).ToArray();
            var comboLength = comboLengths(beatmap);
            int[] lengths = starts.Select(s => comboLength.GetValueOrDefault(s, 1)).ToArray();
            int[] sequenceLengths = Enumerable.Range(1, colourCount * 2 + 2).ToArray();

            int index = 0;
            int[]? lastNormal = null;
            bool lastBurst = false;

            while (index < starts.Count)
            {
                var best = bestSequenceAt(index, 3, colours, lengths, sequenceLengths, lastBurst, lastNormal, maxBurstLength);

                if (best == null)
                {
                    lastBurst = false;
                    index++;
                    continue;
                }

                int[] sequence = best.Value.Sequence;
                int contribution = contributionOf(colours, index, sequence);
                bool burst = contribution == 1 && lengths[index] <= maxBurstLength;

                // After a burst the sequence before it may simply carry on, no point needed.
                if (!(lastBurst && lastNormal != null && isPrefix(sequence, lastNormal) && (sequence.Length == lastNormal.Length || contribution <= sequence.Length)))
                    points.Add(new ColourPoint(starts[index].StartTime, sequence, burst));

                lastBurst = burst;
                index += contribution;
                lastNormal = burst ? lastNormal : sequence;
            }

            return points;
        }

        private static (int[] Sequence, int Contribution, double Cost)? bestSequenceAt(int start, int depth, int[] colours, int[] lengths, int[] sequenceLengths, bool lastBurst, int[]? lastNormal,
                                                                                         int maxBurstLength)
        {
            if (start >= colours.Length)
                return null;

            (int[] Sequence, int Contribution, double Cost)? best = null;
            double bestScore = double.NegativeInfinity;

            foreach (int n in sequenceLengths)
            {
                if (start + n > colours.Length)
                    continue;

                int[] sequence = colours.Skip(start).Take(n).ToArray();
                int contribution = contributionOf(colours, start, sequence);
                bool burst = contribution == 1 && lengths[start] <= maxBurstLength;
                double cost = lastBurst && lastNormal != null && isPrefix(sequence, lastNormal) && (sequence.Length == lastNormal.Length || contribution <= sequence.Length) ? 0 : sequence.Length;

                double totalContribution = contribution;

                if (depth > 0 && bestSequenceAt(start + contribution, depth - 1, colours, lengths, sequenceLengths, burst, burst ? lastNormal : sequence, maxBurstLength) is var (_, nextContribution, nextCost))
                {
                    totalContribution += nextContribution / 2;
                    cost += nextCost / 2;
                }

                double score = totalContribution / cost;

                if (best != null && (score < bestScore || (Math.Abs(score - bestScore) < 1e-9 && cost >= best.Value.Cost)))
                    continue;

                bestScore = score;
                best = (sequence, (int)totalContribution, cost);

                if (double.IsPositiveInfinity(bestScore))
                    break;
            }

            return best;
        }

        private static int contributionOf(int[] colours, int start, int[] sequence)
        {
            int count = 0;

            for (int i = start, s = 0; i < colours.Length && colours[i] == sequence[s]; i++, s = (s + 1) % sequence.Length)
                count++;

            return count;
        }

        private static bool isPrefix(int[] sequence, int[] bigger) => sequence.Length <= bigger.Length && sequence.Select((c, i) => c == bigger[i]).All(x => x);

        // Objects per combo, keyed by the combo's first object.
        private static Dictionary<HitObject, int> comboLengths(IBeatmap beatmap)
        {
            var lengths = new Dictionary<HitObject, int>();
            HitObject? current = null;

            foreach (var h in beatmap.HitObjects)
            {
                if (h is IHasComboInformation { IndexInCurrentCombo: 0 } || current == null)
                    lengths[current = h] = 0;

                lengths[current]++;
            }

            return lengths;
        }

        private static bool isSpinner(HitObject h) => h is IHasDuration && h is not IHasPath;

        private static int mod(int x, int m) => ((x % m) + m) % m;
    }
}
