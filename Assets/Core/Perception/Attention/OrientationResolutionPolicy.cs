// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

namespace SalieriAI.Core.Perception.Attention
{
    /// <summary>
    /// 顔候補、物体候補、優先要求から注視対象を裁定する。
    ///
    /// MonoBehaviourやScene状態を参照せず、
    /// 引数だけを使って結果を返す。
    ///
    /// 首、眼球、VRM、実機サーボは操作しない。
    /// </summary>
    public static class OrientationResolutionPolicy
    {
        public static OrientationResolution Resolve(
            OrientationTargetCandidate faceCandidate,
            OrientationTargetCandidate objectCandidate,
            OrientationPriorityRequest activeRequest,
            OrientationResolution previousResolution,
            OrientationResolverPolicySettings settings,
            long currentUnixMilliseconds,
            long lastTargetSwitchUnixMilliseconds)
        {
            if (settings == null)
            {
                settings =
                    new OrientationResolverPolicySettings();
            }

            long now =
                currentUnixMilliseconds > 0
                    ? currentUnixMilliseconds
                    : 1;

            OrientationTargetCandidate usableFace =
                IsCandidateUsable(
                    faceCandidate,
                    settings,
                    now)
                    ? faceCandidate
                    : null;

            OrientationTargetCandidate usableObject =
                IsCandidateUsable(
                    objectCandidate,
                    settings,
                    now)
                    ? objectCandidate
                    : null;

            OrientationPriorityRequest request =
                IsRequestUsable(
                    activeRequest,
                    now)
                    ? activeRequest
                    : null;

            if (request != null)
            {
                switch (request.Directive)
                {
                    case OrientationPriorityDirective.PreferFace:
                        return ResolvePreferredTarget(
                            usableFace,
                            usableObject,
                            OrientationTargetCandidateKind.Face,
                            request,
                            settings,
                            now);

                    case OrientationPriorityDirective.PreferObject:
                        return ResolvePreferredTarget(
                            usableObject,
                            usableFace,
                            OrientationTargetCandidateKind.Object,
                            request,
                            settings,
                            now);

                    case OrientationPriorityDirective.HoldPrevious:
                        return ResolveHoldPrevious(
                            usableFace,
                            usableObject,
                            previousResolution,
                            request,
                            settings,
                            now);

                    case OrientationPriorityDirective.Neutral:
                        return CreateResolution(
                            OrientationResolutionKind.Neutral,
                            null,
                            request,
                            now,
                            "Explicit neutral request");

                    case OrientationPriorityDirective.TargetDirection:
                        return CreateResolution(
                            OrientationResolutionKind.TargetDirection,
                            null,
                            request,
                            now,
                            "Explicit body-relative target direction request");

                    case OrientationPriorityDirective.Default:
                    default:
                        break;
                }
            }

            return ResolveDefault(
                usableFace,
                usableObject,
                request,
                previousResolution,
                settings,
                now,
                lastTargetSwitchUnixMilliseconds);
        }

        /// <summary>
        /// 候補が現在の設定で使用可能か判定する。
        /// </summary>
        public static bool IsCandidateUsable(
            OrientationTargetCandidate candidate,
            OrientationResolverPolicySettings settings,
            long currentUnixMilliseconds)
        {
            if (candidate == null ||
                !candidate.IsValid ||
                settings == null)
            {
                return false;
            }

            long age =
                currentUnixMilliseconds -
                candidate.UpdatedAtUnixMilliseconds;

            if (age < 0)
            {
                age = 0;
            }

            if (candidate.IsStable)
            {
                int maxAge =
                    settings
                        .StableCandidateMaxAgeMilliseconds;

                return
                    maxAge <= 0 ||
                    age <= maxAge;
            }

            int holdMilliseconds =
                settings
                    .UnstableCandidateHoldMilliseconds;

            return
                holdMilliseconds > 0 &&
                age <= holdMilliseconds;
        }

        private static OrientationResolution ResolveDefault(
            OrientationTargetCandidate face,
            OrientationTargetCandidate objectTarget,
            OrientationPriorityRequest request,
            OrientationResolution previousResolution,
            OrientationResolverPolicySettings settings,
            long now,
            long lastTargetSwitchUnixMilliseconds)
        {
            OrientationTargetCandidate selected = null;
            string reason;

            switch (settings.DefaultSelectionPolicy)
            {
                case OrientationDefaultSelectionPolicy.PreferFace:
                    selected =
                        SelectPreferredCandidate(
                            face,
                            objectTarget,
                            settings);

                    reason =
                        "Default policy prefers face";
                    break;

                case OrientationDefaultSelectionPolicy.PreferObject:
                    selected =
                        SelectPreferredCandidate(
                            objectTarget,
                            face,
                            settings);

                    reason =
                        "Default policy prefers object";
                    break;

                case OrientationDefaultSelectionPolicy.PreferStable:
                    selected =
                        SelectStableCandidate(
                            face,
                            objectTarget);

                    reason =
                        "Default policy prefers stable candidate";
                    break;

                case OrientationDefaultSelectionPolicy.PreferMostRecent:
                    selected =
                        SelectMostRecentCandidate(
                            face,
                            objectTarget);

                    reason =
                        "Default policy prefers most recent candidate";
                    break;

                case OrientationDefaultSelectionPolicy.NoPreference:
                default:
                    return CreateResolution(
                        OrientationResolutionKind.None,
                        null,
                        request,
                        now,
                        "Default policy has no preference");
            }

            OrientationResolution continuity =
                ResolveDefaultObjectContinuity(
                    selected,
                    previousResolution,
                    request,
                    settings,
                    now);

            if (continuity != null)
                return continuity;

            if (selected == null)
            {
                return CreateResolution(
                    OrientationResolutionKind.None,
                    null,
                    request,
                    now,
                    "No usable orientation candidate");
            }

            OrientationTargetCandidate previousTarget =
                ResolvePreviousTarget(
                    face,
                    objectTarget,
                    previousResolution,
                    settings,
                    now);

            if (ShouldHoldPreviousForCooldown(
                    previousTarget,
                    selected,
                    settings,
                    now,
                    lastTargetSwitchUnixMilliseconds))
            {
                return CreateResolution(
                    OrientationResolutionKind.HoldPrevious,
                    previousTarget,
                    request,
                    now,
                    "Automatic target switch suppressed by cooldown");
            }

            return CreateTargetResolution(
                selected,
                request,
                now,
                reason);
        }

        /// <summary>
        /// Retains only the exact previously resolved Object snapshot for the
        /// configured unstable-candidate window. A different candidate is
        /// never relabelled as the previous target; it is merely delayed until
        /// the short continuity window expires.
        /// </summary>
        private static OrientationResolution
            ResolveDefaultObjectContinuity(
                OrientationTargetCandidate selected,
                OrientationResolution previousResolution,
                OrientationPriorityRequest request,
                OrientationResolverPolicySettings settings,
                long now)
        {
            if (previousResolution == null ||
                !previousResolution.HasTarget ||
                previousResolution.Target.Kind !=
                    OrientationTargetCandidateKind.Object ||
                (previousResolution.Kind !=
                    OrientationResolutionKind.Object &&
                 previousResolution.Kind !=
                    OrientationResolutionKind.AttentionContinuityHold))
            {
                return null;
            }

            OrientationTargetCandidate previousTarget =
                previousResolution.Target;

            if (IsSameTarget(previousTarget, selected))
            {
                // Exact TargetKey restoration is handled by the normal direct
                // observation path. The Resolver can diagnose this transition
                // by comparing the previous continuity kind.
                return null;
            }

            int holdMilliseconds =
                settings.UnstableCandidateHoldMilliseconds;

            if (holdMilliseconds <= 0)
                return null;

            long continuityStartedAt =
                previousResolution.Kind ==
                    OrientationResolutionKind.AttentionContinuityHold &&
                previousResolution
                    .ContinuityStartedAtUnixMilliseconds > 0
                        ? previousResolution
                            .ContinuityStartedAtUnixMilliseconds
                        : now;

            long age = now - continuityStartedAt;
            if (age < 0)
                age = 0;

            if (age <= holdMilliseconds)
            {
                return CreateResolution(
                    OrientationResolutionKind.AttentionContinuityHold,
                    previousTarget,
                    request,
                    now,
                    selected == null
                        ? "Object temporarily not observed; resolver continuity retained"
                        : "Different target delayed; resolver continuity retained without identity inheritance",
                    continuityStartedAt);
            }

            if (selected == null)
            {
                return CreateResolution(
                    OrientationResolutionKind.AttentionContinuityExpired,
                    null,
                    request,
                    now,
                    "Object attention continuity expired without exact-target recovery");
            }

            // A different valid candidate after expiry follows the existing
            // target-selection/switch semantics. It is not A and is not a
            // reacquisition bridge.
            return null;
        }

        private static OrientationResolution
            ResolvePreferredTarget(
                OrientationTargetCandidate preferred,
                OrientationTargetCandidate fallback,
                OrientationTargetCandidateKind preferredKind,
                OrientationPriorityRequest request,
                OrientationResolverPolicySettings settings,
                long now)
        {
            OrientationTargetCandidate selected =
                SelectPreferredCandidate(
                    preferred,
                    fallback,
                    settings);

            if (selected == null)
            {
                return CreateResolution(
                    OrientationResolutionKind.None,
                    null,
                    request,
                    now,
                    $"Preferred {preferredKind} target is unavailable");
            }

            string reason =
                selected.Kind == preferredKind
                    ? $"Explicit request selected {preferredKind}"
                    : $"Preferred {preferredKind} unavailable; fallback selected";

            return CreateTargetResolution(
                selected,
                request,
                now,
                reason);
        }

        private static OrientationResolution
            ResolveHoldPrevious(
                OrientationTargetCandidate face,
                OrientationTargetCandidate objectTarget,
                OrientationResolution previousResolution,
                OrientationPriorityRequest request,
                OrientationResolverPolicySettings settings,
                long now)
        {
            OrientationTargetCandidate previousTarget = null;

            string heldTargetKey =
                request != null
                    ? request.HeldTargetKey
                    : string.Empty;

            if (!string.IsNullOrEmpty(heldTargetKey))
            {
                previousTarget = ResolveExplicitHeldTarget(
                    face,
                    objectTarget,
                    previousResolution,
                    heldTargetKey);

                if (previousTarget == null)
                {
                    return CreateResolution(
                        OrientationResolutionKind.None,
                        null,
                        request,
                        now,
                        "Explicit held target does not match the previous or current target");
                }

                return CreateResolution(
                    OrientationResolutionKind.HoldPrevious,
                    previousTarget,
                    request,
                    now,
                    "Explicit hold of correlated target");
            }

            // HoldPreviousへ入った後は、要求が有効な間、
            // 最初に確定した対象スナップショットを継続して保持する。
            //
            // 現在の顔・物体候補が消失した場合でも、
            // 候補の鮮度判定によって保持対象を失わない。
            if (previousResolution != null &&
                previousResolution.Kind ==
                OrientationResolutionKind.HoldPrevious &&
                previousResolution.HasTarget)
            {
                previousTarget =
                    previousResolution.Target;
            }
            else
            {
                previousTarget =
                    ResolvePreviousTarget(
                        face,
                        objectTarget,
                        previousResolution,
                        settings,
                        now);
            }

            if (previousTarget == null)
            {
                return CreateResolution(
                    OrientationResolutionKind.None,
                    null,
                    request,
                    now,
                    "HoldPrevious requested but no usable previous target exists");
            }

            return CreateResolution(
                OrientationResolutionKind.HoldPrevious,
                previousTarget,
                request,
                now,
                "Explicit hold previous request");
        }

        private static OrientationTargetCandidate ResolveExplicitHeldTarget(
            OrientationTargetCandidate face,
            OrientationTargetCandidate objectTarget,
            OrientationResolution previousResolution,
            string heldTargetKey)
        {
            // Behaviorが直前に確認したTargetと同一の場合、初回Hold移行で
            // freshness境界を跨いでも、その解決済みsnapshotを明示的に保持する。
            if (previousResolution != null &&
                previousResolution.HasTarget &&
                HasTargetKey(previousResolution.Target, heldTargetKey))
            {
                return previousResolution.Target;
            }

            if (HasTargetKey(face, heldTargetKey))
                return face;
            if (HasTargetKey(objectTarget, heldTargetKey))
                return objectTarget;
            return null;
        }

        private static bool HasTargetKey(
            OrientationTargetCandidate candidate,
            string targetKey)
        {
            return candidate != null &&
                string.Equals(
                    candidate.TargetKey,
                    targetKey,
                    StringComparison.Ordinal);
        }

        private static OrientationTargetCandidate
            SelectPreferredCandidate(
                OrientationTargetCandidate preferred,
                OrientationTargetCandidate fallback,
                OrientationResolverPolicySettings settings)
        {
            if (preferred != null)
            {
                return preferred;
            }

            if (settings
                .FallbackToOtherTargetWhenPreferredMissing)
            {
                return fallback;
            }

            return null;
        }

        private static OrientationTargetCandidate
            SelectStableCandidate(
                OrientationTargetCandidate face,
                OrientationTargetCandidate objectTarget)
        {
            if (face == null)
            {
                return objectTarget;
            }

            if (objectTarget == null)
            {
                return face;
            }

            if (face.IsStable != objectTarget.IsStable)
            {
                return face.IsStable
                    ? face
                    : objectTarget;
            }

            return SelectMostRecentCandidate(
                face,
                objectTarget);
        }

        private static OrientationTargetCandidate
            SelectMostRecentCandidate(
                OrientationTargetCandidate face,
                OrientationTargetCandidate objectTarget)
        {
            if (face == null)
            {
                return objectTarget;
            }

            if (objectTarget == null)
            {
                return face;
            }

            if (face.UpdatedAtUnixMilliseconds !=
                objectTarget.UpdatedAtUnixMilliseconds)
            {
                return
                    face.UpdatedAtUnixMilliseconds >
                    objectTarget.UpdatedAtUnixMilliseconds
                        ? face
                        : objectTarget;
            }

            // 完全同条件では再現性を保つためFaceを選択する。
            return face;
        }

        private static OrientationTargetCandidate
            ResolvePreviousTarget(
                OrientationTargetCandidate face,
                OrientationTargetCandidate objectTarget,
                OrientationResolution previousResolution,
                OrientationResolverPolicySettings settings,
                long now)
        {
            if (previousResolution == null ||
                !previousResolution.HasTarget)
            {
                return null;
            }

            OrientationTargetCandidate previous =
                previousResolution.Target;

            if (IsSameTarget(
                    previous,
                    face))
            {
                return face;
            }

            if (IsSameTarget(
                    previous,
                    objectTarget))
            {
                return objectTarget;
            }

            return IsCandidateUsable(
                    previous,
                    settings,
                    now)
                ? previous
                : null;
        }

        private static bool ShouldHoldPreviousForCooldown(
            OrientationTargetCandidate previous,
            OrientationTargetCandidate selected,
            OrientationResolverPolicySettings settings,
            long now,
            long lastTargetSwitchUnixMilliseconds)
        {
            if (previous == null ||
                selected == null ||
                IsSameTarget(
                    previous,
                    selected))
            {
                return false;
            }

            int cooldown =
                settings.SwitchCooldownMilliseconds;

            if (cooldown <= 0 ||
                lastTargetSwitchUnixMilliseconds <= 0)
            {
                return false;
            }

            long elapsed =
                now -
                lastTargetSwitchUnixMilliseconds;

            return
                elapsed >= 0 &&
                elapsed < cooldown;
        }

        private static bool IsSameTarget(
            OrientationTargetCandidate left,
            OrientationTargetCandidate right)
        {
            if (left == null ||
                right == null)
            {
                return false;
            }

            return
                left.Kind == right.Kind &&
                string.Equals(
                    left.TargetKey,
                    right.TargetKey,
                    StringComparison.Ordinal);
        }

        private static bool IsRequestUsable(
            OrientationPriorityRequest request,
            long now)
        {
            return
                request != null &&
                request.IsValid &&
                !request.IsExpired(now);
        }

        private static OrientationResolution
            CreateTargetResolution(
                OrientationTargetCandidate target,
                OrientationPriorityRequest request,
                long now,
                string reason)
        {
            OrientationResolutionKind kind =
                target.Kind ==
                OrientationTargetCandidateKind.Face
                    ? OrientationResolutionKind.Face
                    : OrientationResolutionKind.Object;

            return CreateResolution(
                kind,
                target,
                request,
                now,
                reason);
        }

        private static OrientationResolution
            CreateResolution(
                OrientationResolutionKind kind,
                OrientationTargetCandidate target,
                OrientationPriorityRequest request,
                long now,
                string reason,
                long continuityStartedAt = 0)
        {
            return new OrientationResolution(
                kind,
                target,
                request,
                now,
                reason,
                continuityStartedAt);
        }
    }
}
