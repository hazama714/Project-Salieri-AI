// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

using OpenCVForUnity.UnityIntegration.MOT;
using OpenCVForUnity.UnityIntegration.MOT.ByteTrack;
using SalieriAI.Core.Diagnostics.Performance;

using SalieriAI.Core.Perception.ObjectDetection;
using SalieriAI.Core.Perception.VisualSnapshots;

namespace SalieriAI.Core.Perception.ObjectTracking
{
    /// <summary>
    /// YoloXObjectDetectionServiceのVisibleObjectSetをBYTETrackerへ渡し、
    /// TrackId付きTrackedObjectSetとして公開する。
    ///
    /// このServiceは対象選択、発話、身体制御、DB保存を行わない。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ByteTrackObjectTrackingService :
        MonoBehaviour
    {
        [Header("Input")]
        [SerializeField]
        private YoloXObjectDetectionService detectionService;

        [Header("BYTETracker")]
        [Tooltip(
            "BYTETracker内部のlost保持数計算に使われる公称FPS。"
        )]
        [SerializeField]
        [Min(1)]
        private int trackerFrameRate = 30;

        [Tooltip(
            "Lost Trackを保持するTracker更新回数の基準値。"
        )]
        [SerializeField]
        [Min(1)]
        private int trackBuffer = 5;

        [SerializeField]
        [Range(0.01f, 1f)]
        private float trackThreshold = 0.50f;

        [SerializeField]
        [Range(0.01f, 1f)]
        private float highThreshold = 0.60f;

        [SerializeField]
        [Range(0.01f, 1f)]
        private float matchThreshold = 0.80f;

        [SerializeField]
        private bool mot20 = false;

        [Header("Stability")]
        [Tooltip(
            "この回数以上連続してActiveならStableとする。"
        )]
        [SerializeField]
        [Min(1)]
        private int minimumStableSnapshots = 3;

        [Tooltip(
            "ActiveでなくなったTrackの内部履歴を保持するSnapshot数。"
        )]
        [SerializeField]
        [Min(1)]
        private int staleStateRetentionSnapshots = 30;

        [Header("Debug")]
        [SerializeField]
        private bool logTrackSnapshots = true;

        [SerializeField]
        private bool logEmptyTrackSnapshots = false;

        [SerializeField]
        [Min(1)]
        private int maximumLoggedTracks = 5;

        private BYTETracker tracker;

        private bool initialized;
        private bool subscribed;
        private bool fatalError;

        private string currentSessionId;
        private long trackerFrameSequence;

        private readonly Dictionary<int, RuntimeTrackState>
            runtimeStates =
                new Dictionary<int, RuntimeTrackState>();

        private readonly Dictionary<int, string>
            classLabels =
                new Dictionary<int, string>();

        /// <summary>
        /// 新しい追跡Snapshotが作成されたときに通知する。
        /// </summary>
        public event Action<TrackedObjectSet>
            TracksUpdated;

        public bool IsReady =>
            initialized &&
            !fatalError;

        public TrackedObjectSet LatestSnapshot
        {
            get;
            private set;
        }

        public bool HasSnapshot =>
            LatestSnapshot != null;

        public bool TryGetVisualFrameSnapshot(
            string sessionId,
            long sourceFrameId,
            DateTime evaluatedAtUtc,
            out VisualFrameSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            if (detectionService == null)
            {
                error = "YoloXObjectDetectionService is unavailable.";
                return false;
            }

            return detectionService.TryGetVisualFrameSnapshot(
                sessionId,
                sourceFrameId,
                evaluatedAtUtc,
                out snapshot,
                out error);
        }

        private void Awake()
        {
            TryInitializeTracker();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void TryInitializeTracker()
        {
            if (detectionService == null)
            {
                FailInitialization(
                    "[ByteTrackObjectTrackingService][ERROR] " +
                    "YoloXObjectDetectionService is not assigned."
                );

                return;
            }

            if (highThreshold < trackThreshold)
            {
                FailInitialization(
                    "[ByteTrackObjectTrackingService][ERROR] " +
                    "High Threshold must be greater than or equal " +
                    "to Track Threshold."
                );

                return;
            }

            try
            {
                tracker =
                    new BYTETracker(
                        trackerFrameRate,
                        trackBuffer,
                        trackThreshold,
                        highThreshold,
                        matchThreshold,
                        mot20
                    );

                initialized = true;

                Debug.Log(
                    "[ByteTrackObjectTrackingService][READY] " +
                    $"FrameRate={trackerFrameRate} " +
                    $"TrackBuffer={trackBuffer} " +
                    $"TrackThreshold={trackThreshold:F2} " +
                    $"HighThreshold={highThreshold:F2} " +
                    $"MatchThreshold={matchThreshold:F2} " +
                    $"StableSnapshots={minimumStableSnapshots}",
                    this
                );
            }
            catch (Exception ex)
            {
                FailInitialization(
                    "[ByteTrackObjectTrackingService]" +
                    $"[INIT_ERROR] {ex}"
                );
            }
        }

        private void Subscribe()
        {
            if (subscribed ||
                detectionService == null ||
                !initialized)
            {
                return;
            }

            detectionService.SnapshotUpdated +=
                HandleDetectionSnapshot;

            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed ||
                detectionService == null)
            {
                return;
            }

            detectionService.SnapshotUpdated -=
                HandleDetectionSnapshot;

            subscribed = false;
        }

        private void HandleDetectionSnapshot(
            VisibleObjectSet snapshot
        )
        {
            if (!initialized ||
                fatalError ||
                tracker == null ||
                snapshot == null)
            {
                return;
            }

            long trackingStarted =
                SalieriRuntimePerformanceProbe.Timestamp();
            int inputCount = snapshot.Count;
            int outputCount = 0;

            try
            {
                EnsureSession(snapshot.SessionId);

                List<VisibleObject> acceptedDetections =
                    new List<VisibleObject>();

                BBox[] boxes =
                    BuildTrackerInput(
                        snapshot,
                        acceptedDetections
                    );

                tracker.Update(boxes);

                trackerFrameSequence++;

                int[] matchingTrackIds =
                    tracker.GetLastMatchingTrackIds();

                TrackInfo[] activeTracks =
                    tracker.GetActiveBasicTrackInfos();

                Dictionary<int, VisibleObject>
                    matchedDetections =
                        BuildMatchedDetectionMap(
                            acceptedDetections,
                            matchingTrackIds
                        );

                UpdateRuntimeStates(
                    snapshot,
                    activeTracks,
                    matchedDetections
                );

                TrackedObjectSet result =
                    BuildTrackedObjectSet(
                        snapshot,
                        activeTracks,
                        matchedDetections
                    );

                LatestSnapshot = result;
                outputCount = result == null ? 0 : result.Count;

                InvokeTracksUpdated(result);

                LogTrackSnapshot(result);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[ByteTrackObjectTrackingService]" +
                    $"[UPDATE_ERROR] {ex}",
                    this
                );
            }
            finally
            {
                SalieriRuntimePerformanceProbe.RecordDuration(
                    SalieriRuntimePerformanceMetric.ByteTrack,
                    trackingStarted,
                    inputCount,
                    outputCount);
            }
        }

        private void EnsureSession(
            string sessionId
        )
        {
            string safeSessionId =
                sessionId ?? string.Empty;

            if (currentSessionId == null)
            {
                currentSessionId = safeSessionId;
                return;
            }

            if (string.Equals(
                    currentSessionId,
                    safeSessionId,
                    StringComparison.Ordinal))
            {
                return;
            }

            ResetTrackingInternal(
                "Detection session changed"
            );

            currentSessionId = safeSessionId;
        }

        private BBox[] BuildTrackerInput(
            VisibleObjectSet snapshot,
            List<VisibleObject> acceptedDetections
        )
        {
            classLabels.Clear();

            if (snapshot.Objects == null ||
                snapshot.Objects.Length == 0)
            {
                return Array.Empty<BBox>();
            }

            List<BBox> boxes =
                new List<BBox>(
                    snapshot.Objects.Length
                );

            for (int i = 0;
                 i < snapshot.Objects.Length;
                 i++)
            {
                VisibleObject item =
                    snapshot.Objects[i];

                if (item == null)
                    continue;

                float width =
                    item.X2 - item.X1;

                float height =
                    item.Y2 - item.Y1;

                if (width <= 0f ||
                    height <= 0f)
                {
                    continue;
                }

                boxes.Add(
                    new BBox(
                        item.X1,
                        item.Y1,
                        width,
                        height,
                        item.Confidence,
                        item.ClassId
                    )
                );

                acceptedDetections.Add(item);

                if (!string.IsNullOrWhiteSpace(
                        item.ClassLabel))
                {
                    classLabels[item.ClassId] =
                        item.ClassLabel;
                }
            }

            return boxes.ToArray();
        }

        private Dictionary<int, VisibleObject>
            BuildMatchedDetectionMap(
                List<VisibleObject> detections,
                int[] matchingTrackIds
            )
        {
            Dictionary<int, VisibleObject> result =
                new Dictionary<int, VisibleObject>();

            if (detections == null ||
                matchingTrackIds == null)
            {
                return result;
            }

            int count =
                Mathf.Min(
                    detections.Count,
                    matchingTrackIds.Length
                );

            for (int i = 0; i < count; i++)
            {
                int trackId =
                    matchingTrackIds[i];

                // BYTETrackerでは新規Trackの最初の更新時は
                // -1が返るため、その時点では対応付けない。
                if (trackId < 0)
                    continue;

                VisibleObject detection =
                    detections[i];

                if (detection == null)
                    continue;

                if (result.TryGetValue(
                        trackId,
                        out VisibleObject existing))
                {
                    if (existing != null &&
                        existing.Confidence >=
                        detection.Confidence)
                    {
                        continue;
                    }
                }

                result[trackId] =
                    detection;
            }

            return result;
        }

        private void UpdateRuntimeStates(
            VisibleObjectSet snapshot,
            TrackInfo[] activeTracks,
            Dictionary<int, VisibleObject>
                matchedDetections
        )
        {
            HashSet<int> activeIds =
                new HashSet<int>();

            if (activeTracks != null)
            {
                for (int i = 0;
                     i < activeTracks.Length;
                     i++)
                {
                    TrackInfo track =
                        activeTracks[i];

                    int trackId =
                        track.TrackId;

                    activeIds.Add(trackId);

                    if (!runtimeStates.TryGetValue(
                            trackId,
                            out RuntimeTrackState state))
                    {
                        state =
                            new RuntimeTrackState
                            {
                                TrackId =
                                    trackId,

                                FirstSeenSourceFrameId =
                                    snapshot.FrameId,

                                LastSeenSourceFrameId =
                                    snapshot.FrameId,

                                LastSeenTrackerSequence =
                                    trackerFrameSequence,

                                ConsecutiveVisibleSnapshots =
                                    1,

                                TotalVisibleSnapshots =
                                    1
                            };

                        runtimeStates.Add(
                            trackId,
                            state
                        );
                    }
                    else
                    {
                        bool consecutive =
                            state.LastSeenTrackerSequence ==
                            trackerFrameSequence - 1;

                        state.ConsecutiveVisibleSnapshots =
                            consecutive
                                ? state
                                    .ConsecutiveVisibleSnapshots + 1
                                : 1;

                        state.TotalVisibleSnapshots++;

                        state.LastSeenSourceFrameId =
                            snapshot.FrameId;

                        state.LastSeenTrackerSequence =
                            trackerFrameSequence;
                    }

                    state.ClassId =
                        track.BBox.ClassId;

                    if (matchedDetections.TryGetValue(
                            trackId,
                            out VisibleObject detection) &&
                        detection != null)
                    {
                        state.LatestDetectionId =
                            detection.DetectionId;

                        state.ClassId =
                            detection.ClassId;

                        if (!string.IsNullOrWhiteSpace(
                                detection.ClassLabel))
                        {
                            state.ClassLabel =
                                detection.ClassLabel;
                        }
                    }
                    else if (classLabels.TryGetValue(
                                 track.BBox.ClassId,
                                 out string classLabel))
                    {
                        state.ClassLabel =
                            classLabel;
                    }
                }
            }

            RemoveStaleRuntimeStates();
        }

        private void RemoveStaleRuntimeStates()
        {
            if (runtimeStates.Count == 0)
                return;

            List<int> removeIds =
                null;

            foreach (
                KeyValuePair<int, RuntimeTrackState>
                    pair in runtimeStates)
            {
                long elapsed =
                    trackerFrameSequence -
                    pair.Value.LastSeenTrackerSequence;

                if (elapsed <=
                    staleStateRetentionSnapshots)
                {
                    continue;
                }

                if (removeIds == null)
                {
                    removeIds =
                        new List<int>();
                }

                removeIds.Add(pair.Key);
            }

            if (removeIds == null)
                return;

            for (int i = 0;
                 i < removeIds.Count;
                 i++)
            {
                runtimeStates.Remove(
                    removeIds[i]
                );
            }
        }

        private TrackedObjectSet
            BuildTrackedObjectSet(
                VisibleObjectSet source,
                TrackInfo[] activeTracks,
                Dictionary<int, VisibleObject>
                    matchedDetections
            )
        {
            List<TrackedObject> objects =
                new List<TrackedObject>();

            if (activeTracks != null)
            {
                for (int i = 0;
                     i < activeTracks.Length;
                     i++)
                {
                    TrackInfo trackInfo =
                        activeTracks[i];

                    BBox box =
                        trackInfo.BBox;

                    float x1 =
                        Mathf.Clamp(
                            box.X,
                            0f,
                            source.FrameWidth
                        );

                    float y1 =
                        Mathf.Clamp(
                            box.Y,
                            0f,
                            source.FrameHeight
                        );

                    float x2 =
                        Mathf.Clamp(
                            box.X + box.Width,
                            0f,
                            source.FrameWidth
                        );

                    float y2 =
                        Mathf.Clamp(
                            box.Y + box.Height,
                            0f,
                            source.FrameHeight
                        );

                    if (x2 <= x1 ||
                        y2 <= y1)
                    {
                        continue;
                    }

                    runtimeStates.TryGetValue(
                        trackInfo.TrackId,
                        out RuntimeTrackState state
                    );

                    matchedDetections.TryGetValue(
                        trackInfo.TrackId,
                        out VisibleObject detection
                    );

                    int classId =
                        state != null
                            ? state.ClassId
                            : box.ClassId;

                    string classLabel =
                        state != null
                            ? state.ClassLabel
                            : null;

                    if (string.IsNullOrWhiteSpace(
                            classLabel) &&
                        classLabels.TryGetValue(
                            classId,
                            out string knownLabel))
                    {
                        classLabel =
                            knownLabel;
                    }

                    int consecutive =
                        state != null
                            ? state
                                .ConsecutiveVisibleSnapshots
                            : 1;

                    int total =
                        state != null
                            ? state
                                .TotalVisibleSnapshots
                            : 1;

                    TrackedObject item =
                        new TrackedObject
                        {
                            TrackId =
                                trackInfo.TrackId,

                            TrackKey =
                                $"{source.SessionId}:" +
                                $"track:" +
                                $"{trackInfo.TrackId}",

                            LatestDetectionId =
                                detection != null
                                    ? detection.DetectionId
                                    : state
                                        ?.LatestDetectionId,

                            ClassId =
                                classId,

                            ClassLabel =
                                classLabel,

                            Confidence =
                                detection != null
                                    ? detection.Confidence
                                    : box.Score,

                            X1 = x1,
                            Y1 = y1,
                            X2 = x2,
                            Y2 = y2,

                            NormalizedX1 =
                                x1 /
                                source.FrameWidth,

                            NormalizedY1 =
                                y1 /
                                source.FrameHeight,

                            NormalizedX2 =
                                x2 /
                                source.FrameWidth,

                            NormalizedY2 =
                                y2 /
                                source.FrameHeight,

                            FirstSeenSourceFrameId =
                                state != null
                                    ? state
                                        .FirstSeenSourceFrameId
                                    : source.FrameId,

                            LastSeenSourceFrameId =
                                state != null
                                    ? state
                                        .LastSeenSourceFrameId
                                    : source.FrameId,

                            ConsecutiveVisibleSnapshots =
                                consecutive,

                            TotalVisibleSnapshots =
                                total,

                            IsStable =
                                consecutive >=
                                minimumStableSnapshots
                        };

                    objects.Add(item);
                }
            }

            objects.Sort(
                CompareTrackedObjects
            );

            return new TrackedObjectSet
            {
                SessionId =
                    source.SessionId,

                SourceFrameId =
                    source.FrameId,

                TrackerFrameSequence =
                    trackerFrameSequence,

                CapturedAtUnixMilliseconds =
                    source
                        .CapturedAtUnixMilliseconds,

                FrameWidth =
                    source.FrameWidth,

                FrameHeight =
                    source.FrameHeight,

                Rotation =
                    source.Rotation,

                Objects =
                    objects.ToArray()
            };
        }

        private static int CompareTrackedObjects(
            TrackedObject left,
            TrackedObject right
        )
        {
            if (ReferenceEquals(left, right))
                return 0;

            if (left == null)
                return 1;

            if (right == null)
                return -1;

            int stableComparison =
                right
                    .IsStable
                    .CompareTo(
                        left.IsStable
                    );

            if (stableComparison != 0)
                return stableComparison;

            int confidenceComparison =
                right
                    .Confidence
                    .CompareTo(
                        left.Confidence
                    );

            if (confidenceComparison != 0)
                return confidenceComparison;

            return left
                .TrackId
                .CompareTo(
                    right.TrackId
                );
        }

        private void InvokeTracksUpdated(
            TrackedObjectSet snapshot
        )
        {
            try
            {
                TracksUpdated?.Invoke(snapshot);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[ByteTrackObjectTrackingService]" +
                    $"[SUBSCRIBER_ERROR] {ex}",
                    this
                );
            }
        }

        private void LogTrackSnapshot(
            TrackedObjectSet snapshot
        )
        {
            if (!logTrackSnapshots ||
                snapshot == null)
            {
                return;
            }

            if (snapshot.Count == 0)
            {
                if (logEmptyTrackSnapshots)
                {
                    Debug.Log(
                        "[ByteTrackObjectTrackingService]" +
                        "[TRACKS] " +
                        $"SourceFrameId=" +
                        $"{snapshot.SourceFrameId} " +
                        $"TrackerFrame=" +
                        $"{snapshot.TrackerFrameSequence} " +
                        "Count=0",
                        this
                    );
                }

                return;
            }

            int count =
                Mathf.Min(
                    snapshot.Count,
                    maximumLoggedTracks
                );

            StringBuilder builder =
                new StringBuilder(512);

            builder.Append(
                "[ByteTrackObjectTrackingService]" +
                "[TRACKS] " +
                $"SourceFrameId=" +
                $"{snapshot.SourceFrameId} " +
                $"TrackerFrame=" +
                $"{snapshot.TrackerFrameSequence} " +
                $"Count={snapshot.Count} " +
                $"Stable={snapshot.StableCount}"
            );

            for (int i = 0;
                 i < count;
                 i++)
            {
                TrackedObject item =
                    snapshot.Objects[i];

                builder.AppendLine();

                builder.Append(
                    $"  #{i + 1} " +
                    $"TrackId={item.TrackId} " +
                    $"Label={item.ClassLabel} " +
                    $"Confidence={item.Confidence:F3} " +
                    $"Consecutive=" +
                    $"{item.ConsecutiveVisibleSnapshots} " +
                    $"Stable={item.IsStable} " +
                    $"Center=(" +
                    $"{item.NormalizedCenterX:F3}," +
                    $"{item.NormalizedCenterY:F3}) " +
                    $"Size=(" +
                    $"{item.NormalizedWidth:F3}," +
                    $"{item.NormalizedHeight:F3})"
                );
            }

            Debug.Log(
                builder.ToString(),
                this
            );
        }

        public void ResetTracking()
        {
            ResetTrackingInternal(
                "Manual reset"
            );
        }

        private void ResetTrackingInternal(
            string reason
        )
        {
            if (tracker != null)
            {
                tracker.Reset();
            }

            runtimeStates.Clear();
            classLabels.Clear();

            LatestSnapshot = null;

            trackerFrameSequence = 0;
            currentSessionId = null;

            Debug.Log(
                "[ByteTrackObjectTrackingService]" +
                $"[RESET] Reason={reason}",
                this
            );
        }

        private void FailInitialization(
            string message
        )
        {
            fatalError = true;
            initialized = false;
            enabled = false;

            Debug.LogError(
                message,
                this
            );
        }

        private void OnDestroy()
        {
            Unsubscribe();

            TracksUpdated = null;

            runtimeStates.Clear();
            classLabels.Clear();

            tracker?.Dispose();
            tracker = null;

            initialized = false;
        }

        private sealed class RuntimeTrackState
        {
            public int TrackId;

            public int ClassId;
            public string ClassLabel;

            public string LatestDetectionId;

            public long FirstSeenSourceFrameId;
            public long LastSeenSourceFrameId;
            public long LastSeenTrackerSequence;

            public int ConsecutiveVisibleSnapshots;
            public int TotalVisibleSnapshots;
        }
    }
}
