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

namespace SalieriAI.Body.UpperBody.Shadow
{
    public sealed class UpperBodyShadowSegmentSolution
    {
        public BodySegment Segment { get; }
        public BodyReferenceFrame ReferenceFrame { get; }
        public BodySegment ReferenceSegment { get; }
        public bool Enabled { get; }
        public bool Available { get; }
        public bool Clamped { get; }
        public float ContributionYawDegrees { get; }
        public float ContributionPitchDegrees { get; }
        public BodySegmentOrientationState CandidateState { get; }
        public Quaternion CandidateNeutralRelativeRotation =>
            CandidateState.NeutralRelativeRotation;
        public Vector3 CandidateForward => CandidateState.Forward;

        internal UpperBodyShadowSegmentSolution(
            BodySegment segment,
            BodyReferenceFrame referenceFrame,
            BodySegment referenceSegment,
            bool enabled,
            bool available,
            bool clamped,
            float yaw,
            float pitch,
            BodySegmentOrientationState candidateState)
        {
            Segment = segment;
            ReferenceFrame = referenceFrame;
            ReferenceSegment = referenceSegment;
            Enabled = enabled;
            Available = available;
            Clamped = clamped;
            ContributionYawDegrees = yaw;
            ContributionPitchDegrees = pitch;
            CandidateState = candidateState ??
                throw new ArgumentNullException(nameof(candidateState));
        }
    }

    /// <summary>
    /// Immutable read-only candidate pose IR. It contains no physical joint,
    /// servo, transport, or hardware identity.
    /// </summary>
    public sealed class UpperBodyShadowSolution
    {
        private readonly UpperBodyShadowSegmentSolution[] segments;

        public bool Valid { get; }
        public string TargetKey { get; }
        public int SourceFrame { get; }
        public DateTime CapturedAtUtc { get; }
        public Quaternion DesiredBodyRelativeRotation { get; }
        public BodyReferenceFrame ReconstructedReferenceFrame =>
            BodyReferenceFrame.BodyOrientation;
        public IReadOnlyList<UpperBodyShadowSegmentSolution> Segments => segments;
        public Vector3 ReconstructedHeadForward { get; }
        public float ResidualTargetErrorDegrees { get; }
        public string SolverProfileId { get; }
        public string SolverProfileVersion { get; }
        public string Error { get; }

        internal UpperBodyShadowSolution(
            bool valid,
            string targetKey,
            int sourceFrame,
            DateTime capturedAtUtc,
            Quaternion desiredBodyRelativeRotation,
            UpperBodyShadowSegmentSolution[] segments,
            Vector3 reconstructedHeadForward,
            float residualTargetErrorDegrees,
            string solverProfileId,
            string solverProfileVersion,
            string error)
        {
            Valid = valid;
            TargetKey = targetKey ?? string.Empty;
            SourceFrame = sourceFrame;
            CapturedAtUtc = capturedAtUtc;
            DesiredBodyRelativeRotation = desiredBodyRelativeRotation;
            this.segments = segments ?? Array.Empty<UpperBodyShadowSegmentSolution>();
            ReconstructedHeadForward = reconstructedHeadForward;
            ResidualTargetErrorDegrees = residualTargetErrorDegrees;
            SolverProfileId = solverProfileId ?? string.Empty;
            SolverProfileVersion = solverProfileVersion ?? string.Empty;
            Error = error ?? string.Empty;
        }

        public UpperBodyShadowSegmentSolution Find(BodySegment segment)
        {
            for (int i = 0; i < segments.Length; i++)
                if (segments[i].Segment == segment)
                    return segments[i];
            return null;
        }

        public static UpperBodyShadowSolution CreateInvalid(
            string targetKey,
            int sourceFrame,
            DateTime capturedAtUtc,
            string profileId,
            string profileVersion,
            string error)
        {
            if (capturedAtUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("Captured time must be UTC.");
            return new UpperBodyShadowSolution(
                false,
                targetKey,
                sourceFrame,
                capturedAtUtc,
                Quaternion.identity,
                Array.Empty<UpperBodyShadowSegmentSolution>(),
                Vector3.forward,
                180f,
                profileId,
                profileVersion,
                error);
        }
    }
}
