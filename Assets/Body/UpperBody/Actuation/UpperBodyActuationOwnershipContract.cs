// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;

using SalieriAI.Body.Frames;

namespace SalieriAI.Body.UpperBody.Actuation
{
    /// <summary>
    /// Identifies the component that owns the final virtual pose for a body
    /// segment. An owner is a semantic authority, not an execution order.
    /// </summary>
    public enum UpperBodyActuationOwner
    {
        None = 0,
        UnityHumanoidAnimator = 1,
        AnimatorLookAt = 2,
        CanonicalUpperBodyVirtualPoseWriter = 3
    }

    public enum UpperBodyActuationAccess
    {
        Denied = 0,
        ReadOnlyObservation = 1,
        ShadowEvaluation = 2,
        ExclusiveProductionWrite = 3
    }

    public enum UpperBodyProfileAuthority
    {
        Unknown = 0,
        ExplicitVersionedConfiguration = 1
    }

    public enum UpperBodyTargetAuthority
    {
        Unknown = 0,
        OrientationTargetResolver = 1
    }

    public enum UpperBodyBlendAuthority
    {
        Unknown = 0,
        VirtualBodyActuationLayer = 1
    }

    public enum CanonicalBodyOrientationAuthority
    {
        Unknown = 0,
        ExternalCanonicalBodyFrame = 1
    }

    /// <summary>
    /// Immutable ownership row for one canonical upper-body segment.
    /// CurrentFinalPoseOwner describes the existing final writer. The future
    /// owner is a contract only until a later phase explicitly enables it.
    /// </summary>
    public sealed class UpperBodySegmentActuationOwnership
    {
        public BodySegment Segment { get; }
        public bool Available { get; }
        public UpperBodyActuationOwner CurrentFinalPoseOwner { get; }
        public UpperBodyActuationOwner FutureCanonicalOwner { get; }
        public UpperBodyActuationAccess SolverAccess { get; }
        public UpperBodyActuationAccess ObserverAccess { get; }
        public UpperBodyActuationAccess FutureWriterAccess { get; }

        internal UpperBodySegmentActuationOwnership(
            BodySegment segment,
            bool available,
            UpperBodyActuationOwner currentFinalPoseOwner,
            UpperBodyActuationOwner futureCanonicalOwner,
            UpperBodyActuationAccess solverAccess,
            UpperBodyActuationAccess observerAccess,
            UpperBodyActuationAccess futureWriterAccess)
        {
            if (segment == BodySegment.Unknown)
                throw new ArgumentException("Segment must be explicit.", nameof(segment));

            Segment = segment;
            Available = available;
            CurrentFinalPoseOwner = currentFinalPoseOwner;
            FutureCanonicalOwner = futureCanonicalOwner;
            SolverAccess = solverAccess;
            ObserverAccess = observerAccess;
            FutureWriterAccess = futureWriterAccess;
        }
    }

    /// <summary>
    /// U0-5 canonical ownership and arbitration contract. It contains no
    /// Unity Transform, Animator, physical joint, servo, or transport state.
    /// Production upper-body writing remains disabled in this phase.
    /// </summary>
    public static class UpperBodyActuationOwnershipContract
    {
        public const string ContractVersion =
            "upper-body-actuation-ownership-v0.1";
        public const string TargetAuthorityComponent =
            "OrientationTargetResolver";

        private static readonly UpperBodySegmentActuationOwnership[] Rows =
        {
            CreateTrunkRow(BodySegment.Pelvis),
            CreateTrunkRow(BodySegment.Spine),
            CreateTrunkRow(BodySegment.Chest),
            new UpperBodySegmentActuationOwnership(
                BodySegment.UpperChest,
                false,
                UpperBodyActuationOwner.None,
                UpperBodyActuationOwner.None,
                UpperBodyActuationAccess.ShadowEvaluation,
                UpperBodyActuationAccess.ReadOnlyObservation,
                UpperBodyActuationAccess.Denied),
            CreateAnimatorLookAtRow(BodySegment.Neck),
            CreateAnimatorLookAtRow(BodySegment.Head)
        };

        public static IReadOnlyList<UpperBodySegmentActuationOwnership>
            SegmentOwnership => Rows;

        public static UpperBodyProfileAuthority ProfileAuthority =>
            UpperBodyProfileAuthority.ExplicitVersionedConfiguration;

        public static UpperBodyTargetAuthority TargetAuthority =>
            UpperBodyTargetAuthority.OrientationTargetResolver;

        public static UpperBodyBlendAuthority BlendAuthority =>
            UpperBodyBlendAuthority.VirtualBodyActuationLayer;

        public static CanonicalBodyOrientationAuthority BodyOrientationAuthority =>
            CanonicalBodyOrientationAuthority.ExternalCanonicalBodyFrame;

        public static bool ProductionWritingEnabled => false;
        public static bool SolverOwnsTargetSelection => false;
        public static bool SolverWritesVirtualBody => false;
        public static bool WriterMayChangeBodyOrientationFrame => false;
        public static bool BehaviorMayMutateProfile => false;

        public static UpperBodySegmentActuationOwnership Find(
            BodySegment segment)
        {
            for (int i = 0; i < Rows.Length; i++)
                if (Rows[i].Segment == segment)
                    return Rows[i];
            return null;
        }

        public static bool IsFutureCanonicalWriterSegment(
            BodySegment segment)
        {
            UpperBodySegmentActuationOwnership row = Find(segment);
            return row != null && row.Available &&
                   row.FutureCanonicalOwner ==
                       UpperBodyActuationOwner.CanonicalUpperBodyVirtualPoseWriter &&
                   row.FutureWriterAccess ==
                       UpperBodyActuationAccess.ExclusiveProductionWrite;
        }

        private static UpperBodySegmentActuationOwnership CreateTrunkRow(
            BodySegment segment) =>
            new UpperBodySegmentActuationOwnership(
                segment,
                true,
                UpperBodyActuationOwner.UnityHumanoidAnimator,
                UpperBodyActuationOwner.CanonicalUpperBodyVirtualPoseWriter,
                UpperBodyActuationAccess.ShadowEvaluation,
                UpperBodyActuationAccess.ReadOnlyObservation,
                UpperBodyActuationAccess.ExclusiveProductionWrite);

        private static UpperBodySegmentActuationOwnership
            CreateAnimatorLookAtRow(BodySegment segment) =>
            new UpperBodySegmentActuationOwnership(
                segment,
                true,
                UpperBodyActuationOwner.AnimatorLookAt,
                UpperBodyActuationOwner.AnimatorLookAt,
                UpperBodyActuationAccess.ShadowEvaluation,
                UpperBodyActuationAccess.ReadOnlyObservation,
                UpperBodyActuationAccess.Denied);
    }
}
