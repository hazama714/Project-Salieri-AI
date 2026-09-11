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

using UnityEngine;

namespace SalieriAI.Body.UpperBody.Actuation.Timing
{
    /// <summary>
    /// Canonical logical stages for one Humanoid Animator evaluation. These
    /// are ordering facts, not an alternate PlayerLoop implementation.
    /// </summary>
    public enum UpperBodyAnimatorApplicationStage
    {
        Unknown = 0,
        AnimatorBasePoseEvaluated = 10,
        VirtualPoseCandidateEvaluated = 20,
        VirtualPoseBlendEvaluated = 30,
        ChestAppliedDuringAnimatorIk = 40,
        AnimatorLookAtConfigured = 50,
        AnimatorIkSolved = 60,
        SolvedPoseObservedInLateUpdate = 70,
        VirtualPoseRetargeted = 80,
        PhysicalBodyOutput = 90
    }

    public enum UpperBodyAnimatorCallbackBoundary
    {
        Unknown = 0,
        UnityAnimationEvaluation = 1,
        SingleAnimatorIkCoordinator = 2,
        UnityInternalAnimatorIkSolve = 3,
        OrderedLateUpdateObservation = 4,
        RetargetingAfterObservation = 5
    }

    public enum UpperBodyHumanoidWriteApi
    {
        None = 0,
        AnimatorSetBoneLocalRotation = 1
    }

    public sealed class UpperBodyAnimatorApplicationStep
    {
        public UpperBodyAnimatorApplicationStage Stage { get; }
        public UpperBodyAnimatorCallbackBoundary Boundary { get; }
        public bool WritesHumanoidBone { get; }
        public BodySegment WriteSegment { get; }

        internal UpperBodyAnimatorApplicationStep(
            UpperBodyAnimatorApplicationStage stage,
            UpperBodyAnimatorCallbackBoundary boundary,
            bool writesHumanoidBone,
            BodySegment writeSegment)
        {
            Stage = stage;
            Boundary = boundary;
            WritesHumanoidBone = writesHumanoidBone;
            WriteSegment = writeSegment;
        }
    }

    /// <summary>
    /// U0-6 order contract. Production Chest writing is deliberately disabled.
    /// U1 must implement the two coordinator stages in one OnAnimatorIK call.
    /// </summary>
    public static class UpperBodyAnimatorApplicationOrderContract
    {
        public const string ContractVersion =
            "upper-body-animator-application-order-v0.1";
        public const int NeckSolvedPoseReaderOrder = 50;
        public const int BodyFrameObservationOrder = 75;

        private static readonly UpperBodyAnimatorApplicationStep[] Steps =
        {
            Step(
                UpperBodyAnimatorApplicationStage.AnimatorBasePoseEvaluated,
                UpperBodyAnimatorCallbackBoundary.UnityAnimationEvaluation),
            Step(
                UpperBodyAnimatorApplicationStage.VirtualPoseCandidateEvaluated,
                UpperBodyAnimatorCallbackBoundary.SingleAnimatorIkCoordinator),
            Step(
                UpperBodyAnimatorApplicationStage.VirtualPoseBlendEvaluated,
                UpperBodyAnimatorCallbackBoundary.SingleAnimatorIkCoordinator),
            new UpperBodyAnimatorApplicationStep(
                UpperBodyAnimatorApplicationStage.ChestAppliedDuringAnimatorIk,
                UpperBodyAnimatorCallbackBoundary.SingleAnimatorIkCoordinator,
                true,
                BodySegment.Chest),
            Step(
                UpperBodyAnimatorApplicationStage.AnimatorLookAtConfigured,
                UpperBodyAnimatorCallbackBoundary.SingleAnimatorIkCoordinator),
            Step(
                UpperBodyAnimatorApplicationStage.AnimatorIkSolved,
                UpperBodyAnimatorCallbackBoundary.UnityInternalAnimatorIkSolve),
            Step(
                UpperBodyAnimatorApplicationStage.SolvedPoseObservedInLateUpdate,
                UpperBodyAnimatorCallbackBoundary.OrderedLateUpdateObservation),
            Step(
                UpperBodyAnimatorApplicationStage.VirtualPoseRetargeted,
                UpperBodyAnimatorCallbackBoundary.RetargetingAfterObservation),
            Step(
                UpperBodyAnimatorApplicationStage.PhysicalBodyOutput,
                UpperBodyAnimatorCallbackBoundary.RetargetingAfterObservation)
        };

        public static IReadOnlyList<UpperBodyAnimatorApplicationStep>
            CanonicalSteps => Steps;

        public static bool ProductionChestWritingEnabled => false;
        public static BodySegment ProductionWriteSegment => BodySegment.Chest;
        public static UpperBodyHumanoidWriteApi RequiredWriteApi =>
            UpperBodyHumanoidWriteApi.AnimatorSetBoneLocalRotation;
        public static bool RequiresSingleAnimatorIkCoordinator => true;
        public static bool SeparateOnAnimatorIkExecutionOrderIsCanonical => false;
        public static bool LateUpdateTransformWritingAllowed => false;
        public static bool U04LateUpdateSolutionCanDriveSameFrameAnimatorIk => false;
        public static bool LookAtOwnsChest => false;
        public static bool ChestWriterMayWriteNeckOrHead => false;
        public static bool BlendMustCompleteBeforeChestWrite => true;
        public static bool BodyOrientationMayBeWritten => false;
        public static Quaternion ChestNeutralRelativeRotation =>
            Quaternion.identity;

        public static int IndexOf(UpperBodyAnimatorApplicationStage stage)
        {
            for (int i = 0; i < Steps.Length; i++)
                if (Steps[i].Stage == stage)
                    return i;
            return -1;
        }

        public static bool IsBefore(
            UpperBodyAnimatorApplicationStage first,
            UpperBodyAnimatorApplicationStage second)
        {
            int firstIndex = IndexOf(first);
            int secondIndex = IndexOf(second);
            return firstIndex >= 0 && secondIndex >= 0 &&
                   firstIndex < secondIndex;
        }

        public static Quaternion ComposeChestLocalRotation(
            Quaternion animatorBaseLocalRotation,
            Quaternion candidateNeutralRelativeRotation)
        {
            return Normalize(
                animatorBaseLocalRotation * candidateNeutralRelativeRotation);
        }

        private static UpperBodyAnimatorApplicationStep Step(
            UpperBodyAnimatorApplicationStage stage,
            UpperBodyAnimatorCallbackBoundary boundary) =>
            new UpperBodyAnimatorApplicationStep(
                stage, boundary, false, BodySegment.Unknown);

        private static Quaternion Normalize(Quaternion value)
        {
            float magnitude = Mathf.Sqrt(
                value.x * value.x + value.y * value.y +
                value.z * value.z + value.w * value.w);
            if (magnitude < 0.000001f || float.IsNaN(magnitude) ||
                float.IsInfinity(magnitude))
                throw new ArgumentException("Rotation must be finite and non-zero.");
            float inverse = 1f / magnitude;
            return new Quaternion(
                value.x * inverse,
                value.y * inverse,
                value.z * inverse,
                value.w * inverse);
        }
    }
}
