// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

namespace SalieriAI.Body.UpperBody.Actuation.Timing
{
    /// <summary>
    /// Pure correlation record for future U1 diagnostics. It records facts
    /// supplied by the outer runtime and never drives Animator or bones.
    /// </summary>
    public sealed class UpperBodyAnimatorFrameTimingTrace
    {
        public string TargetKey { get; }
        public int CandidateFrame { get; }
        public int CoordinatorOnAnimatorIkFrame { get; }
        public int ChestAppliedFrame { get; }
        public int LookAtConfiguredFrame { get; }
        public int SolvedPoseObservedFrame { get; }
        public DateTime ObservedAtUtc { get; }

        private UpperBodyAnimatorFrameTimingTrace(
            string targetKey,
            int candidateFrame,
            int coordinatorOnAnimatorIkFrame,
            int chestAppliedFrame,
            int lookAtConfiguredFrame,
            int solvedPoseObservedFrame,
            DateTime observedAtUtc)
        {
            TargetKey = targetKey;
            CandidateFrame = candidateFrame;
            CoordinatorOnAnimatorIkFrame = coordinatorOnAnimatorIkFrame;
            ChestAppliedFrame = chestAppliedFrame;
            LookAtConfiguredFrame = lookAtConfiguredFrame;
            SolvedPoseObservedFrame = solvedPoseObservedFrame;
            ObservedAtUtc = observedAtUtc;
        }

        public bool CandidateAndChestAreSameFrame =>
            CandidateFrame == CoordinatorOnAnimatorIkFrame &&
            CoordinatorOnAnimatorIkFrame == ChestAppliedFrame;
        public bool ChestAndLookAtAreSameFrame =>
            ChestAppliedFrame == LookAtConfiguredFrame;
        public bool LookAtAndObservationAreSameFrame =>
            LookAtConfiguredFrame == SolvedPoseObservedFrame;
        public bool HasCanonicalSameFrameCorrelation =>
            CandidateAndChestAreSameFrame &&
            ChestAndLookAtAreSameFrame &&
            LookAtAndObservationAreSameFrame;

        public static bool TryCreate(
            string targetKey,
            int candidateFrame,
            int chestAppliedFrame,
            int lookAtConfiguredFrame,
            int solvedPoseObservedFrame,
            DateTime observedAtUtc,
            out UpperBodyAnimatorFrameTimingTrace trace,
            out string error)
        {
            return TryCreate(
                targetKey,
                candidateFrame,
                chestAppliedFrame,
                chestAppliedFrame,
                lookAtConfiguredFrame,
                solvedPoseObservedFrame,
                observedAtUtc,
                out trace,
                out error);
        }

        public static bool TryCreate(
            string targetKey,
            int candidateFrame,
            int coordinatorOnAnimatorIkFrame,
            int chestAppliedFrame,
            int lookAtConfiguredFrame,
            int solvedPoseObservedFrame,
            DateTime observedAtUtc,
            out UpperBodyAnimatorFrameTimingTrace trace,
            out string error)
        {
            trace = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(targetKey))
            {
                error = "Target key is required.";
                return false;
            }
            if (candidateFrame < 0 || coordinatorOnAnimatorIkFrame < 0 ||
                chestAppliedFrame < 0 ||
                lookAtConfiguredFrame < 0 || solvedPoseObservedFrame < 0)
            {
                error = "All correlated frames must be non-negative.";
                return false;
            }
            if (observedAtUtc.Kind != DateTimeKind.Utc)
            {
                error = "Observation time must be UTC.";
                return false;
            }
            if (coordinatorOnAnimatorIkFrame < candidateFrame ||
                chestAppliedFrame < coordinatorOnAnimatorIkFrame ||
                lookAtConfiguredFrame < chestAppliedFrame ||
                solvedPoseObservedFrame < lookAtConfiguredFrame)
            {
                error = "Frame correlation must not move backward.";
                return false;
            }

            trace = new UpperBodyAnimatorFrameTimingTrace(
                targetKey.Trim(),
                candidateFrame,
                coordinatorOnAnimatorIkFrame,
                chestAppliedFrame,
                lookAtConfiguredFrame,
                solvedPoseObservedFrame,
                observedAtUtc);
            return true;
        }
    }
}
