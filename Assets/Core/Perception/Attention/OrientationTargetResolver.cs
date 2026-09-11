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
    /// 顔候補、物体候補、外部優先要求を収集し、
    /// 現在の抽象的な注視対象を裁定する。
    ///
    /// 首、眼球、VRM、実機サーボは操作しない。
    /// </summary>
    [DefaultExecutionOrder(300)]
    [DisallowMultipleComponent]
    public sealed class OrientationTargetResolver : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField]
        private FaceOrientationTargetSource faceSource;

        [SerializeField]
        private ObjectOrientationTargetSource objectSource;

        [SerializeField]
        private OrientationPriorityRequestService priorityRequestService;

        [Header("Policy")]
        [SerializeField]
        private OrientationResolverPolicySettings policySettings =
            new OrientationResolverPolicySettings();

        [Header("Logging")]
        [SerializeField]
        private bool logResolutionChanges = true;

        [Header("Runtime State (Read Only)")]
        [SerializeField]
        private OrientationResolutionKind currentKind =
            OrientationResolutionKind.None;

        [SerializeField]
        private string currentTargetKey = string.Empty;

        [SerializeField]
        private string currentTargetLabel = string.Empty;

        [SerializeField]
        private Vector2 currentTargetCenter;

        [SerializeField]
        private bool currentTargetStable;

        [SerializeField]
        private string currentPrioritySourceKey = string.Empty;

        [SerializeField]
        private OrientationPriorityDirective currentDirective =
            OrientationPriorityDirective.Default;

        [SerializeField]
        private string currentReason = string.Empty;

        [SerializeField]
        private long lastTargetSwitchUnixMilliseconds;

        private bool subscribed;
        private bool resolutionDirty = true;
        private bool invalidResolutionLogged;
        private long nextTemporalEvaluationUnixMilliseconds;

        public event Action<OrientationResolution>
            ResolutionChanged;

        public OrientationResolution CurrentResolution
        {
            get;
            private set;
        }

        public OrientationResolution PreviousResolution
        {
            get;
            private set;
        }

        public long LastTargetSwitchUnixMilliseconds =>
            lastTargetSwitchUnixMilliseconds;

        private void OnEnable()
        {
            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            Subscribe();
            resolutionDirty = true;
        }

        private void LateUpdate()
        {
            if (!subscribed)
                return;

            long now = DateTimeOffset
                .UtcNow
                .ToUnixTimeMilliseconds();

            if (!resolutionDirty &&
                (nextTemporalEvaluationUnixMilliseconds <= 0 ||
                 now < nextTemporalEvaluationUnixMilliseconds))
            {
                return;
            }

            ResolveCurrentState(now);
        }

        private void OnDisable()
        {
            Unsubscribe();
            resolutionDirty = true;
            nextTemporalEvaluationUnixMilliseconds = 0;
        }

        private void OnDestroy()
        {
            Unsubscribe();
            ResolutionChanged = null;
            CurrentResolution = null;
            PreviousResolution = null;
        }

        private void OnValidate()
        {
            if (policySettings == null)
            {
                policySettings =
                    new OrientationResolverPolicySettings();
            }

            resolutionDirty = true;
            nextTemporalEvaluationUnixMilliseconds = 0;
        }

        private bool ValidateConfiguration()
        {
            bool valid = true;

            if (faceSource == null)
            {
                Debug.LogError(
                    "[OrientationTargetResolver][ERROR] " +
                    "FaceOrientationTargetSource is not assigned.",
                    this
                );

                valid = false;
            }

            if (objectSource == null)
            {
                Debug.LogError(
                    "[OrientationTargetResolver][ERROR] " +
                    "ObjectOrientationTargetSource is not assigned.",
                    this
                );

                valid = false;
            }

            if (priorityRequestService == null)
            {
                Debug.LogError(
                    "[OrientationTargetResolver][ERROR] " +
                    "OrientationPriorityRequestService is not assigned.",
                    this
                );

                valid = false;
            }

            if (policySettings == null)
            {
                Debug.LogError(
                    "[OrientationTargetResolver][ERROR] " +
                    "OrientationResolverPolicySettings is null.",
                    this
                );

                valid = false;
            }

            return valid;
        }

        private void Subscribe()
        {
            if (subscribed)
                return;

            faceSource.CandidateUpdated +=
                HandleCandidateUpdated;

            faceSource.CandidateCleared +=
                HandleCandidateCleared;

            objectSource.CandidateUpdated +=
                HandleCandidateUpdated;

            objectSource.CandidateCleared +=
                HandleCandidateCleared;

            priorityRequestService.ActiveRequestChanged +=
                HandlePriorityRequestChanged;

            priorityRequestService.ActiveRequestCleared +=
                HandlePriorityRequestCleared;

            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
                return;

            if (faceSource != null)
            {
                faceSource.CandidateUpdated -=
                    HandleCandidateUpdated;

                faceSource.CandidateCleared -=
                    HandleCandidateCleared;
            }

            if (objectSource != null)
            {
                objectSource.CandidateUpdated -=
                    HandleCandidateUpdated;

                objectSource.CandidateCleared -=
                    HandleCandidateCleared;
            }

            if (priorityRequestService != null)
            {
                priorityRequestService.ActiveRequestChanged -=
                    HandlePriorityRequestChanged;

                priorityRequestService.ActiveRequestCleared -=
                    HandlePriorityRequestCleared;
            }

            subscribed = false;
        }

        private void HandleCandidateUpdated(
            OrientationTargetCandidate candidate)
        {
            resolutionDirty = true;
        }

        private void HandleCandidateCleared(
            string reason)
        {
            resolutionDirty = true;
        }

        private void HandlePriorityRequestChanged(
            OrientationPriorityRequest request)
        {
            resolutionDirty = true;
        }

        private void HandlePriorityRequestCleared(
            string reason)
        {
            resolutionDirty = true;
        }

        private void ResolveCurrentState(long now)
        {
            OrientationResolution resolved =
                OrientationResolutionPolicy.Resolve(
                    faceSource.CurrentCandidate,
                    objectSource.CurrentCandidate,
                    priorityRequestService.ActiveRequest,
                    CurrentResolution,
                    policySettings,
                    now,
                    lastTargetSwitchUnixMilliseconds
                );

            if (resolved == null ||
                !resolved.IsValid)
            {
                if (!invalidResolutionLogged)
                {
                    Debug.LogError(
                        "[OrientationTargetResolver][ERROR] " +
                        "OrientationResolutionPolicy returned an invalid result.",
                        this
                    );

                    invalidResolutionLogged = true;
                }

                return;
            }

            invalidResolutionLogged = false;
            resolutionDirty = false;

            bool resolutionChanged =
                !AreEquivalentResolutions(
                    CurrentResolution,
                    resolved
                );

            bool targetSwitched =
                HasTargetSwitched(
                    CurrentResolution,
                    resolved
                );

            if (resolutionChanged)
            {
                PreviousResolution =
                    CurrentResolution != null
                        ? CurrentResolution.Clone()
                        : null;
            }

            // 同じ対象の中心座標や安定状態が更新された場合も、
            // 最新値をCurrentResolutionへ反映する。
            CurrentResolution = resolved.Clone();

            if (targetSwitched)
            {
                lastTargetSwitchUnixMilliseconds = now;
            }

            nextTemporalEvaluationUnixMilliseconds =
                CalculateNextTemporalEvaluation(now);

            UpdateInspectorState(CurrentResolution);

            if (!resolutionChanged)
                return;

            if (logResolutionChanges)
            {
                LogAttentionContinuityTransition(
                    PreviousResolution,
                    CurrentResolution);

                LogResolutionChanged(
                    CurrentResolution,
                    targetSwitched
                );
            }

            InvokeResolutionChanged(
                CurrentResolution.Clone()
            );
        }

        private long CalculateNextTemporalEvaluation(
            long now)
        {
            long next = 0;

            IncludeCandidateExpiry(
                faceSource.CurrentCandidate,
                now,
                ref next
            );

            IncludeCandidateExpiry(
                objectSource.CurrentCandidate,
                now,
                ref next
            );

            if (CurrentResolution != null &&
                CurrentResolution.HasTarget)
            {
                if (CurrentResolution.Kind ==
                    OrientationResolutionKind.AttentionContinuityHold)
                {
                    IncludeContinuityExpiry(
                        CurrentResolution,
                        now,
                        ref next);
                }
                else
                {
                    IncludeCandidateExpiry(
                        CurrentResolution.Target,
                        now,
                        ref next
                    );
                }
            }

            OrientationPriorityRequest request =
                priorityRequestService.ActiveRequest;

            if (request != null &&
                request.ExpiresAtUnixMilliseconds > now)
            {
                IncludeEarlierTime(
                    request.ExpiresAtUnixMilliseconds,
                    ref next
                );
            }

            int switchCooldownMilliseconds =
                policySettings.SwitchCooldownMilliseconds;

            if (switchCooldownMilliseconds > 0 &&
                lastTargetSwitchUnixMilliseconds > 0)
            {
                long cooldownEndsAt;

                try
                {
                    cooldownEndsAt = checked(
                        lastTargetSwitchUnixMilliseconds +
                        switchCooldownMilliseconds
                    );
                }
                catch (OverflowException)
                {
                    cooldownEndsAt = 0;
                }

                if (cooldownEndsAt > now)
                {
                    IncludeEarlierTime(
                        cooldownEndsAt,
                        ref next
                    );
                }
            }

            return next;
        }

        private void IncludeCandidateExpiry(
            OrientationTargetCandidate candidate,
            long now,
            ref long next)
        {
            if (candidate == null ||
                !candidate.IsValid)
            {
                return;
            }

            int lifetimeMilliseconds =
                candidate.IsStable
                    ? policySettings
                        .StableCandidateMaxAgeMilliseconds
                    : policySettings
                        .UnstableCandidateHoldMilliseconds;

            IncludeCandidateExpiry(
                candidate,
                lifetimeMilliseconds,
                now,
                ref next);
        }

        private static void IncludeCandidateExpiry(
            OrientationTargetCandidate candidate,
            int lifetimeMilliseconds,
            long now,
            ref long next)
        {
            if (candidate == null ||
                !candidate.IsValid)
            {
                return;
            }

            if (lifetimeMilliseconds <= 0)
                return;

            long expiresAt;

            try
            {
                expiresAt = checked(
                    candidate.UpdatedAtUnixMilliseconds +
                    lifetimeMilliseconds +
                    1L
                );
            }
            catch (OverflowException)
            {
                return;
            }

            if (expiresAt > now)
            {
                IncludeEarlierTime(
                    expiresAt,
                    ref next
                );
            }
        }

        private void IncludeContinuityExpiry(
            OrientationResolution resolution,
            long now,
            ref long next)
        {
            if (resolution == null ||
                resolution.Kind !=
                    OrientationResolutionKind.AttentionContinuityHold ||
                resolution.ContinuityStartedAtUnixMilliseconds <= 0 ||
                policySettings.UnstableCandidateHoldMilliseconds <= 0)
            {
                return;
            }

            long expiresAt;
            try
            {
                expiresAt = checked(
                    resolution.ContinuityStartedAtUnixMilliseconds +
                    policySettings.UnstableCandidateHoldMilliseconds +
                    1L);
            }
            catch (OverflowException)
            {
                return;
            }

            if (expiresAt > now)
                IncludeEarlierTime(expiresAt, ref next);
        }

        private void LogAttentionContinuityTransition(
            OrientationResolution previous,
            OrientationResolution current)
        {
            if (current == null)
                return;

            string targetKey = current.HasTarget
                ? current.Target.TargetKey
                : "none";

            if (current.Kind ==
                OrientationResolutionKind.AttentionContinuityHold)
            {
                string marker = previous != null &&
                    previous.Kind ==
                        OrientationResolutionKind.AttentionContinuityHold
                            ? "ATTENTION_CONTINUITY_MAINTAINED"
                            : "ATTENTION_CONTINUITY_STARTED";
                Debug.Log(
                    "[OrientationTargetResolver][" + marker + "] " +
                    "Target=" + targetKey + " " +
                    "LossProvenance=" + current.Provenance + " " +
                    "Reason=" + current.Reason,
                    this);
                return;
            }

            if (current.Kind == OrientationResolutionKind.Object)
            {
                bool reacquired = previous != null &&
                    previous.Kind ==
                        OrientationResolutionKind.AttentionContinuityHold &&
                    previous.HasTarget &&
                    current.HasTarget &&
                    string.Equals(
                        previous.Target.TargetKey,
                        current.Target.TargetKey,
                        StringComparison.Ordinal);

                bool replaced = previous != null &&
                    previous.HasTarget &&
                    current.HasTarget &&
                    !string.Equals(
                        previous.Target.TargetKey,
                        current.Target.TargetKey,
                        StringComparison.Ordinal);

                string marker = reacquired
                    ? "ATTENTION_REACQUIRED"
                    : replaced
                        ? "ATTENTION_REPLACED"
                        : "ATTENTION_DIRECT";
                Debug.Log(
                    "[OrientationTargetResolver][" + marker + "] " +
                    "Target=" + targetKey + " " +
                    "LossProvenance=" + current.Provenance,
                    this);
                return;
            }

            if (current.Kind ==
                OrientationResolutionKind.AttentionContinuityExpired)
            {
                Debug.Log(
                    "[OrientationTargetResolver]" +
                    "[ATTENTION_CONTINUITY_EXPIRED] " +
                    "Target=none LossProvenance=" + current.Provenance +
                    " Reason=" + current.Reason,
                    this);
                return;
            }

            if (current.Kind == OrientationResolutionKind.Neutral)
            {
                Debug.Log(
                    "[OrientationTargetResolver]" +
                    "[ATTENTION_EXPLICIT_NEUTRAL] " +
                    "Target=none LossProvenance=" + current.Provenance,
                    this);
            }
        }

        private static void IncludeEarlierTime(
            long candidate,
            ref long current)
        {
            if (candidate <= 0)
                return;

            if (current <= 0 || candidate < current)
            {
                current = candidate;
            }
        }

        private void UpdateInspectorState(
            OrientationResolution resolution)
        {
            currentKind =
                resolution != null
                    ? resolution.Kind
                    : OrientationResolutionKind.None;

            OrientationTargetCandidate target =
                resolution != null
                    ? resolution.Target
                    : null;

            currentTargetKey =
                target != null
                    ? target.TargetKey ?? string.Empty
                    : resolution != null &&
                      resolution.PriorityRequest != null &&
                      resolution.PriorityRequest.TargetDirection != null
                        ? resolution.PriorityRequest.TargetDirection.TargetKey ??
                          string.Empty
                        : string.Empty;

            currentTargetLabel =
                target != null
                    ? target.Label ?? string.Empty
                    : string.Empty;

            currentTargetCenter =
                target != null
                    ? new Vector2(
                        target.CenterX,
                        target.CenterY)
                    : Vector2.zero;

            currentTargetStable =
                target != null &&
                target.IsStable;

            OrientationPriorityRequest request =
                resolution != null
                    ? resolution.PriorityRequest
                    : null;

            currentPrioritySourceKey =
                request != null
                    ? request.SourceKey ?? string.Empty
                    : string.Empty;

            currentDirective =
                request != null
                    ? request.Directive
                    : OrientationPriorityDirective.Default;

            currentReason =
                resolution != null
                    ? resolution.Reason ?? string.Empty
                    : string.Empty;
        }

        private void LogResolutionChanged(
            OrientationResolution resolution,
            bool targetSwitched)
        {
            string targetKey =
                resolution.HasTarget
                    ? resolution.Target.TargetKey
                    : resolution.PriorityRequest != null &&
                      resolution.PriorityRequest.TargetDirection != null
                        ? resolution.PriorityRequest.TargetDirection.TargetKey
                        : "none";

            string requestSource =
                resolution.PriorityRequest != null
                    ? resolution.PriorityRequest.SourceKey
                    : "default";

            Debug.Log(
                "[OrientationTargetResolver][RESOLUTION_CHANGED] " +
                $"Kind={resolution.Kind} " +
                $"Target={targetKey} " +
                $"Request={requestSource} " +
                $"TargetSwitched={targetSwitched} " +
                $"Reason={resolution.Reason}",
                this
            );
        }

        private void InvokeResolutionChanged(
            OrientationResolution resolution)
        {
            try
            {
                ResolutionChanged?.Invoke(
                    resolution
                );
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[OrientationTargetResolver]" +
                    $"[SUBSCRIBER_ERROR] {ex}",
                    this
                );
            }
        }

        private static bool AreEquivalentResolutions(
            OrientationResolution left,
            OrientationResolution right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (left == null || right == null)
                return false;

            return
                left.Kind == right.Kind &&
                AreSameTargetIdentity(
                    left.Target,
                    right.Target) &&
                AreEquivalentRequests(
                    left.PriorityRequest,
                    right.PriorityRequest) &&
                string.Equals(
                    left.Reason,
                    right.Reason,
                    StringComparison.Ordinal);
        }

        private static bool HasTargetSwitched(
            OrientationResolution previous,
            OrientationResolution current)
        {
            OrientationTargetCandidate previousTarget =
                previous != null && previous.HasTarget
                    ? previous.Target
                    : null;

            OrientationTargetCandidate currentTarget =
                current != null && current.HasTarget
                    ? current.Target
                    : null;

            // 対象の有無が変わった場合も、意味的な対象切替として扱う。
            if (previousTarget == null ||
                currentTarget == null)
            {
                return previousTarget != currentTarget;
            }

            return !AreSameTargetIdentity(
                previousTarget,
                currentTarget
            );
        }

        private static bool AreSameTargetIdentity(
            OrientationTargetCandidate left,
            OrientationTargetCandidate right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (left == null || right == null)
                return false;

            return
                left.Kind == right.Kind &&
                string.Equals(
                    left.TargetKey,
                    right.TargetKey,
                    StringComparison.Ordinal);
        }

        private static bool AreEquivalentRequests(
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
                left.Directive == right.Directive &&
                left.Reason == right.Reason &&
                left.Priority == right.Priority &&
                left.CreatedAtUnixMilliseconds ==
                    right.CreatedAtUnixMilliseconds &&
                left.ExpiresAtUnixMilliseconds ==
                    right.ExpiresAtUnixMilliseconds &&
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
    }
}
