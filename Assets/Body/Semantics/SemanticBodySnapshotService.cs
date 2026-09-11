// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Body.FreePose;
using SalieriAI.Body.Retargeting.Neck;
using SalieriAI.Body.SpatialTarget;
using SalieriAI.Core.Perception.Attention;

using UnityEngine;

namespace SalieriAI.Body.Semantics
{
    /// <summary>
    /// Pure aggregation boundary from existing read-only runtime states to a
    /// conversation-facing semantic body snapshot. It never drives the body.
    /// </summary>
    public sealed class SemanticBodySnapshotService
    {
        public SemanticBodySnapshot Capture(
            ProductionPoseRuntimeSnapshot freePose,
            bool handAuthorityAvailable,
            HandTargetAuthoritySnapshot rightAuthority,
            HandTargetAuthoritySnapshot leftAuthority,
            OrientationResolution orientation,
            bool neckStateAvailable,
            PhysicalNeckMotionPhase neckPhase,
            DateTime timestampUtc)
        {
            SemanticArmSnapshot right = ProjectArm(
                handAuthorityAvailable,
                rightAuthority,
                freePose != null &&
                    freePose.ExecutionStateAvailable &&
                    freePose.IsActive
                    ? freePose.RightArmSpatialTargetId
                    : string.Empty,
                freePose != null && freePose.IsArmTransitioning);
            SemanticArmSnapshot left = ProjectArm(
                handAuthorityAvailable,
                leftAuthority,
                freePose != null &&
                    freePose.ExecutionStateAvailable &&
                    freePose.IsActive
                    ? freePose.LeftArmSpatialTargetId
                    : string.Empty,
                freePose != null && freePose.IsArmTransitioning);
            SemanticHeadAttentionSnapshot head = ProjectHead(
                orientation,
                neckStateAvailable,
                neckPhase);

            return new SemanticBodySnapshot(
                right,
                left,
                head,
                ProjectAction(freePose, right, left),
                timestampUtc);
        }

        private static SemanticArmSnapshot ProjectArm(
            bool authorityAvailable,
            HandTargetAuthoritySnapshot authority,
            string activeSpatialTargetId,
            bool freePoseTransitioning)
        {
            if (!authorityAvailable)
                return SemanticArmSnapshot.Unknown();

            if (!authority.TargetAvailable)
            {
                return new SemanticArmSnapshot(
                    SemanticBodyAvailability.Unavailable,
                    SemanticArmPose.Unknown,
                    SemanticArmActivity.Unknown,
                    SemanticBodyOwnership.None,
                    SemanticBodyStateBasis.Unknown,
                    SemanticBodyStateSource.HandTargetAuthority);
            }

            SemanticBodyOwnership ownership = MapOwner(
                authority.HasOwner,
                authority.Owner);
            if (authority.HasOwner)
            {
                bool freePoseOwner =
                    ownership == SemanticBodyOwnership.FreePose;
                return new SemanticArmSnapshot(
                    SemanticBodyAvailability.Available,
                    freePoseOwner
                        ? MapPose(activeSpatialTargetId)
                        : SemanticArmPose.Unknown,
                    SemanticArmActivity.ActiveRequest,
                    ownership,
                    freePoseOwner && freePoseTransitioning
                        ? SemanticBodyStateBasis.Requested
                        : SemanticBodyStateBasis.Commanded,
                    freePoseOwner
                        ? SemanticBodyStateSource.FreePose
                        : SemanticBodyStateSource.HandTargetAuthority);
            }

            if (authority.LastSubmitFrame >= 0)
            {
                return new SemanticArmSnapshot(
                    SemanticBodyAvailability.Available,
                    SemanticArmPose.Hold,
                    SemanticArmActivity.HeldCommand,
                    SemanticBodyOwnership.None,
                    SemanticBodyStateBasis.Commanded,
                    SemanticBodyStateSource.HandTargetAuthority);
            }

            return new SemanticArmSnapshot(
                SemanticBodyAvailability.Available,
                SemanticArmPose.Unknown,
                SemanticArmActivity.Idle,
                SemanticBodyOwnership.None,
                SemanticBodyStateBasis.Unknown,
                SemanticBodyStateSource.HandTargetAuthority);
        }

        private static SemanticHeadAttentionSnapshot ProjectHead(
            OrientationResolution orientation,
            bool neckStateAvailable,
            PhysicalNeckMotionPhase neckPhase)
        {
            if (neckStateAvailable &&
                neckPhase == PhysicalNeckMotionPhase.HoldingAfterTargetLost)
            {
                return new SemanticHeadAttentionSnapshot(
                    SemanticHeadAttention.Hold,
                    SemanticBodyStateBasis.Commanded,
                    SemanticBodyStateSource.NeckMotionController);
            }

            if (orientation == null || !orientation.IsValid)
                return SemanticHeadAttentionSnapshot.Unknown();

            SemanticHeadAttention attention;
            switch (orientation.Kind)
            {
                case OrientationResolutionKind.Face:
                    attention = SemanticHeadAttention.Face;
                    break;
                case OrientationResolutionKind.Object:
                    attention = SemanticHeadAttention.Object;
                    break;
                case OrientationResolutionKind.Neutral:
                    attention = SemanticHeadAttention.Neutral;
                    break;
                case OrientationResolutionKind.TargetDirection:
                    attention = SemanticHeadAttention.Directed;
                    break;
                case OrientationResolutionKind.HoldPrevious:
                case OrientationResolutionKind.AttentionContinuityHold:
                    attention = SemanticHeadAttention.Hold;
                    break;
                case OrientationResolutionKind.None:
                case OrientationResolutionKind.AttentionContinuityExpired:
                    attention = SemanticHeadAttention.None;
                    break;
                default:
                    attention = SemanticHeadAttention.Unknown;
                    break;
            }

            SemanticBodyStateSource source =
                orientation.Kind ==
                    OrientationResolutionKind.AttentionContinuityHold
                    ? SemanticBodyStateSource.AttentionContinuity
                    : orientation.PriorityRequest != null
                        ? SemanticBodyStateSource.OrientationPriorityRequest
                        : SemanticBodyStateSource.OrientationResolver;
            return new SemanticHeadAttentionSnapshot(
                attention,
                neckStateAvailable
                    ? SemanticBodyStateBasis.Commanded
                    : SemanticBodyStateBasis.Requested,
                source,
                IsPointableAttention(orientation));
        }

        private static bool IsPointableAttention(
            OrientationResolution orientation)
        {
            if (orientation == null ||
                !orientation.IsValid ||
                !orientation.HasTarget)
            {
                return false;
            }

            return orientation.Kind == OrientationResolutionKind.Face ||
                orientation.Kind == OrientationResolutionKind.Object ||
                orientation.Kind == OrientationResolutionKind.HoldPrevious ||
                orientation.Kind ==
                    OrientationResolutionKind.AttentionContinuityHold;
        }

        private static SemanticBodyAction ProjectAction(
            ProductionPoseRuntimeSnapshot freePose,
            SemanticArmSnapshot right,
            SemanticArmSnapshot left)
        {
            if (freePose != null &&
                freePose.ExecutionStateAvailable &&
                freePose.IsActive)
            {
                return SemanticBodyAction.FreePose;
            }

            SemanticBodyAction rightAction = ActionFor(right.Ownership);
            SemanticBodyAction leftAction = ActionFor(left.Ownership);
            if (rightAction == SemanticBodyAction.None)
                return leftAction;
            if (leftAction == SemanticBodyAction.None ||
                leftAction == rightAction)
            {
                return rightAction;
            }
            return SemanticBodyAction.Mixed;
        }

        private static SemanticBodyAction ActionFor(
            SemanticBodyOwnership owner)
        {
            switch (owner)
            {
                case SemanticBodyOwnership.FreePose:
                    return SemanticBodyAction.FreePose;
                case SemanticBodyOwnership.Point:
                    return SemanticBodyAction.Point;
                case SemanticBodyOwnership.Manual:
                    return SemanticBodyAction.Manual;
                case SemanticBodyOwnership.Debug:
                    return SemanticBodyAction.Debug;
                case SemanticBodyOwnership.Other:
                    return SemanticBodyAction.Unknown;
                case SemanticBodyOwnership.None:
                default:
                    return SemanticBodyAction.None;
            }
        }

        private static SemanticBodyOwnership MapOwner(
            bool hasOwner,
            HandTargetOwnerKind owner)
        {
            if (!hasOwner)
                return SemanticBodyOwnership.None;

            switch (owner)
            {
                case HandTargetOwnerKind.FreePose:
                    return SemanticBodyOwnership.FreePose;
                case HandTargetOwnerKind.ProductionPoint:
                    return SemanticBodyOwnership.Point;
                case HandTargetOwnerKind.ManualDrag:
                    return SemanticBodyOwnership.Manual;
                case HandTargetOwnerKind.DebugPoint:
                    return SemanticBodyOwnership.Debug;
                default:
                    return SemanticBodyOwnership.Other;
            }
        }

        private static SemanticArmPose MapPose(string spatialTargetId)
        {
            if (string.IsNullOrWhiteSpace(spatialTargetId))
                return SemanticArmPose.Unknown;
            if (EndsWith(spatialTargetId, "_neutral"))
                return SemanticArmPose.Neutral;
            if (EndsWith(spatialTargetId, "_chest"))
                return SemanticArmPose.Chest;
            if (EndsWith(spatialTargetId, "_waist"))
                return SemanticArmPose.Waist;
            if (EndsWith(spatialTargetId, "_front"))
                return SemanticArmPose.Front;
            if (EndsWith(spatialTargetId, "_side"))
                return SemanticArmPose.Side;
            if (EndsWith(spatialTargetId, "_up"))
                return SemanticArmPose.Up;
            return SemanticArmPose.Unknown;
        }

        private static bool EndsWith(string value, string suffix)
        {
            return value.EndsWith(suffix, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Production read boundary. Concrete body components remain behind this
    /// projection so Conversation depends only on SemanticBodySnapshot.
    /// </summary>
    public static class SemanticBodySnapshotRuntime
    {
        private static readonly SemanticBodySnapshotService service =
            new SemanticBodySnapshotService();

        private static HandTargetAuthority handTargetAuthority;
        private static OrientationTargetResolver orientationTargetResolver;
        private static global::NeckController neckController;

        public static SemanticBodySnapshot Capture(DateTime timestampUtc)
        {
            ResolveSources();
            ProductionPoseRequestRuntime.TryCaptureSnapshot(
                out ProductionPoseRuntimeSnapshot freePose);

            bool authorityAvailable = handTargetAuthority != null;
            HandTargetAuthoritySnapshot right = authorityAvailable
                ? handTargetAuthority.GetSnapshot(HandTargetArm.Right)
                : default;
            HandTargetAuthoritySnapshot left = authorityAvailable
                ? handTargetAuthority.GetSnapshot(HandTargetArm.Left)
                : default;
            OrientationResolution orientation = orientationTargetResolver != null
                ? orientationTargetResolver.CurrentResolution
                : null;
            bool neckAvailable = neckController != null;
            PhysicalNeckMotionPhase neckPhase = neckAvailable
                ? neckController.MotionPhase
                : PhysicalNeckMotionPhase.Tracking;

            return service.Capture(
                freePose,
                authorityAvailable,
                right,
                left,
                orientation,
                neckAvailable,
                neckPhase,
                timestampUtc);
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeReferences()
        {
            handTargetAuthority = null;
            orientationTargetResolver = null;
            neckController = null;
        }

        private static void ResolveSources()
        {
            if (handTargetAuthority == null ||
                !handTargetAuthority.isActiveAndEnabled)
            {
                handTargetAuthority = UnityEngine.Object
                    .FindObjectOfType<HandTargetAuthority>();
                if (handTargetAuthority != null &&
                    !handTargetAuthority.isActiveAndEnabled)
                {
                    handTargetAuthority = null;
                }
            }
            if (orientationTargetResolver == null ||
                !orientationTargetResolver.isActiveAndEnabled)
            {
                orientationTargetResolver = UnityEngine.Object
                    .FindObjectOfType<OrientationTargetResolver>();
                if (orientationTargetResolver != null &&
                    !orientationTargetResolver.isActiveAndEnabled)
                {
                    orientationTargetResolver = null;
                }
            }
            if (neckController == null ||
                !neckController.isActiveAndEnabled)
            {
                neckController = UnityEngine.Object
                    .FindObjectOfType<global::NeckController>();
                if (neckController != null &&
                    !neckController.isActiveAndEnabled)
                {
                    neckController = null;
                }
            }
        }
    }
}
