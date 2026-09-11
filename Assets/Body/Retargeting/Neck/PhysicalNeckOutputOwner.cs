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
    /// Single ownership boundary for physical neck output.
    /// Phase 4 accepts only a solved Virtual Body retarget candidate and
    /// delegates it to the existing guarded physical output controller.
    /// It owns no target selection, mode switch, servo identity, or safety
    /// policy.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PhysicalNeckOutputOwner : MonoBehaviour
    {
        public const string ContractVersion =
            "salieri.physical-neck-output-owner.v0.2-canonical";

        [Header("Guarded Physical Output")]
        [SerializeField]
        private FaceTrackingOutputController outputController;

        [Header("Runtime State (Read Only)")]
        [SerializeField] private string lastTargetKey = string.Empty;
        [SerializeField] private float lastAcceptedYaw;
        [SerializeField] private float lastAcceptedPitch;
        [SerializeField] private int acceptedCandidateCount;
        [SerializeField] private int rejectedCandidateCount;

        public string LastTargetKey => lastTargetKey;
        public float LastAcceptedYaw => lastAcceptedYaw;
        public float LastAcceptedPitch => lastAcceptedPitch;
        public int AcceptedCandidateCount => acceptedCandidateCount;
        public int RejectedCandidateCount => rejectedCandidateCount;

        public bool TrySubmitVirtualBodyRetarget(
            VirtualNeckRetargetShadowState state)
        {
            if (state == null ||
                !state.Valid ||
                outputController == null ||
                !IsFinite(state.CandidatePhysicalYaw) ||
                !IsFinite(state.CandidatePhysicalPitch))
            {
                rejectedCandidateCount++;
                return false;
            }

            outputController.AcceptOwnedVirtualRetargetState(state);

            lastAcceptedYaw = state.CandidatePhysicalYaw;
            lastAcceptedPitch = state.CandidatePhysicalPitch;
            lastTargetKey = state.TargetKey ?? string.Empty;
            acceptedCandidateCount++;
            return true;
        }

        /// <summary>
        /// Propagates source loss through the same exclusive owner without
        /// promoting an invalid retarget candidate to an accepted command.
        /// </summary>
        public void NotifyVirtualBodyTargetLost(
            VirtualNeckRetargetShadowState state)
        {
            if (outputController == null)
                return;

            outputController.AcceptOwnedVirtualRetargetUnavailable(state);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
