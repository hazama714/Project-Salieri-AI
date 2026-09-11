// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using SalieriAI.Expression.Motion;

using UnityEngine;

namespace SalieriAI.Body.Retargeting.Neck
{
    /// <summary>
    /// Observes the solved virtual neck pose and computes a candidate physical
    /// neck pose. It never writes to a controller, servo, bridge, or command.
    /// </summary>
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class VirtualNeckRetargetShadow : MonoBehaviour
    {
        [Header("Read-only Source")]
        [SerializeField] private VRMNeckSolvedPoseReader solvedPoseReader;

        [Header("Retarget Profile")]
        [SerializeField]
        private VirtualNeckRetargetProfile profile =
            new VirtualNeckRetargetProfile();

        [Header("Shadow State (Read Only)")]
        [SerializeField]
        private VirtualNeckRetargetShadowState currentState =
            new VirtualNeckRetargetShadowState();

        private bool hasPreviousCandidate;
        private float previousCandidateYaw;
        private float previousCandidatePitch;
        private bool hasLastValidCandidate;
        private float lastValidCandidateYaw;
        private float lastValidCandidatePitch;

        public VirtualNeckRetargetProfile Profile => profile;
        public VirtualNeckRetargetShadowState CurrentState => currentState;

        private void LateUpdate()
        {
            EvaluateCurrent(Time.deltaTime);
        }

        public void EvaluateCurrent(float deltaTimeSeconds)
        {
            if (currentState == null)
                currentState = new VirtualNeckRetargetShadowState();

            VirtualNeckPoseState source = solvedPoseReader != null
                ? solvedPoseReader.CurrentPose
                : null;

            VirtualNeckRetargetMath.TryEvaluate(
                Vector3.forward,
                profile,
                false,
                0f,
                0f,
                0f,
                out VirtualNeckRetargetEvaluation neutral,
                out string neutralError);

            if (source == null || !source.Valid)
            {
                currentState.SetInvalid(
                    source,
                    source == null
                        ? "Solved pose source is missing."
                        : "Solved pose is invalid.",
                    hasLastValidCandidate,
                    lastValidCandidateYaw,
                    lastValidCandidatePitch,
                    neutral,
                    profile);
                return;
            }

            if (!VirtualNeckRetargetMath.TryEvaluate(
                    source.HeadForwardInBodyFrame,
                    profile,
                    hasPreviousCandidate,
                    previousCandidateYaw,
                    previousCandidatePitch,
                    deltaTimeSeconds,
                    out VirtualNeckRetargetEvaluation evaluation,
                    out string error))
            {
                currentState.SetInvalid(
                    source,
                    string.IsNullOrEmpty(error) ? neutralError : error,
                    hasLastValidCandidate,
                    lastValidCandidateYaw,
                    lastValidCandidatePitch,
                    neutral,
                    profile);
                return;
            }

            currentState.SetValid(
                source,
                evaluation,
                profile,
                solvedPoseReader != null
                    ? solvedPoseReader.CurrentResolutionKind
                    : SalieriAI.Core.Perception.Attention
                        .OrientationResolutionKind.None);
            hasPreviousCandidate = true;
            previousCandidateYaw = evaluation.RateLimitedYaw;
            previousCandidatePitch = evaluation.RateLimitedPitch;
            hasLastValidCandidate = true;
            lastValidCandidateYaw = evaluation.RateLimitedYaw;
            lastValidCandidatePitch = evaluation.RateLimitedPitch;
        }

        public void ResetRateLimitHistory()
        {
            hasPreviousCandidate = false;
            previousCandidateYaw = 0f;
            previousCandidatePitch = 0f;
        }
    }
}
