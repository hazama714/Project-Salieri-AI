// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Expression.Motion;
using SalieriAI.Core.Perception.Attention;

using UnityEngine;

namespace SalieriAI.Body.Retargeting.Neck
{
    public enum VirtualNeckRetargetSourceStatus
    {
        Invalid = 0,
        Valid = 1
    }

    /// <summary>
    /// Read-only candidate physical neck pose. It carries no servo identity
    /// and grants no physical output authority.
    /// </summary>
    [Serializable]
    public sealed class VirtualNeckRetargetShadowState
    {
        [SerializeField] private bool valid;
        [SerializeField] private VirtualNeckRetargetSourceStatus sourceStatus;
        [SerializeField] private string invalidReason = string.Empty;
        [SerializeField] private string targetKey = string.Empty;
        [SerializeField]
        private OrientationResolutionKind sourceKind =
            OrientationResolutionKind.None;
        [SerializeField] private int sourceFrame = -1;
        [SerializeField] private string capturedAtUtc = string.Empty;

        [SerializeField] private float virtualYaw;
        [SerializeField] private float virtualPitch;
        [SerializeField] private float signedYaw;
        [SerializeField] private float signedPitch;
        [SerializeField] private float scaledYaw;
        [SerializeField] private float scaledPitch;
        [SerializeField] private float offsetYaw;
        [SerializeField] private float offsetPitch;
        [SerializeField] private float clampedYaw;
        [SerializeField] private float clampedPitch;
        [SerializeField] private float candidatePhysicalYaw;
        [SerializeField] private float candidatePhysicalPitch;
        [SerializeField] private bool clamped;
        [SerializeField] private bool rateLimited;

        [SerializeField] private bool hasLastValidCandidate;
        [SerializeField] private float lastValidPhysicalYaw;
        [SerializeField] private float lastValidPhysicalPitch;
        [SerializeField] private float neutralCandidateYaw;
        [SerializeField] private float neutralCandidatePitch;
        [SerializeField] private string profileId = string.Empty;
        [SerializeField] private string profileVersion = string.Empty;

        public bool Valid => valid;
        public VirtualNeckRetargetSourceStatus SourceStatus => sourceStatus;
        public string InvalidReason => invalidReason;
        public string TargetKey => targetKey;
        public OrientationResolutionKind SourceKind => sourceKind;
        public int SourceFrame => sourceFrame;
        public string CapturedAtUtc => capturedAtUtc;
        public float VirtualYaw => virtualYaw;
        public float VirtualPitch => virtualPitch;
        public float SignedYaw => signedYaw;
        public float SignedPitch => signedPitch;
        public float ScaledYaw => scaledYaw;
        public float ScaledPitch => scaledPitch;
        public float OffsetYaw => offsetYaw;
        public float OffsetPitch => offsetPitch;
        public float ClampedYaw => clampedYaw;
        public float ClampedPitch => clampedPitch;
        public float CandidatePhysicalYaw => candidatePhysicalYaw;
        public float CandidatePhysicalPitch => candidatePhysicalPitch;
        public bool Clamped => clamped;
        public bool RateLimited => rateLimited;
        public bool HasLastValidCandidate => hasLastValidCandidate;
        public float LastValidPhysicalYaw => lastValidPhysicalYaw;
        public float LastValidPhysicalPitch => lastValidPhysicalPitch;
        public float NeutralCandidateYaw => neutralCandidateYaw;
        public float NeutralCandidatePitch => neutralCandidatePitch;
        public string ProfileId => profileId;
        public string ProfileVersion => profileVersion;

        internal void SetValid(
            VirtualNeckPoseState source,
            VirtualNeckRetargetEvaluation evaluation,
            VirtualNeckRetargetProfile profile)
        {
            SetValid(
                source,
                evaluation,
                profile,
                string.IsNullOrEmpty(source != null ? source.TargetKey : null)
                    ? OrientationResolutionKind.None
                    : OrientationResolutionKind.Object);
        }

        internal void SetValid(
            VirtualNeckPoseState source,
            VirtualNeckRetargetEvaluation evaluation,
            VirtualNeckRetargetProfile profile,
            OrientationResolutionKind resolvedSourceKind)
        {
            valid = true;
            sourceStatus = VirtualNeckRetargetSourceStatus.Valid;
            invalidReason = string.Empty;
            targetKey = source.TargetKey ?? string.Empty;
            sourceKind = resolvedSourceKind;
            sourceFrame = source.CapturedFrame;
            capturedAtUtc = source.CapturedAtUtc ?? string.Empty;
            ApplyEvaluation(evaluation);
            hasLastValidCandidate = true;
            lastValidPhysicalYaw = candidatePhysicalYaw;
            lastValidPhysicalPitch = candidatePhysicalPitch;
            profileId = profile.ProfileId;
            profileVersion = profile.ProfileVersion;
        }

        internal void SetInvalid(
            VirtualNeckPoseState source,
            string reason,
            bool hasLastValid,
            float lastYaw,
            float lastPitch,
            VirtualNeckRetargetEvaluation neutral,
            VirtualNeckRetargetProfile profile)
        {
            valid = false;
            sourceStatus = VirtualNeckRetargetSourceStatus.Invalid;
            invalidReason = reason ?? string.Empty;
            targetKey = source != null ? source.TargetKey ?? string.Empty : string.Empty;
            sourceKind = OrientationResolutionKind.None;
            sourceFrame = source != null ? source.CapturedFrame : -1;
            capturedAtUtc = source != null
                ? source.CapturedAtUtc ?? string.Empty
                : string.Empty;
            virtualYaw = 0f;
            virtualPitch = 0f;
            signedYaw = 0f;
            signedPitch = 0f;
            scaledYaw = 0f;
            scaledPitch = 0f;
            offsetYaw = 0f;
            offsetPitch = 0f;
            clampedYaw = 0f;
            clampedPitch = 0f;
            candidatePhysicalYaw = hasLastValid ? lastYaw : 0f;
            candidatePhysicalPitch = hasLastValid ? lastPitch : 0f;
            clamped = false;
            rateLimited = false;
            hasLastValidCandidate = hasLastValid;
            lastValidPhysicalYaw = hasLastValid ? lastYaw : 0f;
            lastValidPhysicalPitch = hasLastValid ? lastPitch : 0f;
            neutralCandidateYaw = neutral != null
                ? neutral.ClampedYaw
                : 0f;
            neutralCandidatePitch = neutral != null
                ? neutral.ClampedPitch
                : 0f;
            profileId = profile != null ? profile.ProfileId : string.Empty;
            profileVersion = profile != null
                ? profile.ProfileVersion
                : string.Empty;
        }

        private void ApplyEvaluation(VirtualNeckRetargetEvaluation value)
        {
            virtualYaw = value.VirtualYaw;
            virtualPitch = value.VirtualPitch;
            signedYaw = value.SignedYaw;
            signedPitch = value.SignedPitch;
            scaledYaw = value.ScaledYaw;
            scaledPitch = value.ScaledPitch;
            offsetYaw = value.OffsetYaw;
            offsetPitch = value.OffsetPitch;
            clampedYaw = value.ClampedYaw;
            clampedPitch = value.ClampedPitch;
            candidatePhysicalYaw = value.RateLimitedYaw;
            candidatePhysicalPitch = value.RateLimitedPitch;
            clamped = value.Clamped;
            rateLimited = value.RateLimited;
        }
    }
}
