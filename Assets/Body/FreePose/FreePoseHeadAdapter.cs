// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Core.Perception.Attention;

namespace SalieriAI.Body.FreePose
{
    /// <summary>
    /// Converts only the head portion of a Free Pose definition into the
    /// existing semantic Orientation request contract.
    /// </summary>
    public sealed class FreePoseHeadAdapter : IDisposable
    {
        public const string SourceKey = "free_pose:head:v0";
        public const int Priority = 50;

        private readonly OrientationPriorityRequestService requestService;
        private readonly Func<bool> isFaceAvailable;
        private readonly Func<bool> isObjectAvailable;

        private bool hasOwnRequest;
        private string activeRequestId = string.Empty;

        public bool HasOwnRequest => hasOwnRequest;
        public string ActiveRequestId => activeRequestId;

        public FreePoseHeadAdapter(
            OrientationPriorityRequestService requestService,
            FaceOrientationTargetSource faceSource,
            ObjectOrientationTargetSource objectSource)
            : this(
                requestService,
                () => faceSource != null && faceSource.HasCandidate,
                () => objectSource != null && objectSource.HasCandidate)
        {
        }

        internal FreePoseHeadAdapter(
            OrientationPriorityRequestService requestService,
            Func<bool> isFaceAvailable,
            Func<bool> isObjectAvailable)
        {
            this.requestService = requestService;
            this.isFaceAvailable = isFaceAvailable;
            this.isObjectAvailable = isObjectAvailable;
        }

        internal bool TryPreflight(
            FreePoseHeadIntent intent,
            out string reason)
        {
            if (intent == FreePoseHeadIntent.Keep)
            {
                reason = string.Empty;
                return true;
            }

            if (requestService == null)
            {
                reason = "free_pose_head_request_service_missing";
                return false;
            }

            return TryMap(intent, out _, out _, out reason);
        }

        public bool TryApply(
            FreePoseHeadIntent intent,
            out string reason)
        {
            if (intent == FreePoseHeadIntent.Keep)
            {
                // KEEP submits nothing. If this adapter previously owned a
                // request, remove only that request so KEEP cannot leave a
                // stale Free Pose head override behind.
                return Release(out reason);
            }

            if (requestService == null)
            {
                reason = "free_pose_head_request_service_missing";
                return false;
            }

            if (!TryMap(
                    intent,
                    out OrientationPriorityDirective directive,
                    out OrientationPriorityReason requestReason,
                    out reason))
            {
                return false;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string requestId = "freepose:head:" + Guid.NewGuid().ToString("N");
            var request = new OrientationPriorityRequest(
                requestId,
                SourceKey,
                directive,
                requestReason,
                Priority,
                now,
                0);

            if (!requestService.SubmitOrReplace(request, out reason))
                return false;

            hasOwnRequest = true;
            activeRequestId = requestId;
            reason = string.Empty;
            return true;
        }

        public bool Release(out string reason)
        {
            if (!hasOwnRequest)
            {
                reason = string.Empty;
                return true;
            }

            if (requestService == null)
            {
                ClearOwnership();
                reason = "free_pose_head_request_service_missing";
                return false;
            }

            bool removed = requestService.RemoveByRequestId(activeRequestId);
            ClearOwnership();
            reason = removed
                ? string.Empty
                : "free_pose_head_request_ownership_lost";
            return removed;
        }

        public void Dispose()
        {
            Release(out _);
        }

        private bool TryMap(
            FreePoseHeadIntent intent,
            out OrientationPriorityDirective directive,
            out OrientationPriorityReason reason,
            out string error)
        {
            switch (intent)
            {
                case FreePoseHeadIntent.Front:
                    directive = OrientationPriorityDirective.Neutral;
                    reason = OrientationPriorityReason.Default;
                    error = string.Empty;
                    return true;

                case FreePoseHeadIntent.ConversationPartner:
                    directive = IsAvailable(isFaceAvailable)
                        ? OrientationPriorityDirective.PreferFace
                        : OrientationPriorityDirective.Neutral;
                    reason = OrientationPriorityReason.Conversation;
                    error = string.Empty;
                    return true;

                case FreePoseHeadIntent.CurrentObject:
                    directive = IsAvailable(isObjectAvailable)
                        ? OrientationPriorityDirective.PreferObject
                        : OrientationPriorityDirective.Neutral;
                    reason = OrientationPriorityReason.ObserveObject;
                    error = string.Empty;
                    return true;

                default:
                    directive = default;
                    reason = default;
                    error = "free_pose_head_intent_unsupported";
                    return false;
            }
        }

        private static bool IsAvailable(Func<bool> availability)
        {
            return availability != null && availability();
        }

        private void ClearOwnership()
        {
            hasOwnRequest = false;
            activeRequestId = string.Empty;
        }
    }
}
