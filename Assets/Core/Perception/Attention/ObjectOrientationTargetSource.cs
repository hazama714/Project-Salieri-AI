// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using UnityEngine;

using SalieriAI.Core.Perception.ObjectTargeting;

namespace SalieriAI.Core.Perception.Attention
{
    /// <summary>
    /// ObservationTargetSelectionServiceの物体対象を、
    /// OrientationTargetCandidateへ変換する。
    ///
    /// 注視対象の優先判定や、首・眼球制御は行わない。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ObjectOrientationTargetSource :
        MonoBehaviour
    {
        [Header("Source")]
        [SerializeField]
        private ObservationTargetSelectionService
            selectionService;

        private bool subscribed;

        public event Action<OrientationTargetCandidate>
            CandidateUpdated;

        public event Action<string>
            CandidateCleared;

        public OrientationTargetCandidate CurrentCandidate
        {
            get;
            private set;
        }

        public bool HasCandidate =>
            CurrentCandidate != null &&
            CurrentCandidate.IsValid;

        private void OnEnable()
        {
            if (selectionService == null)
            {
                Debug.LogError(
                    "[ObjectOrientationTargetSource][ERROR] " +
                    "ObservationTargetSelectionService is not assigned.",
                    this
                );

                enabled = false;
                return;
            }

            Subscribe();

            if (selectionService.CurrentTarget != null)
            {
                HandleTargetUpdated(
                    selectionService.CurrentTarget
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

            CandidateUpdated = null;
            CandidateCleared = null;
            CurrentCandidate = null;
        }

        private void Subscribe()
        {
            if (subscribed ||
                selectionService == null)
            {
                return;
            }

            selectionService.TargetUpdated +=
                HandleTargetUpdated;

            selectionService.TargetCleared +=
                HandleTargetCleared;

            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed ||
                selectionService == null)
            {
                return;
            }

            selectionService.TargetUpdated -=
                HandleTargetUpdated;

            selectionService.TargetCleared -=
                HandleTargetCleared;

            subscribed = false;
        }

        private void HandleTargetUpdated(
            ObservationTarget target
        )
        {
            if (target == null)
            {
                ClearCandidate(
                    "Observation target is null"
                );

                return;
            }

            long updatedAt =
                target.UpdatedAtUnixMilliseconds > 0
                    ? target.UpdatedAtUnixMilliseconds
                    : DateTimeOffset
                        .UtcNow
                        .ToUnixTimeMilliseconds();

            // Object detection/tracking coordinates use a top-left origin.
            // OrientationTargetCandidate uses Unity-style normalized coordinates
            // with a bottom-left origin, so only Y must be inverted here.
            float centerX =
                Mathf.Clamp01(
                    target.NormalizedCenterX
                );

            float centerY =
                Mathf.Clamp01(
                    1f - target.NormalizedCenterY
                );

            OrientationTargetCandidate candidate =
                new OrientationTargetCandidate(
                    OrientationTargetCandidateKind.Object,
                    target.TargetKey,
                    centerX,
                    centerY,
                    hasConfidence: true,
                    confidence: target.Confidence,
                    isStable: true,
                    updatedAtUnixMilliseconds: updatedAt,
                    label: target.ClassLabel,
                    sourceFrameId: target.SourceFrameId
                );

            if (!candidate.IsValid)
            {
                ClearCandidate(
                    "Converted object candidate is invalid"
                );

                return;
            }

            CurrentCandidate = candidate;

            try
            {
                CandidateUpdated?.Invoke(
                    candidate.Clone()
                );
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[ObjectOrientationTargetSource]" +
                    $"[SUBSCRIBER_ERROR] {ex}",
                    this
                );
            }
        }

        private void HandleTargetCleared(
            string reason
        )
        {
            ClearCandidate(
                string.IsNullOrWhiteSpace(reason)
                    ? "Observation target cleared"
                    : reason
            );
        }

        private void ClearCandidate(
            string reason
        )
        {
            if (CurrentCandidate == null)
                return;

            CurrentCandidate = null;

            try
            {
                CandidateCleared?.Invoke(reason);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[ObjectOrientationTargetSource]" +
                    $"[SUBSCRIBER_ERROR] {ex}",
                    this
                );
            }
        }
    }
}
