// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.IO.Serialization;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Screens.Edit.Components.Timelines.Summary.Parts;
using osu.Game.Screens.Edit.Compose.Components.Timeline;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Screens.Edit.Reference
{
    /// <summary>
    /// YAWNS: two lanes under the editor timeline for checking rhythm against the overlaid map.
    /// The top lane shows the edited beatmap and the bottom lane the overlay, both in the same compact style,
    /// and hitsounds (object starts, slider repeats and ends) without a counterpart in the other lane are marked red.
    /// Drag across the lanes to select part of the overlay, then right-click to copy it, insert it, or overlay just that pattern.
    /// </summary>
    public partial class ReferenceRhythmLanes : TimelinePart, IHasContextMenu
    {
        public const float ROW_HEIGHT = 18;

        public const float HEIGHT = ROW_HEIGHT * 2;

        /// <summary>
        /// Hitsounds this close together count as the same rhythm, in milliseconds.
        /// </summary>
        private const double rhythm_leniency = 5;

        [Resolved]
        private Timeline timeline { get; set; } = null!;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        [Resolved]
        private IBeatSnapProvider beatSnapProvider { get; set; } = null!;

        [Resolved]
        private EditorClipboard clipboard { get; set; } = null!;

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        private readonly EditorReferenceBeatmap reference;

        private readonly Bindable<IBeatmap?> beatmap = new Bindable<IBeatmap?>();
        private readonly Bindable<(double Start, double End)?> pattern = new Bindable<(double Start, double End)?>();
        private readonly IBindable<double> offset;
        private readonly BindableBool highlightDifferences = new BindableBool();

        // Both are positioned in time, see Update().
        private readonly Container markers;
        private readonly Container overlayContent;

        private readonly Box selectionBox;
        private readonly OsuSpriteText yourLabel;
        private readonly OsuSpriteText overlayLabel;

        private List<LaneObject> yourObjects = new List<LaneObject>();
        private List<LaneObject> overlayObjects = new List<LaneObject>();

        private bool dataValid;
        private (double Start, double End)? renderedRange;
        private double dragAnchor;

        /// <summary>
        /// The selected time range, in the edited beatmap's time.
        /// </summary>
        public (double Start, double End)? Selection { get; private set; }

        public ReferenceRhythmLanes(EditorReferenceBeatmap reference)
        {
            this.reference = reference;

            RelativeSizeAxes = Axes.X;
            Height = HEIGHT;

            beatmap.BindTo(reference.Beatmap);
            pattern.BindTo(reference.Pattern);
            offset = reference.DisplayOffset.GetBoundCopy();
            highlightDifferences.BindTo(reference.HighlightRhythmDifferences);

            AddRangeInternal(new Drawable[]
            {
                // Shade the overlay lane so the two are easy to tell apart.
                new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Y = ROW_HEIGHT,
                    Height = ROW_HEIGHT,
                    Colour = Color4.Black,
                    Alpha = 0.2f,
                    Depth = 1,
                },
                markers = new Container { RelativeSizeAxes = Axes.Both },
                overlayContent = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        selectionBox = new Box
                        {
                            RelativePositionAxes = Axes.X,
                            RelativeSizeAxes = Axes.X,
                            Y = ROW_HEIGHT,
                            Height = ROW_HEIGHT,
                            Alpha = 0,
                        },
                        yourLabel = createLabel("you", 0),
                        overlayLabel = createLabel("overlay", ROW_HEIGHT),
                    }
                },
            });
        }

        private static OsuSpriteText createLabel(string text, float top) => new OsuSpriteText
        {
            Text = text,
            RelativePositionAxes = Axes.X,
            Y = top + ROW_HEIGHT / 2,
            Origin = Anchor.CentreLeft,
            Margin = new MarginPadding { Left = 4 },
            Font = OsuFont.GetFont(size: 11, weight: FontWeight.SemiBold),
            Alpha = 0.7f,
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();

            selectionBox.Colour = colours.Yellow;

            beatmap.BindValueChanged(_ => resetSelectionAndData());
            pattern.BindValueChanged(_ => resetSelectionAndData());
            offset.BindValueChanged(_ => invalidateData());
            highlightDifferences.BindValueChanged(_ => renderedRange = null);

            EditorBeatmap.HitObjectAdded += onEdited;
            EditorBeatmap.HitObjectRemoved += onEdited;
            EditorBeatmap.HitObjectUpdated += onEdited;
            EditorBeatmap.BeatmapReprocessed += invalidateData;

            invalidateData();
        }

        // The lanes show the overlay and the edited beatmap themselves.
        protected override void LoadBeatmap(EditorBeatmap beatmap)
        {
        }

        private void onEdited(HitObject _) => invalidateData();

        private void invalidateData() => dataValid = false;

        private void resetSelectionAndData()
        {
            ClearSelection();
            invalidateData();
        }

        protected override void Update()
        {
            base.Update();

            var timeScale = new Vector2(Content.RelativeChildSize.X, 1);
            markers.RelativeChildSize = overlayContent.RelativeChildSize = timeScale;

            if (beatmap.Value == null || DrawWidth <= 0)
            {
                if (renderedRange != null)
                {
                    markers.Clear();
                    renderedRange = null;
                }

                return;
            }

            if (!dataValid)
            {
                recompute();
                dataValid = true;
                renderedRange = null;
            }

            double visibleStart = ToLocalSpace(timeline.ScreenSpaceDrawQuad.TopLeft).X / DrawWidth * timeScale.X;
            double visibleEnd = ToLocalSpace(timeline.ScreenSpaceDrawQuad.TopRight).X / DrawWidth * timeScale.X;

            if (renderedRange is not (double renderedStart, double renderedEnd) || visibleStart < renderedStart || visibleEnd > renderedEnd)
            {
                // Render half a screen more on each side, so scrolling does not rebuild every frame.
                double margin = (visibleEnd - visibleStart) / 2;
                render(visibleStart - margin, visibleEnd + margin);
            }

            // Keep the lane names at the left edge of what is visible.
            yourLabel.X = overlayLabel.X = (float)visibleStart;
        }

        #region Rhythm

        /// <summary>
        /// Times of the edited beatmap's hitsounds which have no counterpart in the overlay.
        /// </summary>
        public IEnumerable<double> YourDifferences => yourObjects.SelectMany(o => o.Hitsounds).Where(h => h.Unmatched).Select(h => h.Time);

        /// <summary>
        /// Times of the overlay's hitsounds which have no counterpart in the edited beatmap.
        /// </summary>
        public IEnumerable<double> OverlayDifferences => overlayObjects.SelectMany(o => o.Hitsounds).Where(h => h.Unmatched).Select(h => h.Time);

        /// <summary>
        /// How many objects the overlay lane shows (all of the overlay map, or the overlaid pattern).
        /// </summary>
        public int OverlayObjectCount => overlayObjects.Count;

        private void recompute()
        {
            yourObjects = laneObjectsOf(EditorBeatmap.HitObjects, 0);
            overlayObjects = laneObjectsOf(reference.DisplayedObjects, offset.Value);

            // With only a pattern overlaid, rhythm is compared where the pattern is.
            var compared = pattern.Value == null || overlayObjects.Count == 0
                ? (double.NegativeInfinity, double.PositiveInfinity)
                : (overlayObjects.Min(o => o.Start) - rhythm_leniency, overlayObjects.Max(o => o.End) + rhythm_leniency);

            markDifferences(yourObjects, overlayObjects, compared);
            markDifferences(overlayObjects, yourObjects, compared);
        }

        private List<LaneObject> laneObjectsOf(IEnumerable<HitObject> hitObjects, double timeOffset)
        {
            var result = new List<LaneObject>();

            foreach (var hitObject in hitObjects)
            {
                var laneObject = new LaneObject(hitObject.StartTime + timeOffset, hitObject.GetEndTime() + timeOffset,
                    (hitObject as IHasComboInformation)?.GetComboColour(skin) ?? Color4.White);

                switch (hitObject)
                {
                    case IHasRepeats repeats:
                        double spanDuration = repeats.Duration / repeats.SpanCount();

                        for (int i = 1; i <= repeats.SpanCount(); i++)
                            laneObject.Hitsounds.Add(new LaneHitsound(laneObject.Start + i * spanDuration));

                        break;

                    case IHasDuration:
                        laneObject.Hitsounds.Add(new LaneHitsound(laneObject.End));
                        break;
                }

                // A broken object (a slider with no velocity, say) has no place on the timeline.
                if (laneObject.Hitsounds.All(h => double.IsFinite(h.Time)))
                    result.Add(laneObject);
            }

            return result;
        }

        private static void markDifferences(List<LaneObject> lane, List<LaneObject> other, (double Start, double End) compared)
        {
            double[] otherTimes = other.SelectMany(o => o.Hitsounds).Select(h => h.Time).OrderBy(t => t).ToArray();

            foreach (var hitsound in lane.SelectMany(o => o.Hitsounds))
                hitsound.Unmatched = hitsound.Time >= compared.Start && hitsound.Time <= compared.End && !hasHitsoundNear(otherTimes, hitsound.Time);
        }

        private static bool hasHitsoundNear(double[] sortedTimes, double time)
        {
            int i = Array.BinarySearch(sortedTimes, time - rhythm_leniency);
            if (i < 0) i = ~i;

            return i < sortedTimes.Length && sortedTimes[i] <= time + rhythm_leniency;
        }

        private void render(double start, double end)
        {
            markers.Clear();

            renderLane(yourObjects, 0, start, end);
            renderLane(overlayObjects, ROW_HEIGHT, start, end);

            renderedRange = (start, end);
        }

        private void renderLane(List<LaneObject> objects, float top, double start, double end)
        {
            float centre = top + ROW_HEIGHT / 2;

            foreach (var o in objects)
            {
                if (o.End < start || o.Start > end)
                    continue;

                if (o.End > o.Start)
                {
                    markers.Add(new Circle
                    {
                        RelativePositionAxes = Axes.X,
                        RelativeSizeAxes = Axes.X,
                        Origin = Anchor.CentreLeft,
                        X = (float)o.Start,
                        Y = centre,
                        Width = (float)(o.End - o.Start),
                        Height = ROW_HEIGHT / 4,
                        Colour = o.Colour,
                        Alpha = 0.6f,
                    });
                }

                // Slider repeats and ends, spinner ends.
                foreach (var hitsound in o.Hitsounds.Skip(1))
                {
                    markers.Add(new Box
                    {
                        RelativePositionAxes = Axes.X,
                        Origin = Anchor.Centre,
                        X = (float)hitsound.Time,
                        Y = centre,
                        Size = new Vector2(2, ROW_HEIGHT * 0.6f),
                        Colour = isMarked(hitsound) ? colours.Red : o.Colour,
                    });
                }

                if (isMarked(o.Hitsounds[0]))
                {
                    markers.Add(new CircularContainer
                    {
                        RelativePositionAxes = Axes.X,
                        Origin = Anchor.Centre,
                        X = (float)o.Start,
                        Y = centre,
                        Size = new Vector2(ROW_HEIGHT * 0.85f),
                        Masking = true,
                        BorderThickness = 2,
                        BorderColour = colours.Red,
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Alpha = 0,
                            AlwaysPresent = true,
                        },
                    });
                }

                markers.Add(new Circle
                {
                    RelativePositionAxes = Axes.X,
                    Origin = Anchor.Centre,
                    X = (float)o.Start,
                    Y = centre,
                    Size = new Vector2(ROW_HEIGHT * 0.55f),
                    Colour = o.Colour,
                });
            }
        }

        private bool isMarked(LaneHitsound hitsound) => highlightDifferences.Value && hitsound.Unmatched;

        private class LaneObject
        {
            public readonly double Start;
            public readonly double End;
            public readonly Color4 Colour;

            /// <summary>
            /// The object's start first, then its repeats and end.
            /// </summary>
            public readonly List<LaneHitsound> Hitsounds;

            public LaneObject(double start, double end, Color4 colour)
            {
                Start = start;
                End = end;
                Colour = colour;
                Hitsounds = new List<LaneHitsound> { new LaneHitsound(start) };
            }
        }

        private class LaneHitsound
        {
            public readonly double Time;

            public bool Unmatched;

            public LaneHitsound(double time)
            {
                Time = time;
            }
        }

        #endregion

        #region Selection

        /// <summary>
        /// Overlay objects starting inside the selection. Times are the overlay map's own, without the offset.
        /// </summary>
        public IEnumerable<HitObject> SelectedObjects
        {
            get
            {
                if (Selection is not (double start, double end))
                    return Enumerable.Empty<HitObject>();

                // An object counts as selected when its marker overlaps the selection.
                // Edges are not snapped, the overlay usually sits off the edited beatmap's beat grid.
                double markerRadius = DrawWidth > 0 ? ROW_HEIGHT / 4 / DrawWidth * Content.RelativeChildSize.X : 0;
                double timeOffset = offset.Value;

                return reference.DisplayedObjects.Where(h => h.StartTime + timeOffset >= start - markerRadius && h.StartTime + timeOffset <= end + markerRadius);
            }
        }

        public void Select(double start, double end)
        {
            Selection = (Math.Min(start, end), Math.Max(start, end));
            updateSelectionBox();
        }

        public void ClearSelection()
        {
            Selection = null;
            updateSelectionBox();
        }

        private void updateSelectionBox()
        {
            if (Selection is (double start, double end))
            {
                selectionBox.X = (float)start;
                selectionBox.Width = (float)(end - start);
                selectionBox.Alpha = 0.3f;
            }
            else
                selectionBox.Alpha = 0;
        }

        /// <summary>
        /// Puts the selected overlay objects on the editor clipboard, to be pasted anywhere with the usual paste.
        /// </summary>
        public void CopySelection()
        {
            var selected = SelectedObjects.ToList();

            if (selected.Count > 0)
                clipboard.Content.Value = new ClipboardContent { HitObjects = selected }.Serialize();
        }

        /// <summary>
        /// Adds copies of the selected overlay objects to the edited beatmap at the times they are shown, as one undoable change.
        /// </summary>
        public void InsertSelection()
        {
            var selected = SelectedObjects.ToList();

            if (selected.Count == 0)
                return;

            // A serialisation round trip gives independent copies, same as the editor's own paste.
            var copies = new ClipboardContent { HitObjects = selected }.Serialize().Deserialize<ClipboardContent>().HitObjects;

            foreach (var h in copies)
                h.StartTime += offset.Value;

            EditorBeatmap.BeginChange();
            EditorBeatmap.SelectedHitObjects.Clear();
            EditorBeatmap.AddRange(copies);
            EditorBeatmap.SelectedHitObjects.AddRange(copies);
            EditorBeatmap.EndChange();
        }

        /// <summary>
        /// Overlays only the selected objects, with the first of them at the current time (snapped like a paste).
        /// </summary>
        public void OverlaySelectedPattern()
        {
            var selected = SelectedObjects.ToList();

            if (selected.Count > 0)
                reference.OverlayPattern(selected.Min(h => h.StartTime), selected.Max(h => h.StartTime), beatSnapProvider.SnapTime(editorClock.CurrentTime));
        }

        public MenuItem[] ContextMenuItems
        {
            get
            {
                var items = new List<MenuItem>();
                int count = SelectedObjects.Count();

                if (count > 0)
                {
                    items.Add(new OsuMenuItem($"Copy {count} overlay object{(count == 1 ? "" : "s")}", MenuItemType.Standard, CopySelection));
                    items.Add(new OsuMenuItem("Insert at the same time", MenuItemType.Highlighted, InsertSelection));
                    items.Add(new OsuMenuItem("Overlay this pattern at the current time", MenuItemType.Highlighted, OverlaySelectedPattern));
                    items.Add(new OsuMenuItem("Clear selection", MenuItemType.Standard, ClearSelection));
                }

                if (pattern.Value != null)
                    items.Add(new OsuMenuItem("Overlay the whole map again", MenuItemType.Standard, reference.ShowWholeMap));

                items.Add(new ToggleMenuItem("Highlight rhythm differences", MenuItemType.Standard, v => highlightDifferences.Value = v)
                {
                    State = { Value = highlightDifferences.Value }
                });

                return items.ToArray();
            }
        }

        // Keep the timeline from treating a drag here as scrolling.
        protected override bool OnMouseDown(MouseDownEvent e) => e.Button == MouseButton.Left && beatmap.Value != null;

        protected override bool OnDragStart(DragStartEvent e)
        {
            if (e.Button != MouseButton.Left || beatmap.Value == null)
                return false;

            dragAnchor = timeAt(e.ScreenSpaceMouseDownPosition);
            Select(dragAnchor, dragAnchor);
            return true;
        }

        protected override void OnDrag(DragEvent e) => Select(dragAnchor, timeAt(e.ScreenSpaceMousePosition));

        protected override bool OnClick(ClickEvent e)
        {
            ClearSelection();
            return true;
        }

        private double timeAt(Vector2 screenSpacePosition) => Content.ToLocalSpace(screenSpacePosition).X / Content.DrawWidth * Content.RelativeChildSize.X;

        #endregion

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (EditorBeatmap.IsNotNull())
            {
                EditorBeatmap.HitObjectAdded -= onEdited;
                EditorBeatmap.HitObjectRemoved -= onEdited;
                EditorBeatmap.HitObjectUpdated -= onEdited;
                EditorBeatmap.BeatmapReprocessed -= invalidateData;
            }
        }
    }
}
