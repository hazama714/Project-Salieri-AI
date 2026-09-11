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
using SalieriAI.Body.Frames.Canonical;
using SalieriAI.Body.Frames.Observation;
using SalieriAI.Core.Perception.Attention;

using UnityEngine;
using SalieriAI.Core.Diagnostics.Performance;

namespace SalieriAI.Body.UpperBody.Shadow
{
    /// <summary>
    /// Read-only MainScene adapter. It converts the existing resolved target
    /// into a canonical body-relative direction, then calls the pure solver.
    /// It owns no target policy and writes no VRM or physical Transform.
    /// </summary>
    [DefaultExecutionOrder(500)]
    [DisallowMultipleComponent]
    public sealed class UpperBodyShadowRuntimeHost : MonoBehaviour
    {
        [Header("Canonical Input")]
        [SerializeField] private CanonicalBodyFrameHost canonicalBodyFrames;
        [SerializeField] private OrientationResolutionTargetDriver targetDriver;
        [SerializeField] private BodyFrameObservationProvider observationProvider;

        [Header("Runtime State (Read Only)")]
        [SerializeField] private bool valid;
        [SerializeField] private string targetKey = string.Empty;
        [SerializeField] private OrientationResolutionKind targetKind;
        [SerializeField] private int sourceFrame = -1;
        [SerializeField] private string capturedAtUtc = string.Empty;
        [SerializeField] private Vector3 bodyRelativeTargetDirection = Vector3.forward;
        [SerializeField] private Quaternion desiredBodyRelativeRotation = Quaternion.identity;
        [SerializeField] private Vector3 reconstructedHeadForward = Vector3.forward;
        [SerializeField] private float residualTargetErrorDegrees = 180f;
        [SerializeField] private string profileId = string.Empty;
        [SerializeField] private string profileVersion = string.Empty;
        [SerializeField] private string status = "NotCaptured";

        [Header("Segment Candidates (Read Only)")]
        [SerializeField] private Quaternion pelvisCandidate = Quaternion.identity;
        [SerializeField] private Quaternion spineCandidate = Quaternion.identity;
        [SerializeField] private Quaternion chestCandidate = Quaternion.identity;
        [SerializeField] private bool upperChestAvailable;
        [SerializeField] private Quaternion neckCandidate = Quaternion.identity;
        [SerializeField] private Quaternion headCandidate = Quaternion.identity;

        [Header("Optional Diagnostics")]
        [SerializeField] private bool logSolution;
        [Min(1)]
        [SerializeField] private int logIntervalFrames = 120;

        private readonly UpperBodyShadowSolver solver = new UpperBodyShadowSolver();
        private UpperBodyOrientationDistributionProfile profile;

        public bool Valid => valid;
        public string TargetKey => targetKey;
        public OrientationResolutionKind TargetKind => targetKind;
        public int SourceFrame => sourceFrame;
        public string CapturedAtUtc => capturedAtUtc;
        public Vector3 BodyRelativeTargetDirection => bodyRelativeTargetDirection;
        public Quaternion DesiredBodyRelativeRotation => desiredBodyRelativeRotation;
        public Vector3 ReconstructedHeadForward => reconstructedHeadForward;
        public float ResidualTargetErrorDegrees => residualTargetErrorDegrees;
        public Quaternion PelvisCandidate => pelvisCandidate;
        public Quaternion SpineCandidate => spineCandidate;
        public Quaternion ChestCandidate => chestCandidate;
        public bool UpperChestAvailable => upperChestAvailable;
        public Quaternion NeckCandidate => neckCandidate;
        public Quaternion HeadCandidate => headCandidate;
        public UpperBodyShadowSolution CurrentSolution { get; private set; }
        public UpperBodyOrientationDistributionProfile Profile => profile;
        public string Status => status;

        private void Awake()
        {
            profile = UpperBodyOrientationDistributionProfile.CreateU04Conservative();
            profileId = profile.ProfileId;
            profileVersion = profile.Version;
        }

        private void LateUpdate()
        {
            long virtualBodyStarted =
                SalieriRuntimePerformanceProbe.Timestamp();
            Capture(DateTime.UtcNow, Time.frameCount);
            if (logSolution && Time.frameCount % Mathf.Max(1, logIntervalFrames) == 0)
                LogCurrentSolution();
            SalieriRuntimePerformanceProbe.RecordDuration(
                SalieriRuntimePerformanceMetric.VirtualBody,
                virtualBodyStarted);
        }

        public void SetProfile(UpperBodyOrientationDistributionProfile replacement)
        {
            profile = replacement ?? throw new ArgumentNullException(nameof(replacement));
            profileId = profile.ProfileId;
            profileVersion = profile.Version;
        }

        public void Capture(DateTime capturedUtc, int frame)
        {
            if (capturedUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("Capture time must be UTC.", nameof(capturedUtc));
            sourceFrame = frame;
            capturedAtUtc = capturedUtc.ToString("O");
            targetKind = targetDriver != null
                ? targetDriver.OutputKind
                : OrientationResolutionKind.None;
            targetKey = targetDriver != null
                ? targetDriver.OutputTargetKey ?? string.Empty
                : string.Empty;

            if (profile == null)
                profile = UpperBodyOrientationDistributionProfile.CreateU04Conservative();
            if (canonicalBodyFrames == null || !canonicalBodyFrames.Valid ||
                canonicalBodyFrames.CurrentBodyOrientation == null ||
                targetDriver == null || !targetDriver.HasOutputPosition)
            {
                SetInvalid(capturedUtc, frame, "Canonical body or resolved target unavailable.");
                return;
            }

            Vector3 worldDirection = targetDriver.OutputWorldPosition -
                                     canonicalBodyFrames.BodyOrientationFrame.position;
            if (!SalieriAI.Body.Frames.BodyRelativeTargetDirection.TryCreate(
                    canonicalBodyFrames.CurrentBodyOrientation,
                    worldDirection,
                    out SalieriAI.Body.Frames.BodyRelativeTargetDirection canonicalTarget,
                    out string targetError))
            {
                SetInvalid(capturedUtc, frame, targetError);
                return;
            }

            CurrentSolution = solver.Solve(
                canonicalBodyFrames.CurrentBodyOrientation,
                canonicalTarget,
                targetKey,
                frame,
                capturedUtc,
                profile,
                BuildAvailability());
            ApplySolution(canonicalTarget);
        }

        /// <summary>
        /// Explicit canonical diagnostic entry used by Play validation and
        /// future Shadow callers. It updates only this Host's result state.
        /// </summary>
        public void EvaluateBodyRelativeTargetDirection(
            SalieriAI.Body.Frames.BodyRelativeTargetDirection canonicalTarget,
            string explicitTargetKey,
            OrientationResolutionKind explicitKind,
            DateTime capturedUtc,
            int frame)
        {
            if (capturedUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("Capture time must be UTC.", nameof(capturedUtc));
            if (profile == null)
                profile = UpperBodyOrientationDistributionProfile.CreateU04Conservative();
            sourceFrame = frame;
            capturedAtUtc = capturedUtc.ToString("O");
            targetKey = explicitTargetKey ?? string.Empty;
            targetKind = explicitKind;
            if (canonicalBodyFrames == null || !canonicalBodyFrames.Valid ||
                canonicalBodyFrames.CurrentBodyOrientation == null ||
                canonicalTarget == null)
            {
                SetInvalid(capturedUtc, frame, "Canonical diagnostic input unavailable.");
                return;
            }
            CurrentSolution = solver.Solve(
                canonicalBodyFrames.CurrentBodyOrientation,
                canonicalTarget,
                targetKey,
                frame,
                capturedUtc,
                profile,
                BuildAvailability());
            ApplySolution(canonicalTarget);
        }

        private Dictionary<BodySegment, bool> BuildAvailability()
        {
            return new Dictionary<BodySegment, bool>
            {
                { BodySegment.Pelvis, Available(observationProvider?.Pelvis) },
                { BodySegment.Spine, Available(observationProvider?.Spine) },
                { BodySegment.Chest, Available(observationProvider?.Chest) },
                { BodySegment.UpperChest, Available(observationProvider?.UpperChest) },
                { BodySegment.Neck, Available(observationProvider?.Neck) },
                { BodySegment.Head, Available(observationProvider?.Head) }
            };
        }

        private static bool Available(BodySegmentObservationSnapshot snapshot) =>
            snapshot != null && snapshot.BoneAvailable &&
            snapshot.CanonicalState != null && snapshot.CanonicalState.Valid;

        private void ApplySolution(
            SalieriAI.Body.Frames.BodyRelativeTargetDirection canonicalTarget)
        {
            valid = CurrentSolution != null && CurrentSolution.Valid;
            bodyRelativeTargetDirection = canonicalTarget.BodyRelativeDirection;
            desiredBodyRelativeRotation = valid
                ? CurrentSolution.DesiredBodyRelativeRotation
                : Quaternion.identity;
            reconstructedHeadForward = valid
                ? CurrentSolution.ReconstructedHeadForward
                : Vector3.forward;
            residualTargetErrorDegrees = valid
                ? CurrentSolution.ResidualTargetErrorDegrees
                : 180f;
            status = valid ? "Valid" : CurrentSolution?.Error ?? "Invalid";
            profileId = profile.ProfileId;
            profileVersion = profile.Version;
            pelvisCandidate = Candidate(BodySegment.Pelvis);
            spineCandidate = Candidate(BodySegment.Spine);
            chestCandidate = Candidate(BodySegment.Chest);
            UpperBodyShadowSegmentSolution upper = CurrentSolution?.Find(BodySegment.UpperChest);
            upperChestAvailable = upper != null && upper.Available;
            neckCandidate = Candidate(BodySegment.Neck);
            headCandidate = Candidate(BodySegment.Head);
        }

        private Quaternion Candidate(BodySegment segment)
        {
            UpperBodyShadowSegmentSolution value = CurrentSolution?.Find(segment);
            return value != null
                ? value.CandidateNeutralRelativeRotation
                : Quaternion.identity;
        }

        private void SetInvalid(DateTime capturedUtc, int frame, string reason)
        {
            CurrentSolution = UpperBodyShadowSolution.CreateInvalid(
                targetKey,
                frame,
                capturedUtc,
                profile != null ? profile.ProfileId : string.Empty,
                profile != null ? profile.Version : string.Empty,
                reason);
            valid = false;
            bodyRelativeTargetDirection = Vector3.forward;
            desiredBodyRelativeRotation = Quaternion.identity;
            reconstructedHeadForward = Vector3.forward;
            residualTargetErrorDegrees = 180f;
            status = reason;
            pelvisCandidate = spineCandidate = chestCandidate =
                neckCandidate = headCandidate = Quaternion.identity;
            upperChestAvailable = false;
        }

        private void LogCurrentSolution()
        {
            Debug.Log(
                "[UpperBodyShadowRuntime] Valid=" + valid +
                " Target=" + (targetKey.Length > 0 ? targetKey : "none") +
                " Kind=" + targetKind +
                " Direction=" + bodyRelativeTargetDirection +
                " ResidualDeg=" + residualTargetErrorDegrees.ToString("F3") +
                " UpperChestAvailable=" + upperChestAvailable +
                " Profile=" + profileId + "/" + profileVersion,
                this);
        }
    }
}
