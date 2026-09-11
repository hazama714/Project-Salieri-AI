// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using UnityEngine;

namespace SalieriAI.Core.Perception.Attention
{
    /// <summary>
    /// FaceAttentionTargetDriverの顔対象を、
    /// OrientationTargetCandidateへ変換する。
    ///
    /// 注視優先順位の決定や、首・眼球制御は行わない。
    /// </summary>
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class FaceOrientationTargetSource : MonoBehaviour
    {
        private const string FaceTargetKey = "face:primary";

        [Header("Source")]
        [SerializeField]
        private global::FaceAttentionTargetDriver faceTargetDriver;

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
            if (faceTargetDriver == null)
            {
                Debug.LogError(
                    "[FaceOrientationTargetSource][ERROR] " +
                    "FaceAttentionTargetDriver is not assigned.",
                    this
                );

                enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (faceTargetDriver == null)
                return;

            if (!faceTargetDriver.HasValidTarget)
            {
                ClearCandidate("Face target fully lost");
                return;
            }

            UpdateCandidate();
        }

        private void OnDisable()
        {
            ClearCandidate("Face target source disabled");
        }

        private void OnDestroy()
        {
            CandidateUpdated = null;
            CandidateCleared = null;
            CurrentCandidate = null;
        }

        private void UpdateCandidate()
        {
            Vector2 center =
                faceTargetDriver.ScreenPosition;

            bool isStable =
                faceTargetDriver.IsDetected;

            long updatedAt;

            if (isStable || CurrentCandidate == null)
            {
                updatedAt = DateTimeOffset
                    .UtcNow
                    .ToUnixTimeMilliseconds();
            }
            else
            {
                // 一時消失中は、最後に実際に検出した時刻を保持する。
                updatedAt =
                    CurrentCandidate.UpdatedAtUnixMilliseconds;
            }

            OrientationTargetCandidate candidate =
                new OrientationTargetCandidate(
                    OrientationTargetCandidateKind.Face,
                    FaceTargetKey,
                    center.x,
                    center.y,
                    hasConfidence: false,
                    confidence: 0f,
                    isStable: isStable,
                    updatedAtUnixMilliseconds: updatedAt,
                    label: "face"
                );

            if (!candidate.IsValid)
            {
                ClearCandidate(
                    "Converted face candidate is invalid"
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
                    "[FaceOrientationTargetSource]" +
                    $"[SUBSCRIBER_ERROR] {ex}",
                    this
                );
            }
        }

        private void ClearCandidate(string reason)
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
                    "[FaceOrientationTargetSource]" +
                    $"[SUBSCRIBER_ERROR] {ex}",
                    this
                );
            }
        }
    }
}