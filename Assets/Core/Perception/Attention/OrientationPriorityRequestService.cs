// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SalieriAI.Core.Perception.Attention
{
    /// <summary>
    /// Mode、Skill、会話制御などから送られる注視優先要求を管理する。
    ///
    /// 同じSourceKeyからの要求は、新しい要求で置き換える。
    /// 現在有効な要求のうち、最も優先度が高い1件を公開する。
    ///
    /// このサービスは首・眼球・サーボを操作しない。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OrientationPriorityRequestService :
        MonoBehaviour
    {
        [Header("Expiry")]
        [SerializeField]
        [Min(0.05f)]
        private float expiryCheckIntervalSeconds = 0.10f;

        private readonly List<OrientationPriorityRequest>
            activeRequests =
                new List<OrientationPriorityRequest>();

        private float nextExpiryCheckTime;

        public event Action<OrientationPriorityRequest>
            ActiveRequestChanged;

        public event Action<string>
            ActiveRequestCleared;

        public OrientationPriorityRequest ActiveRequest
        {
            get;
            private set;
        }

        public bool HasActiveRequest =>
            ActiveRequest != null &&
            ActiveRequest.IsValid;

        public int ActiveRequestCount =>
            activeRequests.Count;

        private void OnEnable()
        {
            nextExpiryCheckTime =
                Time.unscaledTime +
                expiryCheckIntervalSeconds;

            RemoveExpiredRequests();
            RecalculateActiveRequest(
                "Service enabled"
            );
        }

        private void Update()
        {
            if (Time.unscaledTime <
                nextExpiryCheckTime)
            {
                return;
            }

            nextExpiryCheckTime =
                Time.unscaledTime +
                expiryCheckIntervalSeconds;

            if (RemoveExpiredRequests())
            {
                RecalculateActiveRequest(
                    "Expired request removed"
                );
            }
        }

        private void OnDisable()
        {
            ClearAll(
                "Priority request service disabled"
            );
        }

        private void OnDestroy()
        {
            activeRequests.Clear();
            ActiveRequest = null;

            ActiveRequestChanged = null;
            ActiveRequestCleared = null;
        }

        private void OnValidate()
        {
            if (expiryCheckIntervalSeconds < 0.05f)
            {
                expiryCheckIntervalSeconds = 0.05f;
            }
        }

        /// <summary>
        /// 要求を追加する。
        ///
        /// 同じSourceKeyの既存要求は削除され、
        /// 新しい要求へ置き換えられる。
        /// </summary>
        public bool SubmitOrReplace(
            OrientationPriorityRequest request,
            out string error)
        {
            error = string.Empty;

            if (request == null)
            {
                error =
                    "OrientationPriorityRequest is null.";

                return false;
            }

            if (!request.IsValid)
            {
                error =
                    "OrientationPriorityRequest is invalid.";

                return false;
            }

            long now =
                DateTimeOffset
                    .UtcNow
                    .ToUnixTimeMilliseconds();

            if (request.IsExpired(now))
            {
                error =
                    "OrientationPriorityRequest is already expired.";

                return false;
            }

            RemoveBySourceKeyInternal(
                request.SourceKey
            );

            activeRequests.Add(
                request.Clone()
            );

            RemoveExpiredRequests();

            RecalculateActiveRequest(
                $"Submitted by {request.SourceKey}"
            );

            return true;
        }

        public bool RemoveByRequestId(
            string requestId)
        {
            if (string.IsNullOrWhiteSpace(
                    requestId))
            {
                return false;
            }

            bool removed = false;

            for (int i =
                     activeRequests.Count - 1;
                 i >= 0;
                 i--)
            {
                if (!string.Equals(
                        activeRequests[i].RequestId,
                        requestId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                activeRequests.RemoveAt(i);
                removed = true;
            }

            if (removed)
            {
                RecalculateActiveRequest(
                    $"Removed request {requestId}"
                );
            }

            return removed;
        }

        public bool RemoveBySourceKey(
            string sourceKey)
        {
            if (string.IsNullOrWhiteSpace(
                    sourceKey))
            {
                return false;
            }

            bool removed =
                RemoveBySourceKeyInternal(
                    sourceKey
                );

            if (removed)
            {
                RecalculateActiveRequest(
                    $"Removed source {sourceKey}"
                );
            }

            return removed;
        }

        public void ClearAll(string reason)
        {
            bool hadRequests =
                activeRequests.Count > 0;

            bool hadActiveRequest =
                ActiveRequest != null;

            activeRequests.Clear();
            ActiveRequest = null;

            if (!hadRequests &&
                !hadActiveRequest)
            {
                return;
            }

            InvokeActiveRequestCleared(
                string.IsNullOrWhiteSpace(reason)
                    ? "All priority requests cleared"
                    : reason
            );
        }

        private bool RemoveBySourceKeyInternal(
            string sourceKey)
        {
            bool removed = false;

            for (int i =
                     activeRequests.Count - 1;
                 i >= 0;
                 i--)
            {
                if (!string.Equals(
                        activeRequests[i].SourceKey,
                        sourceKey,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                activeRequests.RemoveAt(i);
                removed = true;
            }

            return removed;
        }

        private bool RemoveExpiredRequests()
        {
            long now =
                DateTimeOffset
                    .UtcNow
                    .ToUnixTimeMilliseconds();

            bool removed = false;

            for (int i =
                     activeRequests.Count - 1;
                 i >= 0;
                 i--)
            {
                OrientationPriorityRequest request =
                    activeRequests[i];

                if (request != null &&
                    request.IsValid &&
                    !request.IsExpired(now))
                {
                    continue;
                }

                activeRequests.RemoveAt(i);
                removed = true;
            }

            return removed;
        }

        private void RecalculateActiveRequest(
            string reason)
        {
            OrientationPriorityRequest best =
                FindBestRequest();

            if (AreEquivalent(
                    ActiveRequest,
                    best))
            {
                return;
            }

            ActiveRequest =
                best != null
                    ? best.Clone()
                    : null;

            if (ActiveRequest != null)
            {
                InvokeActiveRequestChanged(
                    ActiveRequest.Clone()
                );
            }
            else
            {
                InvokeActiveRequestCleared(
                    reason
                );
            }
        }

        private OrientationPriorityRequest
            FindBestRequest()
        {
            OrientationPriorityRequest best =
                null;

            for (int i = 0;
                 i < activeRequests.Count;
                 i++)
            {
                OrientationPriorityRequest candidate =
                    activeRequests[i];

                if (candidate == null ||
                    !candidate.IsValid)
                {
                    continue;
                }

                if (best == null ||
                    IsHigherPriority(
                        candidate,
                        best))
                {
                    best = candidate;
                }
            }

            return best;
        }

        private static bool IsHigherPriority(
            OrientationPriorityRequest candidate,
            OrientationPriorityRequest current)
        {
            if (candidate.Priority !=
                current.Priority)
            {
                return candidate.Priority >
                       current.Priority;
            }

            if (candidate.CreatedAtUnixMilliseconds !=
                current.CreatedAtUnixMilliseconds)
            {
                return
                    candidate.CreatedAtUnixMilliseconds >
                    current.CreatedAtUnixMilliseconds;
            }

            return string.CompareOrdinal(
                       candidate.RequestId,
                       current.RequestId
                   ) < 0;
        }

        private static bool AreEquivalent(
            OrientationPriorityRequest left,
            OrientationPriorityRequest right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (left == null || right == null)
                return false;

            return
                string.Equals(
                    left.RequestId,
                    right.RequestId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    left.SourceKey,
                    right.SourceKey,
                    StringComparison.Ordinal) &&
                left.Directive ==
                right.Directive &&
                left.Reason ==
                right.Reason &&
                left.Priority ==
                right.Priority &&
                left.CreatedAtUnixMilliseconds ==
                right.CreatedAtUnixMilliseconds &&
                left.ExpiresAtUnixMilliseconds ==
                right.ExpiresAtUnixMilliseconds &&
                string.Equals(
                    left.HeldTargetKey,
                    right.HeldTargetKey,
                    StringComparison.Ordinal) &&
                AreEquivalentTargetDirections(
                    left.TargetDirection,
                    right.TargetDirection);
        }

        private static bool AreEquivalentTargetDirections(
            OrientationTargetDirectionRequest left,
            OrientationTargetDirectionRequest right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (left == null || right == null)
                return false;

            return
                string.Equals(
                    left.TargetKey,
                    right.TargetKey,
                    StringComparison.Ordinal) &&
                left.BodyRelativeYawDegrees.Equals(
                    right.BodyRelativeYawDegrees) &&
                left.BodyRelativePitchDegrees.Equals(
                    right.BodyRelativePitchDegrees);
        }

        private void InvokeActiveRequestChanged(
            OrientationPriorityRequest request)
        {
            try
            {
                ActiveRequestChanged?.Invoke(
                    request
                );
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[OrientationPriorityRequestService]" +
                    $"[SUBSCRIBER_ERROR] {ex}",
                    this
                );
            }
        }

        private void InvokeActiveRequestCleared(
            string reason)
        {
            try
            {
                ActiveRequestCleared?.Invoke(
                    reason
                );
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[OrientationPriorityRequestService]" +
                    $"[SUBSCRIBER_ERROR] {ex}",
                    this
                );
            }
        }
    }
}
