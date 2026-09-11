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
    /// Resolverが決定した現在の注視方針。
    /// </summary>
    public enum OrientationResolutionKind
    {
        None = 0,
        Face = 1,
        Object = 2,
        Neutral = 3,
        HoldPrevious = 4,
        TargetDirection = 5,

        /// <summary>
        /// A short-lived, resolver-owned retention of the last directly
        /// observed Object target while perception is temporarily absent.
        /// This is not persistent identity and is distinct from an explicit
        /// HoldPrevious priority request.
        /// </summary>
        AttentionContinuityHold = 6,

        /// <summary>
        /// The resolver-owned continuity window elapsed without exact-target
        /// recovery. This no-target result carries true-loss provenance so a
        /// downstream actuator does not start a second hidden grace period.
        /// </summary>
        AttentionContinuityExpired = 7
    }

    /// <summary>
    /// Why the current attention result is direct, retained, or unavailable.
    /// Safety/permission override remains a downstream authority decision and
    /// is represented here only as an explicit contract value for diagnostics.
    /// </summary>
    public enum OrientationAttentionProvenance
    {
        None = 0,
        DirectObservation = 1,
        TransientPerceptionDropout = 2,
        ContinuityExpired = 3,
        ExplicitNeutral = 4,
        SafetyOrPermissionOverride = 5
    }

    /// <summary>
    /// 顔候補、物体候補、優先要求を裁定した結果。
    ///
    /// この型自体は首・眼球・サーボを操作しない。
    /// </summary>
    [Serializable]
    public sealed class OrientationResolution
    {
        public OrientationResolutionKind Kind
        {
            get;
        }

        /// <summary>
        /// Face、Object、HoldPreviousの場合に使用する対象。
        /// Neutral、Noneではnull。
        /// </summary>
        public OrientationTargetCandidate Target
        {
            get;
        }

        /// <summary>
        /// 裁定に使用した外部要求。
        /// デフォルト裁定の場合はnull。
        /// </summary>
        public OrientationPriorityRequest PriorityRequest
        {
            get;
        }

        public long ResolvedAtUnixMilliseconds
        {
            get;
        }

        public string Reason
        {
            get;
        }

        public OrientationAttentionProvenance Provenance
        {
            get;
        }

        /// <summary>
        /// First instant at which direct Object observation was lost for the
        /// current resolver-owned continuity cycle. Zero for all other kinds.
        /// </summary>
        public long ContinuityStartedAtUnixMilliseconds
        {
            get;
        }

        public bool HasTarget =>
            Target != null &&
            Target.IsValid;

        public bool IsValid
        {
            get
            {
                if (ResolvedAtUnixMilliseconds <= 0)
                    return false;

                switch (Kind)
                {
                    case OrientationResolutionKind.Face:
                        return
                            HasTarget &&
                            Target.Kind ==
                            OrientationTargetCandidateKind.Face;

                    case OrientationResolutionKind.Object:
                        return
                            HasTarget &&
                            Target.Kind ==
                            OrientationTargetCandidateKind.Object;

                    case OrientationResolutionKind.HoldPrevious:
                    case OrientationResolutionKind.AttentionContinuityHold:
                        return HasTarget;

                    case OrientationResolutionKind.TargetDirection:
                        return
                            Target == null &&
                            PriorityRequest != null &&
                            PriorityRequest.Directive ==
                                OrientationPriorityDirective.TargetDirection &&
                            PriorityRequest.TargetDirection != null &&
                            PriorityRequest.TargetDirection.IsValid;

                    case OrientationResolutionKind.Neutral:
                    case OrientationResolutionKind.None:
                    case OrientationResolutionKind.AttentionContinuityExpired:
                        return Target == null;

                    default:
                        return false;
                }
            }
        }

        public OrientationResolution(
            OrientationResolutionKind kind,
            OrientationTargetCandidate target,
            OrientationPriorityRequest priorityRequest,
            long resolvedAtUnixMilliseconds,
            string reason,
            long continuityStartedAtUnixMilliseconds = 0)
        {
            Kind = kind;

            Target =
                target != null
                    ? target.Clone()
                    : null;

            PriorityRequest =
                priorityRequest != null
                    ? priorityRequest.Clone()
                    : null;

            ResolvedAtUnixMilliseconds =
                resolvedAtUnixMilliseconds;

            Reason =
                reason ?? string.Empty;

            Provenance = InferProvenance(kind);

            ContinuityStartedAtUnixMilliseconds =
                kind == OrientationResolutionKind.AttentionContinuityHold
                    ? Math.Max(1L, continuityStartedAtUnixMilliseconds)
                    : 0L;
        }

        public OrientationResolution Clone()
        {
            return new OrientationResolution(
                Kind,
                Target,
                PriorityRequest,
                ResolvedAtUnixMilliseconds,
                Reason,
                ContinuityStartedAtUnixMilliseconds
            );
        }

        private static OrientationAttentionProvenance InferProvenance(
            OrientationResolutionKind kind)
        {
            switch (kind)
            {
                case OrientationResolutionKind.Face:
                case OrientationResolutionKind.Object:
                    return OrientationAttentionProvenance.DirectObservation;

                case OrientationResolutionKind.AttentionContinuityHold:
                    return OrientationAttentionProvenance
                        .TransientPerceptionDropout;

                case OrientationResolutionKind.AttentionContinuityExpired:
                    return OrientationAttentionProvenance.ContinuityExpired;

                case OrientationResolutionKind.Neutral:
                    return OrientationAttentionProvenance.ExplicitNeutral;

                case OrientationResolutionKind.None:
                case OrientationResolutionKind.HoldPrevious:
                case OrientationResolutionKind.TargetDirection:
                default:
                    return OrientationAttentionProvenance.None;
            }
        }
    }
}
