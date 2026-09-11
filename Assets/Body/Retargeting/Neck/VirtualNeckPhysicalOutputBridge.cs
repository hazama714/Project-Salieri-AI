// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

namespace SalieriAI.Body.Retargeting.Neck
{
    /// <summary>
    /// Submits the Phase 2 read-only candidate to the exclusive output owner.
    /// This bridge has no NeckController, servo, command coordinator, mode,
    /// or safety authority. Phase 4 has no alternate production candidate.
    /// </summary>
    [DefaultExecutionOrder(650)]
    [DisallowMultipleComponent]
    public sealed class VirtualNeckPhysicalOutputBridge : MonoBehaviour
    {
        [SerializeField]
        private VirtualNeckRetargetShadow retargetShadow;

        [SerializeField]
        private PhysicalNeckOutputOwner outputOwner;

        [Header("Runtime State (Read Only)")]
        [SerializeField] private bool sourceValid;
        [SerializeField] private bool acceptedByOwner;
        [SerializeField] private string targetKey = string.Empty;
        [SerializeField] private float submittedYaw;
        [SerializeField] private float submittedPitch;
        [SerializeField] private bool upstreamRateLimiterDisabled = true;

        private bool loggedDoubleLimiterBlock;

        public bool SourceValid => sourceValid;
        public bool AcceptedByOwner => acceptedByOwner;
        public string TargetKey => targetKey;
        public float SubmittedYaw => submittedYaw;
        public float SubmittedPitch => submittedPitch;
        public bool UpstreamRateLimiterDisabled =>
            upstreamRateLimiterDisabled;

        private void LateUpdate()
        {
            VirtualNeckRetargetShadowState state = retargetShadow != null
                ? retargetShadow.CurrentState
                : null;

            sourceValid = state != null && state.Valid;
            targetKey = state != null ? state.TargetKey : string.Empty;
            submittedYaw = sourceValid ? state.CandidatePhysicalYaw : 0f;
            submittedPitch = sourceValid
                ? state.CandidatePhysicalPitch
                : 0f;
            upstreamRateLimiterDisabled = IsUpstreamRateLimiterDisabled();
            if (!upstreamRateLimiterDisabled)
            {
                acceptedByOwner = false;
                if (!loggedDoubleLimiterBlock)
                {
                    Debug.LogError(
                        "[VirtualNeckPhysicalOutputBridge] " +
                        "Blocked: upstream and final physical neck rate " +
                        "limiters cannot be active simultaneously.",
                        this);
                    loggedDoubleLimiterBlock = true;
                }
                return;
            }

            loggedDoubleLimiterBlock = false;
            if (sourceValid && outputOwner != null)
            {
                acceptedByOwner =
                    outputOwner.TrySubmitVirtualBodyRetarget(state);
            }
            else
            {
                acceptedByOwner = false;
                if (state != null && outputOwner != null)
                    outputOwner.NotifyVirtualBodyTargetLost(state);
            }
        }

        private bool IsUpstreamRateLimiterDisabled()
        {
            VirtualNeckRetargetProfile activeProfile = retargetShadow != null
                ? retargetShadow.Profile
                : null;
            return activeProfile == null ||
                (Mathf.Approximately(
                    activeProfile.YawMaxSpeedDegreesPerSecond, 0f) &&
                 Mathf.Approximately(
                    activeProfile.PitchMaxSpeedDegreesPerSecond, 0f));
        }
    }
}
