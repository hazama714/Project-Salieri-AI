// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using UnityEngine;

using SalieriAI.Core.Perception.ObjectTracking;
using SalieriAI.Core.Perception.VisualSnapshots;

namespace SalieriAI.Core.Perception.ObjectTargeting
{
    /// <summary>
    /// TrackedObjectSetから観察対象を1件選択する。
    ///
    /// 初期v1では以下を決定論的に評価する。
    ///
    /// ・Stable Trackだけを対象にする
    /// ・除外ラベルを対象にしない
    /// ・小さすぎる／大きすぎる矩形を除外する
    /// ・画面中央、信頼度、面積、継続性を評価する
    /// ・現在対象を簡単には切り替えない
    ///
    /// UI、首、眼球、発話、DBは操作しない。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ObservationTargetSelectionService :
        MonoBehaviour
    {
        [Header("Input")]
        [SerializeField]
        private ByteTrackObjectTrackingService
            trackingService;

        [Header("Eligibility")]
        [SerializeField]
        [Range(0f, 1f)]
        private float minimumConfidence = 0.50f;

        [Tooltip(
            "一度選択された現在対象を維持できる最低Confidence。"
        )]
        [SerializeField]
        [Range(0f, 1f)]
        private float minimumMaintainedConfidence = 0.40f;

        [Tooltip(
            "画面全体に対する最小面積。0.005は画面の0.5%。"
        )]
        [SerializeField]
        [Range(0f, 1f)]
        private float minimumNormalizedArea = 0.005f;

        [Tooltip(
            "画面全体に対する最大面積。巨大な誤検出を除外する。"
        )]
        [SerializeField]
        [Range(0f, 1f)]
        private float maximumNormalizedArea = 0.75f;

        [Tooltip(
            "初期観察対象から除外するCOCOラベル。"
        )]
        [SerializeField]
        private string[] excludedLabels =
        {
            "person"
        };

        [Header("Scoring")]
        [SerializeField]
        [Min(0f)]
        private float centralityWeight = 0.45f;

        [SerializeField]
        [Min(0f)]
        private float confidenceWeight = 0.30f;

        [SerializeField]
        [Min(0f)]
        private float areaWeight = 0.15f;

        [SerializeField]
        [Min(0f)]
        private float persistenceWeight = 0.10f;

        [Tooltip(
            "この面積以上はAreaScore=1とする。"
        )]
        [SerializeField]
        [Range(0.001f, 1f)]
        private float preferredAreaReference = 0.15f;

        [Tooltip(
            "この連続確認数以上はPersistenceScore=1とする。"
        )]
        [SerializeField]
        [Min(1)]
        private int fullPersistenceSnapshots = 10;

        [Header("Switch Control")]
        [Tooltip(
            "現在対象よりこの値以上高得点でなければ切り替えない。"
        )]
        [SerializeField]
        [Range(0f, 1f)]
        private float switchScoreMargin = 0.08f;

        [Header("Debug")]
        [SerializeField]
        private bool logSelectionChanges = true;

        [SerializeField]
        private bool logTargetUpdates = false;

        [SerializeField]
        private bool logNoCandidate = false;

        private bool subscribed;

        public event Action<ObservationTarget>
            TargetChanged;

        public event Action<ObservationTarget>
            TargetUpdated;

        public event Action<string>
            TargetCleared;

        public ObservationTarget CurrentTarget
        {
            get;
            private set;
        }

        public bool HasTarget =>
            CurrentTarget != null;

        public bool TryGetVisualFrameSnapshot(
            string sessionId,
            long sourceFrameId,
            DateTime evaluatedAtUtc,
            out VisualFrameSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            if (trackingService == null)
            {
                error = "ByteTrackObjectTrackingService is unavailable.";
                return false;
            }

            return trackingService.TryGetVisualFrameSnapshot(
                sessionId,
                sourceFrameId,
                evaluatedAtUtc,
                out snapshot,
                out error);
        }

        private void OnEnable()
        {
            if (trackingService == null)
            {
                Debug.LogError(
                    "[ObservationTargetSelectionService][ERROR] " +
                    "ByteTrackObjectTrackingService is not assigned.",
                    this
                );

                enabled = false;
                return;
            }

            Subscribe();

            if (trackingService.HasSnapshot)
            {
                HandleTracksUpdated(
                    trackingService.LatestSnapshot
                );
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();

            TargetChanged = null;
            TargetUpdated = null;
            TargetCleared = null;

            CurrentTarget = null;
        }

        private void OnValidate()
        {
            minimumMaintainedConfidence =
                Mathf.Clamp(
                    minimumMaintainedConfidence,
                    0f,
                    minimumConfidence
                );

            minimumNormalizedArea =
                Mathf.Clamp01(
                    minimumNormalizedArea
                );

            maximumNormalizedArea =
                Mathf.Clamp(
                    maximumNormalizedArea,
                    minimumNormalizedArea,
                    1f
                );

            preferredAreaReference =
                Mathf.Max(
                    0.001f,
                    preferredAreaReference
                );

            fullPersistenceSnapshots =
                Mathf.Max(
                    1,
                    fullPersistenceSnapshots
                );

            centralityWeight =
                Mathf.Max(
                    0f,
                    centralityWeight
                );

            confidenceWeight =
                Mathf.Max(
                    0f,
                    confidenceWeight
                );

            areaWeight =
                Mathf.Max(
                    0f,
                    areaWeight
                );

            persistenceWeight =
                Mathf.Max(
                    0f,
                    persistenceWeight
                );
        }

        private void Subscribe()
        {
            if (subscribed ||
                trackingService == null)
            {
                return;
            }

            trackingService.TracksUpdated +=
                HandleTracksUpdated;

            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed ||
                trackingService == null)
            {
                return;
            }

            trackingService.TracksUpdated -=
                HandleTracksUpdated;

            subscribed = false;
        }

        private void HandleTracksUpdated(
            TrackedObjectSet snapshot
        )
        {
            if (snapshot == null)
            {
                ClearCurrentTarget(
                    "Tracking snapshot is null"
                );

                return;
            }

            bool hadCurrentTarget =
                CurrentTarget != null;

            CandidateEvaluation current =
                default;

            bool currentStillEligible =
                false;

            if (hadCurrentTarget)
            {
                currentStillEligible =
                    TryFindCandidateByKey(
                        snapshot,
                        CurrentTarget.TargetKey,
                        out current
                    );
            }

            CandidateEvaluation best =
                default;

            bool hasBestNewCandidate =
                TryFindBestCandidate(
                    snapshot,
                    out best
                );

            /*
             * 現在対象を維持できる場合。
             */
            if (currentStillEligible)
            {
                CandidateEvaluation selected =
                    current;

                ObservationTargetSelectionReason reason =
                    ObservationTargetSelectionReason
                        .MaintainedCurrentTarget;

                if (hasBestNewCandidate)
                {
                    bool sameAsBest =
                        string.Equals(
                            current.Track.TrackKey,
                            best.Track.TrackKey,
                            StringComparison.Ordinal
                        );

                    if (!sameAsBest &&
                        best.TotalScore >=
                        current.TotalScore +
                        switchScoreMargin)
                    {
                        selected = best;

                        reason =
                            ObservationTargetSelectionReason
                                .SwitchedForHigherScore;
                    }
                }

                PublishSelectedTarget(
                    snapshot,
                    selected,
                    reason
                );

                return;
            }

            /*
             * 現在対象がない、または維持不能で、
             * 新規候補が存在する場合。
             */
            if (hasBestNewCandidate)
            {
                ObservationTargetSelectionReason reason =
                    hadCurrentTarget
                        ? ObservationTargetSelectionReason
                            .ReplacedUnavailableTarget
                        : ObservationTargetSelectionReason
                            .InitialSelection;

                PublishSelectedTarget(
                    snapshot,
                    best,
                    reason
                );

                return;
            }

            /*
             * 維持可能な現在対象も新規候補もない。
             */
            ClearCurrentTarget(
                "No eligible stable track"
            );

            if (logNoCandidate)
            {
                Debug.Log(
                    "[ObservationTargetSelectionService]" +
                    "[NO_CANDIDATE] " +
                    $"SourceFrameId={snapshot.SourceFrameId}",
                    this
                );
            }
        }

        private bool TryFindBestCandidate(
            TrackedObjectSet snapshot,
            out CandidateEvaluation best)
        {
            best = default;
            bool found = false;

            if (snapshot.Objects == null)
                return false;

            for (int i = 0;
                 i < snapshot.Objects.Length;
                 i++)
            {
                TrackedObject item =
                    snapshot.Objects[i];

                if (!IsEligibleForNewSelection(item))
                    continue;

                CandidateEvaluation candidate =
                    Evaluate(item);

                if (!found ||
                    IsBetterCandidate(
                        candidate,
                        best))
                {
                    best = candidate;
                    found = true;
                }
            }

            return found;
        }

        private bool TryFindCandidateByKey(
            TrackedObjectSet snapshot,
            string targetKey,
            out CandidateEvaluation result)
        {
            result = default;

            if (snapshot.Objects == null ||
                string.IsNullOrWhiteSpace(targetKey))
            {
                return false;
            }

            for (int i = 0;
                 i < snapshot.Objects.Length;
                 i++)
            {
                TrackedObject item =
                    snapshot.Objects[i];

                if (item == null ||
                    !string.Equals(
                        item.TrackKey,
                        targetKey,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (!IsEligibleForMaintainingCurrentTarget(item))
                    return false;

                result = Evaluate(item);
                return true;
            }

            return false;
        }

        private bool IsEligibleForNewSelection(
            TrackedObject item
        )
        {
            return IsEligible(
                item,
                minimumConfidence
            );
        }

        private bool IsEligibleForMaintainingCurrentTarget(
            TrackedObject item
        )
        {
            return IsEligible(
                item,
                minimumMaintainedConfidence
            );
        }

        private bool IsEligible(
            TrackedObject item,
            float confidenceThreshold
        )
        {
            if (item == null)
                return false;

            if (!item.IsStable)
                return false;

            if (item.Confidence <
                confidenceThreshold)
            {
                return false;
            }

            float area =
                item.NormalizedArea;

            if (area <
                    minimumNormalizedArea ||
                area >
                    maximumNormalizedArea)
            {
                return false;
            }

            if (IsExcludedLabel(
                    item.ClassLabel))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(
                    item.TrackKey))
            {
                return false;
            }

            return true;
        }

        private bool IsExcludedLabel(
            string classLabel
        )
        {
            if (excludedLabels == null ||
                excludedLabels.Length == 0 ||
                string.IsNullOrWhiteSpace(
                    classLabel))
            {
                return false;
            }

            for (int i = 0;
                 i < excludedLabels.Length;
                 i++)
            {
                string excluded =
                    excludedLabels[i];

                if (string.IsNullOrWhiteSpace(
                        excluded))
                {
                    continue;
                }

                if (string.Equals(
                        classLabel.Trim(),
                        excluded.Trim(),
                        StringComparison
                            .OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private CandidateEvaluation Evaluate(
            TrackedObject item
        )
        {
            float deltaX =
                item.NormalizedCenterX -
                0.5f;

            float deltaY =
                item.NormalizedCenterY -
                0.5f;

            float distanceFromCenter =
                Mathf.Sqrt(
                    deltaX * deltaX +
                    deltaY * deltaY
                );

            const float maximumCenterDistance =
                0.70710678f;

            float centralityScore =
                1f -
                Mathf.Clamp01(
                    distanceFromCenter /
                    maximumCenterDistance
                );

            float confidenceScore =
                Mathf.Clamp01(
                    item.Confidence
                );

            float areaScore =
                Mathf.Clamp01(
                    item.NormalizedArea /
                    preferredAreaReference
                );

            float persistenceScore =
                Mathf.Clamp01(
                    item.ConsecutiveVisibleSnapshots /
                    (float)fullPersistenceSnapshots
                );

            float totalWeight =
                centralityWeight +
                confidenceWeight +
                areaWeight +
                persistenceWeight;

            float weightedScore =
                centralityScore *
                    centralityWeight +
                confidenceScore *
                    confidenceWeight +
                areaScore *
                    areaWeight +
                persistenceScore *
                    persistenceWeight;

            float totalScore =
                totalWeight > 0f
                    ? weightedScore /
                      totalWeight
                    : 0f;

            return new CandidateEvaluation
            {
                Track = item,

                TotalScore =
                    Mathf.Clamp01(
                        totalScore
                    ),

                CentralityScore =
                    centralityScore,

                ConfidenceScore =
                    confidenceScore,

                AreaScore =
                    areaScore,

                PersistenceScore =
                    persistenceScore
            };
        }

        private static bool IsBetterCandidate(
            CandidateEvaluation candidate,
            CandidateEvaluation currentBest)
        {
            const float epsilon = 0.0001f;

            if (candidate.TotalScore >
                currentBest.TotalScore +
                epsilon)
            {
                return true;
            }

            if (candidate.TotalScore <
                currentBest.TotalScore -
                epsilon)
            {
                return false;
            }

            if (candidate.Track.Confidence >
                currentBest.Track.Confidence +
                epsilon)
            {
                return true;
            }

            if (candidate.Track.Confidence <
                currentBest.Track.Confidence -
                epsilon)
            {
                return false;
            }

            if (candidate.Track
                    .ConsecutiveVisibleSnapshots >
                currentBest.Track
                    .ConsecutiveVisibleSnapshots)
            {
                return true;
            }

            if (candidate.Track
                    .ConsecutiveVisibleSnapshots <
                currentBest.Track
                    .ConsecutiveVisibleSnapshots)
            {
                return false;
            }

            if (candidate.Track.NormalizedArea >
                currentBest.Track.NormalizedArea +
                epsilon)
            {
                return true;
            }

            if (candidate.Track.NormalizedArea <
                currentBest.Track.NormalizedArea -
                epsilon)
            {
                return false;
            }

            // 完全同点時は小さいTrackIdを優先し、
            // 実行ごとの結果を決定論的に固定する。
            return candidate.Track.TrackId <
                   currentBest.Track.TrackId;
        }

        private void PublishSelectedTarget(
            TrackedObjectSet source,
            CandidateEvaluation selected,
            ObservationTargetSelectionReason reason)
        {
            long now =
                DateTimeOffset
                    .UtcNow
                    .ToUnixTimeMilliseconds();

            bool sameTarget =
                CurrentTarget != null &&
                string.Equals(
                    CurrentTarget.TargetKey,
                    selected.Track.TrackKey,
                    StringComparison.Ordinal
                ) &&
                string.Equals(
                    CurrentTarget.SessionId,
                    source.SessionId,
                    StringComparison.Ordinal
                );

            long selectedAt =
                sameTarget
                    ? CurrentTarget
                        .SelectedAtUnixMilliseconds
                    : now;

            ObservationTarget next =
                new ObservationTarget
                {
                    SessionId =
                        source.SessionId,

                    TargetKey =
                        selected.Track.TrackKey,

                    SourceFrameId =
                        source.SourceFrameId,

                    SelectedAtUnixMilliseconds =
                        selectedAt,

                    UpdatedAtUnixMilliseconds =
                        source
                            .CapturedAtUnixMilliseconds > 0
                            ? source
                                .CapturedAtUnixMilliseconds
                            : now,

                    SelectionReason =
                        reason,

                    SelectionScore =
                        selected.TotalScore,

                    CentralityScore =
                        selected.CentralityScore,

                    ConfidenceScore =
                        selected.ConfidenceScore,

                    AreaScore =
                        selected.AreaScore,

                    PersistenceScore =
                        selected.PersistenceScore,

                    TrackSnapshot =
                        CloneTrackedObject(
                            selected.Track
                        )
                };

            CurrentTarget = next;

            if (sameTarget)
            {
                InvokeTargetUpdated(next);

                if (logTargetUpdates)
                {
                    LogTarget(
                        "UPDATED",
                        next
                    );
                }

                return;
            }

            InvokeTargetChanged(next);
            InvokeTargetUpdated(next);

            if (logSelectionChanges)
            {
                LogTarget(
                    "SELECTED",
                    next
                );
            }
        }

        private void ClearCurrentTarget(
            string reason
        )
        {
            if (CurrentTarget == null)
                return;

            string previousKey =
                CurrentTarget.TargetKey;

            CurrentTarget = null;

            try
            {
                TargetCleared?.Invoke(reason);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[ObservationTargetSelectionService]" +
                    $"[SUBSCRIBER_ERROR] {ex}",
                    this
                );
            }

            if (logSelectionChanges)
            {
                Debug.Log(
                    "[ObservationTargetSelectionService]" +
                    "[CLEARED] " +
                    $"PreviousKey={previousKey} " +
                    $"Reason={reason}",
                    this
                );
            }
        }

        private void InvokeTargetChanged(
            ObservationTarget target
        )
        {
            try
            {
                TargetChanged?.Invoke(target);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[ObservationTargetSelectionService]" +
                    $"[SUBSCRIBER_ERROR] {ex}",
                    this
                );
            }
        }

        private void InvokeTargetUpdated(
            ObservationTarget target
        )
        {
            try
            {
                TargetUpdated?.Invoke(target);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[ObservationTargetSelectionService]" +
                    $"[SUBSCRIBER_ERROR] {ex}",
                    this
                );
            }
        }

        private void LogTarget(
            string operation,
            ObservationTarget target
        )
        {
            Debug.Log(
                "[ObservationTargetSelectionService]" +
                $"[{operation}] " +
                $"SourceFrameId={target.SourceFrameId} " +
                $"TargetKey={target.TargetKey} " +
                $"TrackId={target.TrackId} " +
                $"Label={target.ClassLabel} " +
                $"Confidence={target.Confidence:F3} " +
                $"Score={target.SelectionScore:F3} " +
                $"Center=(" +
                $"{target.NormalizedCenterX:F3}," +
                $"{target.NormalizedCenterY:F3}) " +
                $"Reason={target.SelectionReason}",
                this
            );
        }

        private static TrackedObject
            CloneTrackedObject(
                TrackedObject source
            )
        {
            if (source == null)
                return null;

            return new TrackedObject
            {
                TrackId =
                    source.TrackId,

                TrackKey =
                    source.TrackKey,

                LatestDetectionId =
                    source.LatestDetectionId,

                ClassId =
                    source.ClassId,

                ClassLabel =
                    source.ClassLabel,

                Confidence =
                    source.Confidence,

                X1 = source.X1,
                Y1 = source.Y1,
                X2 = source.X2,
                Y2 = source.Y2,

                NormalizedX1 =
                    source.NormalizedX1,

                NormalizedY1 =
                    source.NormalizedY1,

                NormalizedX2 =
                    source.NormalizedX2,

                NormalizedY2 =
                    source.NormalizedY2,

                FirstSeenSourceFrameId =
                    source
                        .FirstSeenSourceFrameId,

                LastSeenSourceFrameId =
                    source
                        .LastSeenSourceFrameId,

                ConsecutiveVisibleSnapshots =
                    source
                        .ConsecutiveVisibleSnapshots,

                TotalVisibleSnapshots =
                    source
                        .TotalVisibleSnapshots,

                IsStable =
                    source.IsStable
            };
        }

        private struct CandidateEvaluation
        {
            public TrackedObject Track;

            public float TotalScore;

            public float CentralityScore;

            public float ConfidenceScore;

            public float AreaScore;

            public float PersistenceScore;
        }
    }
}
