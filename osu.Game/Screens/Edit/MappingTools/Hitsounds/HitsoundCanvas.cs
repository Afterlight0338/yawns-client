// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Utils;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Input;

namespace osu.Game.Screens.Edit.MappingTools.Hitsounds
{
    /// <summary>
    /// YAWNS: Hitsound Studio's sequencer canvas (sequencer.ts), drawn natively: ruler with waveform, lanes, grid, ghost notes, hits, overview scrollbar and playhead.
    /// Only what is in view is drawn, rebuilt every frame.
    /// </summary>
    public partial class HitsoundCanvas : CompositeDrawable
    {
        public const float RULER_HEIGHT = 64;
        public const float SCROLLBAR_HEIGHT = 14;
        public const float LANE_HEIGHT = 58;
        public const float COMPACT_LANE_HEIGHT = 28;

        public const float MIN_ZOOM = 20;
        public const float MAX_ZOOM = 4000;

        /// <summary>
        /// Horizontal zoom in pixels per second.
        /// </summary>
        public readonly BindableFloat Zoom = new BindableFloat(220) { MinValue = MIN_ZOOM, MaxValue = MAX_ZOOM };

        /// <summary>
        /// Vertical scroll of the lanes, in pixels. Shared with the lane rack.
        /// </summary>
        public readonly BindableFloat ScrollTop = new BindableFloat();

        public readonly BindableBool ShowGhostNotes = new BindableBool(true);

        public double ScrollLeftMs { get; set; }

        private readonly HitsoundsScreen screen;

        [Resolved]
        private EditorClock clock { get; set; } = null!;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        private readonly QuadLayer laneLayer;
        private readonly QuadLayer rulerLayer;
        private readonly Container<OsuSpriteText> rulerText;
        private readonly QuadLayer overlayLayer;
        private readonly Container laneMask;

        private double lastClockTime = double.NaN;
        private int usedLabels;

        public float LaneHeight => screen.Compact.Value ? COMPACT_LANE_HEIGHT : LANE_HEIGHT;

        private double durationMs => Math.Max(clock.TrackLength, 10000);

        private float width => DrawWidth;

        public HitsoundCanvas(HitsoundsScreen screen)
        {
            this.screen = screen;

            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                laneMask = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Y = RULER_HEIGHT,
                    Masking = true,
                    Child = laneLayer = new QuadLayer { RelativeSizeAxes = Axes.X, Y = -RULER_HEIGHT },
                },
                rulerLayer = new QuadLayer { RelativeSizeAxes = Axes.Both },
                rulerText = new Container<OsuSpriteText> { RelativeSizeAxes = Axes.Both },
                overlayLayer = new QuadLayer { RelativeSizeAxes = Axes.Both },
            };
        }

        #region Geometry (sequencer.ts)

        private float msToPx(double ms) => (float)((ms - ScrollLeftMs) / 1000 * Zoom.Value);

        private double pxToMs(float px) => ScrollLeftMs + px / Zoom.Value * 1000;

        private float trackAreaHeight => Math.Max(0, DrawHeight - RULER_HEIGHT - SCROLLBAR_HEIGHT);

        private float maxScrollTop => Math.Max(0, screen.Lanes.Count * LaneHeight - trackAreaHeight);

        public IReadOnlyList<TimingControlPoint> RedLines => editorBeatmap.ControlPointInfo.TimingPoints;

        public TimingControlPoint ActiveRedLine(double timeMs)
        {
            var redLines = RedLines;

            if (redLines.Count == 0)
                return new TimingControlPoint { BeatLength = 500 };

            var active = redLines[0];

            foreach (var rl in redLines)
            {
                if (rl.Time <= timeMs)
                    active = rl;
                else
                    break;
            }

            return active;
        }

        public int ActiveBpm(double timeMs)
        {
            var rl = ActiveRedLine(timeMs);
            return rl.BeatLength <= 0 ? 120 : (int)Math.Round(60000 / rl.BeatLength);
        }

        /// <summary>
        /// Snaps to the beat divisor grid of the red line at the time, or of the next one when that is closer (sequencer.ts snapTimeToGrid).
        /// </summary>
        public double SnapTimeToGrid(double timeMs)
        {
            var redLines = RedLines;
            if (redLines.Count == 0)
                return Math.Max(0, timeMs);

            int activeIdx = 0;

            for (int i = 0; i < redLines.Count; i++)
            {
                if (redLines[i].Time <= timeMs)
                    activeIdx = i;
                else
                    break;
            }

            var rl = redLines[activeIdx];
            double snapInterval = rl.BeatLength / screen.SnapDivisor;
            double snappedTime = Math.Max(0, Math.Round(rl.Time + Math.Round((timeMs - rl.Time) / snapInterval) * snapInterval));

            if (activeIdx + 1 < redLines.Count && snappedTime >= redLines[activeIdx + 1].Time)
            {
                var nextRl = redLines[activeIdx + 1];
                double nextInterval = nextRl.BeatLength / screen.SnapDivisor;
                double nextSnapped = Math.Max(0, Math.Round(nextRl.Time + Math.Round((timeMs - nextRl.Time) / nextInterval) * nextInterval));

                if (Math.Abs(timeMs - nextSnapped) < Math.Abs(timeMs - snappedTime))
                    return nextSnapped;
            }

            return snappedTime;
        }

        private List<(double Start, double End)> kiaiIntervals()
        {
            var intervals = new List<(double, double)>();
            double? start = null;

            foreach (var effect in editorBeatmap.ControlPointInfo.EffectPoints)
            {
                if (effect.KiaiMode && start == null)
                    start = effect.Time;
                else if (!effect.KiaiMode && start != null)
                {
                    intervals.Add((start.Value, effect.Time));
                    start = null;
                }
            }

            if (start != null)
                intervals.Add((start.Value, Math.Max(start.Value + 10000, durationMs)));

            return intervals;
        }

        #endregion

        #region View

        public void SetZoom(float zoom) => Zoom.Value = Math.Clamp(zoom, MIN_ZOOM, MAX_ZOOM);

        public void PageBy(float pages) => ScrollLeftMs = Math.Max(0, ScrollLeftMs + width / Zoom.Value * 1000 * pages);

        /// <summary>
        /// Keeps the playhead in view (sequencer.ts setTime with followPlayhead).
        /// </summary>
        private void follow(double timeMs)
        {
            double viewDurationMs = width / Zoom.Value * 1000;

            if (timeMs > ScrollLeftMs + viewDurationMs * 0.85 || timeMs < ScrollLeftMs)
                ScrollLeftMs = Math.Max(0, timeMs - viewDurationMs * 0.2);
        }

        /// <summary>
        /// Where a time on a lane is on screen (for tests).
        /// </summary>
        internal Vector2 ScreenSpacePositionAt(double time, int laneIndex) =>
            ToScreenSpace(new Vector2(msToPx(time), RULER_HEIGHT - ScrollTop.Value + laneIndex * LaneHeight + LaneHeight / 2));

        public void ResetView()
        {
            ScrollLeftMs = 0;
            ScrollTop.Value = 0;
        }

        #endregion

        protected override void Update()
        {
            base.Update();

            laneMask.Height = trackAreaHeight;
            laneLayer.Height = DrawHeight;

            if (ScrollTop.Value > maxScrollTop)
                ScrollTop.Value = maxScrollTop;

            double now = clock.CurrentTime;

            if (now != lastClockTime)
            {
                follow(now);
                lastClockTime = now;
            }

            if (dragNotesStart != null && dragNotesStart.HasDragged)
                autoScrollWhileDragging();

            render();
        }

        #region Rendering (sequencer.ts render)

        private static readonly Colour4 background = Colour4.FromHex("0f1115");
        private static readonly Colour4 accent = Colour4.FromHex("4a9eff");

        private static Colour4 rgba(int r, int g, int b, float a) => new Colour4(r / 255f, g / 255f, b / 255f, a);

        private void render()
        {
            laneLayer.Clear();
            rulerLayer.Clear();
            overlayLayer.Clear();
            usedLabels = 0;

            double viewStartMs = ScrollLeftMs;
            double viewEndMs = pxToMs(width);
            float height = DrawHeight;
            var kiai = kiaiIntervals();

            laneLayer.Rect(0, 0, width, height, background);

            renderLaneRows(height - SCROLLBAR_HEIGHT, kiai);
            renderGrid(viewStartMs, viewEndMs, height - SCROLLBAR_HEIGHT);
            renderGhostObjects(viewStartMs, viewEndMs);
            renderTriggers(viewStartMs, viewEndMs);

            if (isBoxSelecting)
                renderSelectionBox();

            renderRulerAndWaveform(viewStartMs, viewEndMs, kiai);
            renderBottomScrollbar(height, kiai);
            renderPlayhead(height - SCROLLBAR_HEIGHT);

            for (int i = usedLabels; i < rulerText.Count; i++)
                rulerText[i].Alpha = 0;

            laneLayer.Commit();
            rulerLayer.Commit();
            overlayLayer.Commit();
        }

        private void renderRulerAndWaveform(double viewStartMs, double viewEndMs, List<(double Start, double End)> kiai)
        {
            const float ruler_h = RULER_HEIGHT;

            rulerLayer.Rect(0, 0, width, ruler_h, Colour4.FromHex("14171f"));

            foreach (var (start, end) in kiai)
            {
                if (end < viewStartMs || start > viewEndMs) continue;

                float startX = Math.Max(0, msToPx(start));
                float endX = Math.Min(width, msToPx(end));
                float w = Math.Max(2, endX - startX);

                rulerLayer.Rect(startX, 0, w, ruler_h, rgba(255, 170, 0, 0.2f));
                rulerLayer.Rect(startX, 0, w, 3, Colour4.FromHex("ffaa00"));
            }

            var waveform = screen.Waveform;

            if (waveform != null)
            {
                int startIdx = Math.Max(0, (int)Math.Floor(ScrollLeftMs / 1000 * HitsoundWaveform.POINTS_PER_SECOND));
                int endIdx = Math.Min(waveform.Peaks.Length, (int)Math.Ceiling(pxToMs(width) / 1000 * HitsoundWaveform.POINTS_PER_SECOND));

                // One bar per pixel column (the loudest point in it), so zooming out does not draw thousands of overlapping bars.
                float column = float.NaN;
                float peak = 0;
                float transient = 0;

                for (int i = startIdx; i <= endIdx; i++)
                {
                    float x = i < endIdx ? MathF.Floor(msToPx(i * 1000.0 / HitsoundWaveform.POINTS_PER_SECOND)) : float.NaN;

                    if (x != column)
                    {
                        if (!float.IsNaN(column))
                        {
                            float h = peak * ruler_h * 0.7f;
                            rulerLayer.Rect(column, ruler_h - h, 2, h, rgba(74, 158, 255, 0.16f));

                            if (transient > 0.08f)
                            {
                                float th = Math.Min(ruler_h, transient * ruler_h * 1.5f);
                                rulerLayer.Rect(column, ruler_h - th, 1.5f, th, rgba(255, 196, 0, 0.4f));
                            }
                        }

                        column = x;
                        peak = 0;
                        transient = 0;
                    }

                    if (i < endIdx)
                    {
                        peak = Math.Max(peak, waveform.Peaks[i]);
                        transient = Math.Max(transient, waveform.Transients[i]);
                    }
                }
            }

            rulerLayer.Rect(0, ruler_h - 1, width, 1, Colour4.FromHex("2b3142"));

            var redLines = RedLines;
            if (redLines.Count == 0)
                return;

            double cumulativeBeats = 0;

            for (int i = 0; i < redLines.Count; i++)
            {
                var rl = redLines[i];
                double segmentEndMs = i + 1 < redLines.Count ? redLines[i + 1].Time : Math.Max(viewEndMs, durationMs);
                double beatLength = rl.BeatLength;
                int meter = Math.Max(1, rl.TimeSignature.Numerator);
                double measureMs = beatLength * meter;
                double beatsInSegment = Math.Max(1, Math.Round((segmentEndMs - rl.Time) / beatLength));

                if (segmentEndMs < viewStartMs || rl.Time > viewEndMs || measureMs <= 0)
                {
                    cumulativeBeats += beatsInSegment;
                    continue;
                }

                double segStartMs = Math.Max(rl.Time, viewStartMs);
                double segEndMs = Math.Min(segmentEndMs, viewEndMs);
                int startM = Math.Max(0, (int)Math.Floor((segStartMs - rl.Time) / measureMs));
                int endM = (int)Math.Ceiling((segEndMs - rl.Time) / measureMs);

                double measurePx = measureMs / 1000 * Zoom.Value;
                int labelStep = measurePx < 3.5 ? 32 : measurePx < 7 ? 16 : measurePx < 14 ? 8 : measurePx < 28 ? 4 : measurePx < 55 ? 2 : 1;
                float lastDrawnX = -9999;

                for (int m = startM; m <= endM; m++)
                {
                    double mTime = rl.Time + m * measureMs;
                    if (mTime > segmentEndMs) break;
                    if (mTime < viewStartMs || mTime > viewEndMs) continue;

                    float x = MathF.Round(msToPx(mTime));

                    rulerLayer.Rect(x - 1, ruler_h - 22, 2, 22, accent);

                    int measureNum = 1 + (int)Math.Floor((cumulativeBeats + m * meter) / meter);

                    if (m % labelStep == 0 && x > lastDrawnX + 50)
                    {
                        label(measureNum.ToString(), x + 5, 8, 12, true, Colour4.FromHex("8ab4f8"));
                        label($"{mTime / 1000:0.00}s", x + 5, 24, 10, false, Colour4.FromHex("7a869a"));
                        lastDrawnX = x;
                    }
                }

                cumulativeBeats += beatsInSegment;
            }

            foreach (var rl in redLines)
            {
                if (rl.Time < viewStartMs - 1000 || rl.Time > viewEndMs + 1000) continue;

                float x = MathF.Round(msToPx(rl.Time));
                int bpm = (int)Math.Round(60000 / rl.BeatLength);

                rulerLayer.Rect(x - 1, 0, 2, ruler_h, Colour4.FromHex("ff3344"));

                var tag = label($"{bpm} BPM", x + 5, ruler_h - 15, 9, true, Colour4.White);
                rulerLayer.RoundedRect(x + 2, ruler_h - 16, tag.DrawWidth + 6, 14, 3, rgba(239, 68, 68, 0.92f));
            }
        }

        private void renderLaneRows(float height, List<(double Start, double End)> kiai)
        {
            float y = RULER_HEIGHT - ScrollTop.Value;
            bool hasSolo = screen.Lanes.Any(l => l.Solo);

            for (int i = 0; i < screen.Lanes.Count; i++)
            {
                var lane = screen.Lanes[i];
                bool inactive = lane.Muted || (hasSolo && !lane.Solo);

                if (y + LaneHeight > RULER_HEIGHT && y < height)
                {
                    laneLayer.Rect(0, y, width, LaneHeight, inactive ? Colour4.FromHex("111319") : i % 2 == 0 ? Colour4.FromHex("161922") : Colour4.FromHex("1a1e28"));

                    if (!inactive)
                    {
                        bool playing = screen.PlayingLaneIds.Contains(lane.Id);
                        var colour = laneColour(lane);

                        laneLayer.Rect(0, y, width, LaneHeight, colour.Opacity(playing ? 0x2c / 255f : 0x0d / 255f));

                        if (playing)
                            laneLayer.Rect(0, y, 3, LaneHeight, colour);
                    }

                    laneLayer.Rect(0, y + LaneHeight - 1, width, 1, inactive ? Colour4.FromHex("1e2330") : Colour4.FromHex("272d3d"));
                }

                y += LaneHeight;
            }

            if (y < height)
                laneLayer.Rect(0, y, width, height - y, background);

            double viewStartMs = ScrollLeftMs;
            double viewEndMs = pxToMs(width);

            foreach (var (start, end) in kiai)
            {
                if (end < viewStartMs || start > viewEndMs) continue;

                float startX = Math.Max(0, msToPx(start));
                float endX = Math.Min(width, msToPx(end));

                laneLayer.Rect(startX, RULER_HEIGHT, Math.Max(2, endX - startX), height - RULER_HEIGHT, rgba(255, 170, 0, 0.035f));

                if (start >= viewStartMs && start <= viewEndMs)
                    laneLayer.DashedVertical(startX - 0.75f, RULER_HEIGHT, height, 1.5f, 4, 4, rgba(255, 170, 0, 0.45f));
            }
        }

        private void renderGrid(double viewStartMs, double viewEndMs, float totalHeight)
        {
            var redLines = RedLines;
            int divisor = screen.SnapDivisor;

            for (int i = 0; i < redLines.Count; i++)
            {
                var rl = redLines[i];
                double segmentEndMs = i + 1 < redLines.Count ? redLines[i + 1].Time : Math.Max(viewEndMs, durationMs);
                double segmentStart = Math.Max(rl.Time, viewStartMs - rl.BeatLength * 2);
                double segmentEnd = Math.Min(segmentEndMs, viewEndMs + rl.BeatLength * 2);

                if (segmentStart >= segmentEnd || rl.BeatLength <= 0) continue;

                int meter = Math.Max(1, rl.TimeSignature.Numerator);
                double subInterval = rl.BeatLength / divisor;
                long startBeat = (long)Math.Floor((segmentStart - rl.Time) / subInterval);
                long endBeat = (long)Math.Ceiling((segmentEnd - rl.Time) / subInterval);

                for (long b = startBeat; b <= endBeat; b++)
                {
                    double timeMs = Math.Round(rl.Time + b * subInterval);
                    if (timeMs >= segmentEndMs) break;
                    if (timeMs < viewStartMs || timeMs > viewEndMs) continue;

                    float x = MathF.Round(msToPx(timeMs));
                    Colour4 colour;
                    float lineWidth = 1;

                    if (b % (divisor * meter) == 0)
                    {
                        colour = rgba(255, 255, 255, 0.45f);
                        lineWidth = 1.5f;
                    }
                    else if (b % divisor == 0)
                        colour = rgba(255, 255, 255, 0.28f);
                    else if (b * 2 % divisor == 0)
                        colour = rgba(255, 80, 80, 0.40f);
                    else if (b * 4 % divisor == 0)
                        colour = rgba(64, 180, 255, 0.32f);
                    else if (b * 3 % divisor == 0)
                        colour = rgba(190, 100, 255, 0.35f);
                    else if (b * 6 % divisor == 0)
                        colour = rgba(255, 90, 200, 0.28f);
                    else if (b * 8 % divisor == 0)
                        colour = rgba(255, 210, 50, 0.30f);
                    else
                        colour = rgba(255, 255, 255, 0.12f);

                    laneLayer.Rect(x, RULER_HEIGHT, lineWidth, totalHeight - RULER_HEIGHT, colour);
                }
            }
        }

        private void renderGhostObjects(double viewStartMs, double viewEndMs)
        {
            var ghosts = screen.GhostObjects;

            if (!ShowGhostNotes.Value || ghosts.Count == 0)
                return;

            float totalLanesHeight = screen.Lanes.Count * LaneHeight;
            float baseY = RULER_HEIGHT - ScrollTop.Value;
            const float top_marker_y = RULER_HEIGHT + 8;

            foreach (var ho in ghosts)
            {
                if (ho.Time < viewStartMs - 2000 || ho.Time > viewEndMs + 2000) continue;

                float x = MathF.Round(msToPx(ho.Time));

                switch (ho.Kind)
                {
                    case GhostKind.Circle:
                        laneLayer.Rect(x, baseY, 1, totalLanesHeight, rgba(255, 255, 255, 0.12f));
                        laneLayer.Dot(x + 0.5f, top_marker_y, 3, rgba(255, 255, 255, 0.65f));
                        break;

                    case GhostKind.Slider:
                    {
                        float endX = MathF.Round(msToPx(ho.EndTime));
                        float w = Math.Max(4, endX - x);

                        laneLayer.Rect(x, baseY, w, totalLanesHeight, rgba(74, 158, 255, 0.04f));
                        laneLayer.Rect(x, baseY, 1, totalLanesHeight, rgba(74, 158, 255, 0.25f));

                        for (int e = 1; e < ho.EdgeTimes.Length; e++)
                        {
                            float edgeX = MathF.Round(msToPx(ho.EdgeTimes[e]));
                            bool isTail = e == ho.EdgeTimes.Length - 1;

                            laneLayer.Rect(edgeX, baseY, 1, totalLanesHeight, isTail ? rgba(74, 158, 255, 0.25f) : rgba(255, 170, 0, 0.35f));

                            if (!isTail)
                            {
                                laneLayer.Shape(new Vector2(edgeX + 0.5f, top_marker_y - 4), new Vector2(edgeX + 3.5f, top_marker_y),
                                    new Vector2(edgeX - 2.5f, top_marker_y), new Vector2(edgeX + 0.5f, top_marker_y + 4), rgba(255, 170, 0, 0.85f));
                            }
                        }

                        laneLayer.RoundedRect(x, top_marker_y - 3, w, 6, 3, rgba(74, 158, 255, 0.55f));
                        break;
                    }

                    case GhostKind.Spinner:
                    {
                        float endX = MathF.Round(msToPx(ho.EndTime));
                        float w = Math.Max(4, endX - x);

                        laneLayer.Rect(x, baseY, w, totalLanesHeight, rgba(230, 64, 255, 0.03f));
                        laneLayer.Rect(x, top_marker_y - 2, w, 4, rgba(230, 64, 255, 0.5f));
                        break;
                    }
                }
            }
        }

        public float TriggerWidth => Math.Max(6, Math.Min(26, Zoom.Value * 0.035f));

        private float noteHeight => Math.Max(16, LaneHeight - (LaneHeight < 36 ? 4 : 12));

        private void renderTriggers(double viewStartMs, double viewEndMs)
        {
            var laneIndex = laneIndexMap();
            bool hasSolo = screen.Lanes.Any(l => l.Solo);
            float triggerW = TriggerWidth;
            float h = noteHeight;

            if (draggedNotesPreview != null && dragNotesStart != null)
            {
                foreach (var orig in dragNotesStart.OriginalNotes.Values)
                {
                    if (!laneIndex.TryGetValue(orig.LaneId, out int lIdx)) continue;

                    float x = MathF.Round(msToPx(orig.Time) - triggerW / 2);
                    float y = RULER_HEIGHT - ScrollTop.Value + lIdx * LaneHeight + MathF.Round((LaneHeight - h) / 2);

                    laneLayer.DashedOutline(x, y, triggerW, h, 1.8f, 3, laneColour(screen.Lanes[lIdx]).Opacity(0.38f));
                }
            }

            var triggers = screen.Triggers;

            // Hits are kept sorted by time, so skip straight to the first one that can be in view (a dragged one can be anywhere).
            int first = draggedNotesPreview == null ? firstAtOrAfter(triggers, viewStartMs - 500) : 0;

            for (int i = first; i < triggers.Count; i++)
            {
                var tr = triggers[i];
                (string LaneId, double Time)? preview = draggedNotesPreview != null && draggedNotesPreview.TryGetValue(tr.Id, out var p) ? p : null;
                string effectiveLaneId = preview?.LaneId ?? tr.LaneId;
                double effectiveTime = preview?.Time ?? tr.Time;

                if (draggedNotesPreview == null && effectiveTime > viewEndMs + 500) break;
                if (effectiveTime < viewStartMs - 500 || effectiveTime > viewEndMs + 500) continue;
                if (!laneIndex.TryGetValue(effectiveLaneId, out int lIdx)) continue;

                var lane = screen.Lanes[lIdx];
                bool inactive = lane.Muted || (hasSolo && !lane.Solo);

                float x = MathF.Round(msToPx(effectiveTime) - triggerW / 2);
                float y = RULER_HEIGHT - ScrollTop.Value + lIdx * LaneHeight + MathF.Round((LaneHeight - h) / 2);

                if (y + h < RULER_HEIGHT || y > DrawHeight) continue;

                float alpha = inactive ? 0.28f : 1;
                bool selected = screen.SelectedIds.Contains(tr.Id);

                laneLayer.RoundedRect(x, y, triggerW, h, 4, laneColour(lane).Opacity(alpha));

                if (preview != null)
                {
                    laneLayer.Outline(x, y, triggerW, h, 2.5f, Colour4.FromHex("00e5ff").Opacity(alpha));
                    laneLayer.Rect(x + 2, y + 2, triggerW - 4, h - 4, rgba(0, 229, 255, 0.45f * alpha));
                }
                else if (selected)
                {
                    laneLayer.Outline(x, y, triggerW, h, 2.5f, Colour4.FromHex("fffb00").Opacity(alpha));
                    laneLayer.Rect(x + 2, y + 2, triggerW - 4, h - 4, rgba(255, 255, 255, 0.4f * alpha));
                }
                else
                {
                    laneLayer.Outline(x, y, triggerW, h, 1.2f, Colour4.White.Opacity(alpha));
                    laneLayer.Rect(x + 2, y + 2, triggerW - 4, 2, rgba(255, 255, 255, 0.7f * alpha));
                }
            }
        }

        private void renderBottomScrollbar(float height, List<(double Start, double End)> kiai)
        {
            float barY = height - SCROLLBAR_HEIGHT;

            overlayLayer.Rect(0, barY, width, SCROLLBAR_HEIGHT, Colour4.FromHex("0a0c10"));
            overlayLayer.Rect(0, barY, width, 1, Colour4.FromHex("1e2330"));

            var (thumbX, thumbW, totalMs) = scrollbarThumb();

            foreach (var (start, end) in kiai)
                overlayLayer.Rect((float)(start / totalMs) * width, barY + 2, Math.Max(2, (float)((end - start) / totalMs) * width), SCROLLBAR_HEIGHT - 4, rgba(255, 170, 0, 0.45f));

            overlayLayer.RoundedRect(thumbX, barY + 2, thumbW, SCROLLBAR_HEIGHT - 4, 3, isDraggingScrollbar ? accent : rgba(255, 255, 255, 0.28f));
        }

        private (float X, float Width, double TotalMs) scrollbarThumb()
        {
            double viewDurationMs = width / Zoom.Value * 1000;
            double totalMs = Math.Max(viewDurationMs, durationMs);
            float thumbW = Math.Max(30, (float)(viewDurationMs / totalMs) * width);
            double maxScrollMs = Math.Max(0, totalMs - viewDurationMs);
            float thumbX = maxScrollMs > 0 ? (float)(ScrollLeftMs / maxScrollMs) * (width - thumbW) : 0;
            return (thumbX, thumbW, totalMs);
        }

        private void handleScrollbarClick(float clickX)
        {
            double viewDurationMs = width / Zoom.Value * 1000;
            double totalMs = Math.Max(viewDurationMs, durationMs);
            float thumbW = Math.Max(30, (float)(viewDurationMs / totalMs) * width);
            double maxScrollMs = Math.Max(0, totalMs - viewDurationMs);

            double targetRatio = Math.Clamp((clickX - thumbW / 2) / (width - thumbW), 0, 1);
            ScrollLeftMs = targetRatio * maxScrollMs;
        }

        private void renderSelectionBox()
        {
            float x = Math.Min(selectionStart.X, selectionCurrent.X);
            float y = Math.Min(selectionStart.Y, selectionCurrent.Y);
            float w = Math.Abs(selectionCurrent.X - selectionStart.X);
            float h = Math.Abs(selectionCurrent.Y - selectionStart.Y);

            laneLayer.Rect(x, y, w, h, rgba(74, 158, 255, 0.15f));
            laneLayer.DashedOutline(x, y, w, h, 1, 4, accent);
        }

        private void renderPlayhead(float totalHeight)
        {
            float x = MathF.Round(msToPx(clock.CurrentTime));

            overlayLayer.Rect(x - 0.25f, 0, 1.5f, totalHeight, Colour4.White);
            overlayLayer.Shape(new Vector2(x - 7, 0), new Vector2(x + 7, 0), new Vector2(x, 12), new Vector2(x, 12), accent);
        }

        private OsuSpriteText label(string text, float x, float y, float size, bool bold, Colour4 colour)
        {
            if (usedLabels == rulerText.Count)
                rulerText.Add(new OsuSpriteText());

            var sprite = rulerText[usedLabels++];
            sprite.Text = text;
            sprite.Font = OsuFont.Default.With(size: size, weight: bold ? FontWeight.Bold : FontWeight.Regular);
            sprite.Colour = colour;
            sprite.Position = new Vector2(x, y);
            sprite.Alpha = 1;
            return sprite;
        }

        private static readonly Dictionary<string, Colour4> lane_colour_cache = new Dictionary<string, Colour4>();

        private static Colour4 laneColour(HitsoundLane lane)
        {
            if (!lane_colour_cache.TryGetValue(lane.Colour, out var colour))
                lane_colour_cache[lane.Colour] = colour = Colour4.TryParseHex(lane.Colour, out var parsed) ? parsed : Colour4.HotPink;

            return colour;
        }

        private Dictionary<string, int> laneIndexMap()
        {
            var map = new Dictionary<string, int>();
            for (int i = 0; i < screen.Lanes.Count; i++)
                map[screen.Lanes[i].Id] = i;
            return map;
        }

        private static int firstAtOrAfter(IReadOnlyList<HitsoundTrigger> triggers, double time)
        {
            int low = 0, high = triggers.Count - 1, result = triggers.Count;

            while (low <= high)
            {
                int mid = (low + high) >> 1;

                if (triggers[mid].Time >= time)
                {
                    result = mid;
                    high = mid - 1;
                }
                else
                    low = mid + 1;
            }

            return result;
        }

        /// <summary>
        /// The index of the first hit after <paramref name="time"/>.
        /// </summary>
        public static int FirstAfter(IReadOnlyList<HitsoundTrigger> triggers, double time)
        {
            int i = firstAtOrAfter(triggers, time);

            while (i < triggers.Count && triggers[i].Time <= time)
                i++;

            return i;
        }

        /// <summary>
        /// The lanes that played in the last <paramref name="windowMs"/> (sequencer.ts getActiveLaneIds), for the playback flash.
        /// </summary>
        public static HashSet<string> ActiveLaneIds(IReadOnlyList<HitsoundTrigger> triggers, double currentMs, double windowMs = 80)
        {
            var active = new HashSet<string>();

            for (int i = firstAtOrAfter(triggers, currentMs - windowMs); i < triggers.Count && triggers[i].Time <= currentMs + 20; i++)
                active.Add(triggers[i].LaneId);

            return active;
        }

        #endregion

        #region Mouse (sequencer.ts onMouseDown / onMouseMove / onMouseUp / onWheel)

        private bool isScrubbingRuler;
        private bool isBoxSelecting;
        private bool isPainting;
        private bool isErasing;
        private bool isPanning;
        private bool isDraggingScrollbar;
        private Vector2 panStart;
        private double panStartScrollLeft;
        private float panStartScrollTop;
        private Vector2 selectionStart;
        private Vector2 selectionCurrent;
        private Vector2 mouseDownPos;
        private Vector2 lastMouse;
        private (HitsoundLane Lane, double Time)? pendingClickNote;
        private HashSet<string> initialSelection = new HashSet<string>();
        private string? lastPaintedCell;

        private class DragNotesState
        {
            public string AnchorId = string.Empty;
            public double AnchorOriginalTime;
            public int StartLaneIdx;
            public Vector2 StartMouse;
            public double StartRawTime;
            public Dictionary<string, (string LaneId, double Time)> OriginalNotes = new Dictionary<string, (string, double)>();
            public bool HasDragged;
            public bool ClickedExistingWasSelected;
        }

        private DragNotesState? dragNotesStart;
        private Dictionary<string, (string LaneId, double Time)>? draggedNotesPreview;

        /// <summary>
        /// Whether a mouse gesture is in progress (keys that edit the hits wait for it to finish).
        /// </summary>
        public bool IsInteracting => isBoxSelecting || isPainting || isErasing || dragNotesStart != null || pendingClickNote != null;

        private int laneIndexAt(float y) => (int)Math.Floor((y - RULER_HEIGHT + ScrollTop.Value) / LaneHeight);

        private HitsoundTrigger? triggerNear(HitsoundLane lane, double rawTime, float tolerancePx)
        {
            double toleranceMs = tolerancePx / Zoom.Value * 1000;
            HitsoundTrigger? existing = null;
            double minDiff = double.MaxValue;

            foreach (var tr in screen.Triggers)
            {
                if (tr.LaneId != lane.Id) continue;

                double diff = Math.Abs(tr.Time - rawTime);

                if (diff <= toleranceMs && diff < minDiff)
                {
                    minDiff = diff;
                    existing = tr;
                }
            }

            return existing;
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            var pos = ToLocalSpace(e.ScreenSpaceMousePosition);
            lastMouse = pos;

            // Bottom scrollbar.
            if (pos.Y >= DrawHeight - SCROLLBAR_HEIGHT)
            {
                isDraggingScrollbar = true;
                handleScrollbarClick(pos.X);
                return true;
            }

            // Middle click or Alt + left click: hand pan.
            if (e.Button == MouseButton.Middle || (e.AltPressed && e.Button == MouseButton.Left))
            {
                startPan(pos);
                return true;
            }

            // Ruler: left scrubs, right pans.
            if (pos.Y <= RULER_HEIGHT)
            {
                if (e.Button == MouseButton.Right)
                {
                    startPan(pos);
                    return true;
                }

                if (e.Button != MouseButton.Left)
                    return false;

                isScrubbingRuler = true;
                screen.SeekTo(Math.Max(0, pxToMs(pos.X)));
                return true;
            }

            int laneIdx = laneIndexAt(pos.Y);
            if (laneIdx < 0 || laneIdx >= screen.Lanes.Count)
            {
                if (e.Button == MouseButton.Left && !e.ShiftPressed)
                    screen.SetSelection(Enumerable.Empty<string>());
                return e.Button == MouseButton.Left;
            }

            var lane = screen.Lanes[laneIdx];
            double rawTime = pxToMs(pos.X);
            double snappedTime = SnapTimeToGrid(rawTime);
            var existing = triggerNear(lane, rawTime, 16);

            if (e.Button == MouseButton.Right)
            {
                if (!screen.Editable)
                    return true;

                if (existing != null)
                {
                    screen.PushHistory();
                    screen.RemoveTrigger(existing.Id);
                    return true;
                }

                isErasing = true;
                screen.PushHistory();
                eraseTriggerAt(pos);
                return true;
            }

            if (e.Button != MouseButton.Left)
                return false;

            if (e.ControlPressed)
            {
                if (!screen.Editable)
                    return true;

                isPainting = true;
                screen.PushHistory();
                paintTriggerAt(pos);
                return true;
            }

            if (existing != null)
            {
                bool wasAlreadySelected = screen.SelectedIds.Contains(existing.Id);

                if (e.ShiftPressed)
                {
                    if (wasAlreadySelected)
                        screen.SelectedIds.Remove(existing.Id);
                    else
                        screen.SelectedIds.Add(existing.Id);
                }
                else if (!wasAlreadySelected)
                {
                    screen.SelectedIds.Clear();
                    screen.SelectedIds.Add(existing.Id);
                }

                screen.SelectionChanged();

                if (screen.SelectedIds.Contains(existing.Id) && screen.Editable)
                {
                    dragNotesStart = new DragNotesState
                    {
                        AnchorId = existing.Id,
                        AnchorOriginalTime = existing.Time,
                        StartLaneIdx = laneIdx,
                        StartMouse = pos,
                        StartRawTime = rawTime,
                        OriginalNotes = screen.Triggers.Where(t => screen.SelectedIds.Contains(t.Id)).ToDictionary(t => t.Id, t => (t.LaneId, t.Time)),
                        ClickedExistingWasSelected = wasAlreadySelected,
                    };
                }

                screen.PreviewLane(lane);
                return true;
            }

            // Empty space: place the note on release, so a drag selects without dropping a note.
            if (!e.ShiftPressed)
                screen.SetSelection(Enumerable.Empty<string>());

            if (screen.Editable)
                pendingClickNote = (lane, snappedTime);

            mouseDownPos = pos;
            selectionStart = pos;
            selectionCurrent = pos;
            isBoxSelecting = false;
            initialSelection = new HashSet<string>(screen.SelectedIds);
            return true;
        }

        private void startPan(Vector2 pos)
        {
            isPanning = true;
            panStart = pos;
            panStartScrollLeft = ScrollLeftMs;
            panStartScrollTop = ScrollTop.Value;
        }

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            pointerMove(ToLocalSpace(e.ScreenSpaceMousePosition), e.ShiftPressed);
            return base.OnMouseMove(e);
        }

        // Left drags keep reporting when the cursor leaves the canvas.
        protected override bool OnDragStart(DragStartEvent e) => e.Button == MouseButton.Left;

        protected override void OnDrag(DragEvent e) => pointerMove(ToLocalSpace(e.ScreenSpaceMousePosition), e.ShiftPressed);

        private void pointerMove(Vector2 pos, bool shift)
        {
            lastMouse = pos;

            if (isDraggingScrollbar)
            {
                handleScrollbarClick(pos.X);
                return;
            }

            if (isPanning)
            {
                var delta = pos - panStart;
                ScrollLeftMs = Math.Max(0, panStartScrollLeft - delta.X / Zoom.Value * 1000);
                ScrollTop.Value = Math.Clamp(panStartScrollTop - delta.Y, 0, maxScrollTop);
                return;
            }

            if (isScrubbingRuler)
            {
                screen.SeekTo(Math.Max(0, pxToMs(pos.X)));
                return;
            }

            if (dragNotesStart != null)
            {
                if (!dragNotesStart.HasDragged && Vector2.Distance(pos, dragNotesStart.StartMouse) >= 4)
                    dragNotesStart.HasDragged = true;

                if (dragNotesStart.HasDragged)
                {
                    updateDragPreview(pos);
                    return;
                }
            }

            if (isPainting)
            {
                paintTriggerAt(pos);
                return;
            }

            if (isErasing)
            {
                eraseTriggerAt(pos);
                return;
            }

            if (pendingClickNote != null && Vector2.Distance(pos, mouseDownPos) >= 5)
            {
                pendingClickNote = null;
                isBoxSelecting = true;
            }

            if (isBoxSelecting)
            {
                selectionCurrent = pos;
                updateBoxSelection(shift);
            }
        }

        private void autoScrollWhileDragging()
        {
            if (lastMouse.X > width - 25)
                ScrollLeftMs += 25 / Zoom.Value * 1000 * Math.Min(1, Time.Elapsed / 16);
            else if (lastMouse.X < 25 && ScrollLeftMs > 0)
                ScrollLeftMs = Math.Max(0, ScrollLeftMs - 25 / Zoom.Value * 1000 * Math.Min(1, Time.Elapsed / 16));
            else
                return;

            updateDragPreview(lastMouse);
        }

        private void updateDragPreview(Vector2 pos)
        {
            var drag = dragNotesStart!;
            double rawDeltaMs = pxToMs(pos.X) - drag.StartRawTime;

            // Snap the anchor, move everything by the same amount.
            double targetAnchorSnapped = Math.Max(0, SnapTimeToGrid(drag.AnchorOriginalTime + rawDeltaMs));
            double effectiveDeltaMs = targetAnchorSnapped - drag.AnchorOriginalTime;

            foreach (var orig in drag.OriginalNotes.Values)
            {
                if (orig.Time + effectiveDeltaMs < 0)
                    effectiveDeltaMs = -orig.Time;
            }

            int laneDelta = laneIndexAt(pos.Y) - drag.StartLaneIdx;
            var laneIndex = laneIndexMap();

            foreach (var orig in drag.OriginalNotes.Values)
            {
                int origIdx = laneIndex.GetValueOrDefault(orig.LaneId);

                if (origIdx + laneDelta < 0)
                    laneDelta = -origIdx;
                else if (origIdx + laneDelta >= screen.Lanes.Count)
                    laneDelta = screen.Lanes.Count - 1 - origIdx;
            }

            var preview = new Dictionary<string, (string, double)>();

            foreach (var (id, orig) in drag.OriginalNotes)
            {
                int target = Math.Clamp(laneIndex.GetValueOrDefault(orig.LaneId) + laneDelta, 0, screen.Lanes.Count - 1);
                preview[id] = (screen.Lanes[target].Id, Math.Max(0, Math.Round(orig.Time + effectiveDeltaMs)));
            }

            draggedNotesPreview = preview;
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            isScrubbingRuler = false;
            isDraggingScrollbar = false;
            isPanning = false;

            if (isPainting || isErasing)
            {
                isPainting = false;
                isErasing = false;
                lastPaintedCell = null;
                screen.EndGesture();
            }

            if (dragNotesStart != null)
            {
                if (dragNotesStart.HasDragged && draggedNotesPreview != null)
                {
                    bool anyChanged = draggedNotesPreview.Any(kv => dragNotesStart.OriginalNotes.TryGetValue(kv.Key, out var orig) && orig != kv.Value);

                    if (anyChanged)
                    {
                        screen.PushHistory();
                        screen.MoveTriggers(draggedNotesPreview);

                        if (draggedNotesPreview.TryGetValue(dragNotesStart.AnchorId, out var anchorMove)
                            && screen.Lanes.FirstOrDefault(l => l.Id == anchorMove.LaneId) is HitsoundLane targetLane)
                            screen.PreviewLane(targetLane);
                    }
                }
                else if (!dragNotesStart.HasDragged && dragNotesStart.ClickedExistingWasSelected && !e.ShiftPressed)
                {
                    // A click on a note of a bigger selection selects only it.
                    screen.SetSelection(new[] { dragNotesStart.AnchorId });
                }

                dragNotesStart = null;
                draggedNotesPreview = null;
            }

            if (pendingClickNote is { } pending)
            {
                screen.PushHistory();
                screen.AddTrigger(pending.Lane, pending.Time);
                screen.PreviewLane(pending.Lane);
                pendingClickNote = null;
            }

            isBoxSelecting = false;

            base.OnMouseUp(e);
        }

        private void paintTriggerAt(Vector2 pos)
        {
            int laneIdx = laneIndexAt(pos.Y);
            if (laneIdx < 0 || laneIdx >= screen.Lanes.Count) return;

            var lane = screen.Lanes[laneIdx];
            double snappedTime = SnapTimeToGrid(pxToMs(pos.X));
            string cellKey = $"{lane.Id}_{snappedTime}";

            if (lastPaintedCell == cellKey) return;

            lastPaintedCell = cellKey;

            double toleranceMs = 10 / Zoom.Value * 1000;

            if (!screen.Triggers.Any(t => t.LaneId == lane.Id && Math.Abs(t.Time - snappedTime) <= toleranceMs))
            {
                screen.AddTrigger(lane, snappedTime);
                screen.PreviewLane(lane);
            }
        }

        private void eraseTriggerAt(Vector2 pos)
        {
            int laneIdx = laneIndexAt(pos.Y);
            if (laneIdx < 0 || laneIdx >= screen.Lanes.Count) return;

            var lane = screen.Lanes[laneIdx];
            double rawTime = pxToMs(pos.X);
            double toleranceMs = 16 / Zoom.Value * 1000;

            var existing = screen.Triggers.FirstOrDefault(t => t.LaneId == lane.Id && Math.Abs(t.Time - rawTime) <= toleranceMs);

            if (existing != null)
                screen.RemoveTrigger(existing.Id);
        }

        private void updateBoxSelection(bool keepExisting)
        {
            float minX = Math.Min(selectionStart.X, selectionCurrent.X);
            float maxX = Math.Max(selectionStart.X, selectionCurrent.X);
            float minY = Math.Min(selectionStart.Y, selectionCurrent.Y);
            float maxY = Math.Max(selectionStart.Y, selectionCurrent.Y);

            double minTime = pxToMs(minX);
            double maxTime = pxToMs(maxX);
            double halfWTime = TriggerWidth / 2 / Zoom.Value * 1000;
            var laneIndex = laneIndexMap();

            var selected = keepExisting ? new HashSet<string>(initialSelection) : new HashSet<string>();

            foreach (var tr in screen.Triggers)
            {
                if (tr.Time + halfWTime < minTime || tr.Time - halfWTime > maxTime) continue;
                if (!laneIndex.TryGetValue(tr.LaneId, out int lIdx)) continue;

                float noteTop = RULER_HEIGHT - ScrollTop.Value + lIdx * LaneHeight + 2;
                float noteBottom = noteTop + LaneHeight - 4;

                if (noteBottom >= minY && noteTop <= maxY)
                    selected.Add(tr.Id);
            }

            screen.SetSelection(selected);
        }

        protected override bool OnScroll(ScrollEvent e)
        {
            var pos = ToLocalSpace(e.ScreenSpaceMousePosition);

            // A wheel notch is about 100 pixels of scrolling in a browser; down is positive there.
            float deltaY = -e.ScrollDelta.Y * 100;
            float deltaX = -e.ScrollDelta.X * 100;

            if (e.ControlPressed || e.AltPressed)
            {
                double mouseTime = pxToMs(pos.X);
                SetZoom(Zoom.Value * (e.ScrollDelta.Y > 0 ? 1.18f : 0.82f));
                ScrollLeftMs = Math.Max(0, mouseTime - pos.X / Zoom.Value * 1000);
                return true;
            }

            if (e.ShiftPressed)
            {
                ScrollLeftMs = Math.Max(0, ScrollLeftMs + deltaY / Zoom.Value * 600);
                return true;
            }

            if (Math.Abs(deltaX) > Math.Abs(deltaY))
            {
                ScrollLeftMs = Math.Max(0, ScrollLeftMs + deltaX / Zoom.Value * 800);
                return true;
            }

            if (pos.Y <= RULER_HEIGHT)
            {
                ScrollLeftMs = Math.Max(0, ScrollLeftMs + deltaY / Zoom.Value * 600);
                return true;
            }

            ScrollTop.Value = Math.Clamp(ScrollTop.Value + deltaY, 0, maxScrollTop);
            return true;
        }

        /// <summary>
        /// Wheel over the lane rack scrolls the lanes too.
        /// </summary>
        public void ScrollLanesBy(float deltaY) => ScrollTop.Value = Math.Clamp(ScrollTop.Value + deltaY, 0, maxScrollTop);

        #endregion

        /// <summary>
        /// Rectangles and other four-cornered shapes in local coordinates, drawn in one pass with the white pixel.
        /// </summary>
        private partial class QuadLayer : Drawable
        {
            private readonly List<(Quad Quad, Colour4 Colour)> quads = new List<(Quad, Colour4)>();

            private IShader shader = null!;
            private Texture texture = null!;

            [BackgroundDependencyLoader]
            private void load(IRenderer renderer, ShaderManager shaders)
            {
                texture = renderer.WhitePixel;
                shader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE);
            }

            public void Clear() => quads.Clear();

            public void Commit() => Invalidate(Invalidation.DrawNode);

            public void Rect(float x, float y, float w, float h, Colour4 colour)
            {
                if (w <= 0 || h <= 0 || colour.A <= 0)
                    return;

                quads.Add((new Quad(x, y, w, h), colour));
            }

            public void Shape(Vector2 topLeft, Vector2 topRight, Vector2 bottomLeft, Vector2 bottomRight, Colour4 colour) =>
                quads.Add((new Quad(topLeft, topRight, bottomLeft, bottomRight), colour));

            /// <summary>
            /// A rectangle with its corners cut at <paramref name="radius"/>, which reads as rounded at these sizes.
            /// </summary>
            public void RoundedRect(float x, float y, float w, float h, float radius, Colour4 colour)
            {
                float r = Math.Min(radius, Math.Min(w, h) / 2);

                if (r < 1)
                {
                    Rect(x, y, w, h, colour);
                    return;
                }

                Rect(x, y + r, w, h - 2 * r, colour);
                Rect(x + r, y, w - 2 * r, r, colour);
                Rect(x + r, y + h - r, w - 2 * r, r, colour);

                // Corner triangles.
                Shape(new Vector2(x + r, y), new Vector2(x + r, y), new Vector2(x, y + r), new Vector2(x + r, y + r), colour);
                Shape(new Vector2(x + w - r, y), new Vector2(x + w - r, y), new Vector2(x + w - r, y + r), new Vector2(x + w, y + r), colour);
                Shape(new Vector2(x, y + h - r), new Vector2(x + r, y + h - r), new Vector2(x + r, y + h), new Vector2(x + r, y + h), colour);
                Shape(new Vector2(x + w - r, y + h - r), new Vector2(x + w, y + h - r), new Vector2(x + w - r, y + h), new Vector2(x + w - r, y + h), colour);
            }

            public void Dot(float cx, float cy, float radius, Colour4 colour) => RoundedRect(cx - radius, cy - radius, radius * 2, radius * 2, radius * 0.6f, colour);

            public void Outline(float x, float y, float w, float h, float thickness, Colour4 colour)
            {
                Rect(x, y, w, thickness, colour);
                Rect(x, y + h - thickness, w, thickness, colour);
                Rect(x, y + thickness, thickness, h - 2 * thickness, colour);
                Rect(x + w - thickness, y + thickness, thickness, h - 2 * thickness, colour);
            }

            public void DashedOutline(float x, float y, float w, float h, float thickness, float dash, Colour4 colour)
            {
                for (float d = 0; d < w; d += dash * 2)
                {
                    Rect(x + d, y, Math.Min(dash, w - d), thickness, colour);
                    Rect(x + d, y + h - thickness, Math.Min(dash, w - d), thickness, colour);
                }

                for (float d = 0; d < h; d += dash * 2)
                {
                    Rect(x, y + d, thickness, Math.Min(dash, h - d), colour);
                    Rect(x + w - thickness, y + d, thickness, Math.Min(dash, h - d), colour);
                }
            }

            public void DashedVertical(float x, float top, float bottom, float thickness, float dash, float gap, Colour4 colour)
            {
                for (float y = top; y < bottom; y += dash + gap)
                    Rect(x, y, thickness, Math.Min(dash, bottom - y), colour);
            }

            protected override DrawNode CreateDrawNode() => new QuadLayerDrawNode(this);

            private class QuadLayerDrawNode : DrawNode
            {
                protected new QuadLayer Source => (QuadLayer)base.Source;

                private readonly List<(Quad Quad, Colour4 Colour)> quads = new List<(Quad, Colour4)>();
                private IShader shader = null!;
                private Texture texture = null!;

                public QuadLayerDrawNode(QuadLayer source)
                    : base(source)
                {
                }

                public override void ApplyState()
                {
                    base.ApplyState();

                    shader = Source.shader;
                    texture = Source.texture;
                    quads.Clear();
                    quads.AddRange(Source.quads);
                }

                protected override void Draw(IRenderer renderer)
                {
                    base.Draw(renderer);

                    shader.Bind();

                    foreach (var (quad, colour) in quads)
                    {
                        var screenQuad = new Quad(
                            Vector2Extensions.Transform(quad.TopLeft, DrawInfo.Matrix),
                            Vector2Extensions.Transform(quad.TopRight, DrawInfo.Matrix),
                            Vector2Extensions.Transform(quad.BottomLeft, DrawInfo.Matrix),
                            Vector2Extensions.Transform(quad.BottomRight, DrawInfo.Matrix));

                        ColourInfo drawColour = DrawColourInfo.Colour;
                        drawColour.ApplyChild(colour);

                        renderer.DrawQuad(texture, screenQuad, drawColour);
                    }

                    shader.Unbind();
                }
            }
        }
    }

    public enum GhostKind
    {
        Circle,
        Slider,
        Spinner,
    }

    /// <summary>
    /// YAWNS: an object of the ghost map, drawn behind the lanes.
    /// </summary>
    public record GhostObject(double Time, double EndTime, GhostKind Kind, double[] EdgeTimes);
}
