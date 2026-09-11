// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Body.Frames;
using SalieriAI.Body.Frames.Canonical;
using SalieriAI.Body.Frames.Observation;
using SalieriAI.Body.UpperBody.Actuation.Timing;
using SalieriAI.Body.UpperBody.Shadow;
using SalieriAI.Core.Perception.Attention;
using SalieriAI.Expression.Motion;

using UnityEngine;
using SalieriAI.Core.Diagnostics.Performance;

namespace SalieriAI.Body.UpperBody.Actuation.Production
{
    /// <summary>
    /// Single logical owner of MainScene LookAt application and optional U1
    /// Chest production writing. Candidate, blend, Chest apply, and LookAt
    /// configuration execute in this order in one OnAnimatorIK callback.
    /// </summary>
    [DefaultExecutionOrder(525)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class UpperBodyAnimatorIKCoordinator : MonoBehaviour,
        IUpperBodyVirtualPoseWriter
    {
        [Header("Animator IK Ownership")]
        [SerializeField] private Animator animator;
        [SerializeField] private VrmHeadLookAtVisual lookAtVisual;

        [Header("Canonical Production Input")]
        [SerializeField] private CanonicalBodyFrameHost canonicalBodyFrames;
        [SerializeField]
        private OrientationResolutionTargetDriver targetDriver;

        [Header("Solved Pose Observation")]
        [SerializeField]
        private BodyFrameObservationProvider observationProvider;
        [SerializeField] private VRMNeckSolvedPoseReader neckSolvedPoseReader;

        [Header("Production Gate (Default Disabled)")]
        [SerializeField] private bool productionChestWriteEnabled;

        [Header("Production Profile (Read Only)")]
        [SerializeField] private string profileId = string.Empty;
        [SerializeField] private string profileVersion = string.Empty;
        [SerializeField] private float maxYawDegrees;
        [SerializeField] private float maxPitchDegrees;
        [SerializeField] private float blendDegreesPerSecond;

        [Header("Current Candidate / Applied Pose (Read Only)")]
        [SerializeField] private bool candidateValid;
        [SerializeField] private string targetKey = string.Empty;
        [SerializeField] private string status = "NotInitialized";
        [SerializeField] private Quaternion chestCandidate = Quaternion.identity;
        [SerializeField] private Quaternion blendedChestCandidate =
            Quaternion.identity;
        [SerializeField] private Quaternion animatorBaseChestLocalRotation =
            Quaternion.identity;
        [SerializeField] private Quaternion appliedChestLocalRotation =
            Quaternion.identity;

        [Header("Frame Timing (Read Only)")]
        [SerializeField] private int candidateFrame = -1;
        [SerializeField] private int coordinatorOnAnimatorIkFrame = -1;
        [SerializeField] private int chestAppliedFrame = -1;
        [SerializeField] private int lookAtAppliedFrame = -1;
        [SerializeField] private int solvedPoseObservedFrame = -1;
        [SerializeField] private bool sameFrameCorrelation;

        [Header("Actual vs Candidate (Read Only)")]
        [SerializeField] private Quaternion actualChestNeutralRelativeRotation =
            Quaternion.identity;
        [SerializeField] private Vector3 candidateChestForward = Vector3.forward;
        [SerializeField] private Vector3 actualChestForward = Vector3.forward;
        [SerializeField] private float actualCandidateAngularDifference = -1f;
        [SerializeField] private float headTargetResidualDegrees = -1f;

        private readonly UpperBodyChestProductionCandidateEvaluator evaluator =
            new UpperBodyChestProductionCandidateEvaluator();
        private UpperBodyChestProductionActuationProfile profile;
        private Transform chestBone;
        private bool animatorIkPassActive;
        private Vector3 lastBodyRelativeTargetDirection = Vector3.forward;

        public bool ProductionChestWriteEnabled =>
            productionChestWriteEnabled;
        public bool CandidateValid => candidateValid;
        public string TargetKey => targetKey;
        public string Status => status;
        public string ProfileId => profileId;
        public string ProfileVersion => profileVersion;
        public float MaxYawDegrees => maxYawDegrees;
        public float MaxPitchDegrees => maxPitchDegrees;
        public float BlendDegreesPerSecond => blendDegreesPerSecond;
        public Quaternion ChestCandidate => chestCandidate;
        public Quaternion BlendedChestCandidate => blendedChestCandidate;
        public Quaternion AppliedChestLocalRotation => appliedChestLocalRotation;
        public int CandidateFrame => candidateFrame;
        public int CoordinatorOnAnimatorIkFrame =>
            coordinatorOnAnimatorIkFrame;
        public int ChestAppliedFrame => chestAppliedFrame;
        public int LookAtAppliedFrame => lookAtAppliedFrame;
        public int SolvedPoseObservedFrame => solvedPoseObservedFrame;
        public bool SameFrameCorrelation => sameFrameCorrelation;
        public float ActualCandidateAngularDifference =>
            actualCandidateAngularDifference;
        public float HeadTargetResidualDegrees => headTargetResidualDegrees;
        public UpperBodyShadowSolution CurrentSolution { get; private set; }
        public UpperBodyAnimatorFrameTimingTrace CurrentTimingTrace
        {
            get;
            private set;
        }
        public UpperBodyVirtualPoseWriteResult LastWriteResult
        {
            get;
            private set;
        } = UpperBodyVirtualPoseWriteResult.CreateDisabled();

        private void Awake()
        {
            InitializeRuntime();
        }

        private void OnEnable()
        {
            InitializeRuntime();
            AcquireLookAtOwnership();
        }

        private void OnDisable()
        {
            if (lookAtVisual != null)
                lookAtVisual.ReleaseAnimatorIkCoordinatorOwnership(this);
            animatorIkPassActive = false;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            long animatorIkStarted =
                SalieriRuntimePerformanceProbe.Timestamp();
            coordinatorOnAnimatorIkFrame = Time.frameCount;
            animatorIkPassActive = true;
            try
            {
                EvaluateCandidate(DateTime.UtcNow, Time.frameCount);
                string requestError = string.Empty;
                UpperBodyActuationProfileSelection selection = null;
                UpperBodyVirtualPoseWriteRequest request = null;
                if (candidateValid && productionChestWriteEnabled &&
                    UpperBodyActuationProfileSelection.TryCreate(
                        profileId,
                        profileVersion,
                        out selection,
                        out _) &&
                    UpperBodyVirtualPoseWriteRequest.TryCreate(
                        CurrentSolution,
                        selection,
                        out request,
                        out requestError))
                {
                    LastWriteResult = Apply(request);
                }
                else
                {
                    if (!candidateValid)
                    {
                        blendedChestCandidate = Quaternion.identity;
                        LastWriteResult =
                            UpperBodyVirtualPoseWriteResult.CreateRejected(
                                "Invalid candidate; Animator base pose retained.");
                    }
                    else if (!productionChestWriteEnabled)
                    {
                        blendedChestCandidate = Quaternion.identity;
                        LastWriteResult =
                            UpperBodyVirtualPoseWriteResult.CreateDisabled();
                    }
                    else
                    {
                        LastWriteResult =
                            UpperBodyVirtualPoseWriteResult.CreateRejected(
                                requestError ?? "Write request rejected.");
                    }
                    chestAppliedFrame = -1;
                }

                bool lookAtApplied = lookAtVisual != null &&
                    lookAtVisual.ApplyLookAtFromCoordinator(
                        this,
                        animator,
                        Time.frameCount);
                lookAtAppliedFrame = lookAtApplied
                    ? Time.frameCount
                    : -1;
                if (!lookAtApplied && status == "CandidateReady")
                    status = "CandidateReady; LookAtUnavailable";
            }
            finally
            {
                animatorIkPassActive = false;
                SalieriRuntimePerformanceProbe.RecordDuration(
                    SalieriRuntimePerformanceMetric.AnimatorIk,
                    animatorIkStarted);
            }
        }

        private void LateUpdate()
        {
            solvedPoseObservedFrame = Time.frameCount;
            CaptureActualComparison();
            CurrentTimingTrace = null;
            sameFrameCorrelation = false;
            if (candidateFrame >= 0 && chestAppliedFrame >= 0 &&
                lookAtAppliedFrame >= 0 &&
                UpperBodyAnimatorFrameTimingTrace.TryCreate(
                    targetKey,
                    candidateFrame,
                    coordinatorOnAnimatorIkFrame,
                    chestAppliedFrame,
                    lookAtAppliedFrame,
                    solvedPoseObservedFrame,
                    DateTime.UtcNow,
                    out UpperBodyAnimatorFrameTimingTrace trace,
                    out _))
            {
                CurrentTimingTrace = trace;
                sameFrameCorrelation =
                    trace.HasCanonicalSameFrameCorrelation;
            }
        }

        public void SetProductionChestWriteEnabled(bool enabled)
        {
            productionChestWriteEnabled = enabled;
            if (!enabled)
            {
                blendedChestCandidate = Quaternion.identity;
                chestAppliedFrame = -1;
                LastWriteResult =
                    UpperBodyVirtualPoseWriteResult.CreateDisabled();
            }
        }

        public UpperBodyVirtualPoseWriteResult Apply(
            UpperBodyVirtualPoseWriteRequest request)
        {
            if (!animatorIkPassActive)
                return UpperBodyVirtualPoseWriteResult.CreateRejected(
                    "Chest writes are only valid inside coordinator OnAnimatorIK.");
            if (!productionChestWriteEnabled)
                return UpperBodyVirtualPoseWriteResult.CreateDisabled();
            if (request == null || request.Solution == null ||
                request.ProfileSelection == null || profile == null)
                return UpperBodyVirtualPoseWriteResult.CreateRejected(
                    "Complete write request and production profile are required.");
            if (!string.Equals(
                    request.ProfileSelection.ProfileId,
                    profileId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    request.ProfileSelection.ProfileVersion,
                    profileVersion,
                    StringComparison.Ordinal))
                return UpperBodyVirtualPoseWriteResult.CreateRejected(
                    "Production profile identity mismatch.");

            UpperBodyShadowSegmentSolution chest =
                request.Solution.Find(BodySegment.Chest);
            if (chest == null || !chest.Available || !chest.Enabled ||
                request.Solution.Segments.Count != 1)
                return UpperBodyVirtualPoseWriteResult.CreateRejected(
                    "U1 permits one available Chest segment only.");
            if (animator == null || !animator.isHuman || chestBone == null)
                return UpperBodyVirtualPoseWriteResult.CreateRejected(
                    "Humanoid Chest bone unavailable.");

            chestCandidate = chest.CandidateNeutralRelativeRotation;
            float maxDelta = Mathf.Max(
                0f,
                profile.BlendDegreesPerSecond * Time.deltaTime);
            blendedChestCandidate = Quaternion.RotateTowards(
                blendedChestCandidate,
                chestCandidate,
                maxDelta);
            animatorBaseChestLocalRotation = chestBone.localRotation;
            appliedChestLocalRotation =
                UpperBodyAnimatorApplicationOrderContract
                    .ComposeChestLocalRotation(
                        animatorBaseChestLocalRotation,
                        blendedChestCandidate);
            animator.SetBoneLocalRotation(
                HumanBodyBones.Chest,
                appliedChestLocalRotation);
            chestAppliedFrame = Time.frameCount;
            status = "ChestApplied";
            return UpperBodyVirtualPoseWriteResult.CreateApplied(
                "Chest candidate applied during coordinator OnAnimatorIK.");
        }

        private void InitializeRuntime()
        {
            if (animator == null)
                animator = GetComponent<Animator>();
            if (lookAtVisual == null)
                lookAtVisual = GetComponent<VrmHeadLookAtVisual>();
            profile = UpperBodyChestProductionActuationProfile.CreateU1Initial();
            profileId = profile.SolverProfile.ProfileId;
            profileVersion = profile.SolverProfile.Version;
            maxYawDegrees =
                UpperBodyChestProductionActuationProfile.U1MaxYawDegrees;
            maxPitchDegrees =
                UpperBodyChestProductionActuationProfile.U1MaxPitchDegrees;
            blendDegreesPerSecond = profile.BlendDegreesPerSecond;
            chestBone = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.Chest)
                : null;
            status = animator != null && animator.isHuman && chestBone != null
                ? "Ready"
                : "HumanoidChestUnavailable";
        }

        private void AcquireLookAtOwnership()
        {
            if (lookAtVisual == null ||
                !lookAtVisual.TryAcquireAnimatorIkCoordinatorOwnership(this))
                status = "LookAtOwnershipUnavailable";
        }

        private void EvaluateCandidate(DateTime evaluatedAtUtc, int frame)
        {
            candidateFrame = frame;
            targetKey = targetDriver != null
                ? targetDriver.OutputTargetKey ?? string.Empty
                : string.Empty;
            if (canonicalBodyFrames == null ||
                !canonicalBodyFrames.Valid ||
                canonicalBodyFrames.CurrentBodyOrientation == null ||
                canonicalBodyFrames.BodyOrientationFrame == null ||
                targetDriver == null || !targetDriver.HasOutputPosition)
            {
                CurrentSolution = UpperBodyShadowSolution.CreateInvalid(
                    targetKey,
                    frame,
                    evaluatedAtUtc,
                    profileId,
                    profileVersion,
                    "Canonical body or resolved target unavailable.");
                candidateValid = false;
                chestCandidate = Quaternion.identity;
                status = CurrentSolution.Error;
                return;
            }

            CurrentSolution = evaluator.Evaluate(
                canonicalBodyFrames.CurrentBodyOrientation,
                canonicalBodyFrames.BodyOrientationFrame.position,
                targetDriver.OutputWorldPosition,
                targetKey,
                frame,
                evaluatedAtUtc,
                profile);
            candidateValid = CurrentSolution != null && CurrentSolution.Valid;
            UpperBodyShadowSegmentSolution chest =
                CurrentSolution?.Find(BodySegment.Chest);
            chestCandidate = candidateValid && chest != null
                ? chest.CandidateNeutralRelativeRotation
                : Quaternion.identity;
            if (candidateValid)
            {
                Vector3 worldDirection = targetDriver.OutputWorldPosition -
                    canonicalBodyFrames.BodyOrientationFrame.position;
                lastBodyRelativeTargetDirection =
                    Quaternion.Inverse(
                        canonicalBodyFrames.CurrentBodyOrientation.WorldRotation) *
                    worldDirection.normalized;
                status = "CandidateReady";
            }
            else
            {
                blendedChestCandidate = Quaternion.identity;
                lastBodyRelativeTargetDirection = Vector3.forward;
                status = CurrentSolution?.Error ?? "CandidateInvalid";
            }
        }

        private void CaptureActualComparison()
        {
            BodySegmentObservationSnapshot actual =
                observationProvider != null
                    ? observationProvider.Chest
                    : null;
            if (candidateValid && actual != null &&
                actual.CanonicalState != null && actual.CanonicalState.Valid)
            {
                actualChestNeutralRelativeRotation =
                    actual.CanonicalState.NeutralRelativeRotation;
                actualChestForward = actual.CanonicalState.Forward;
                candidateChestForward =
                    blendedChestCandidate * Vector3.forward;
                actualCandidateAngularDifference = Quaternion.Angle(
                    blendedChestCandidate,
                    actualChestNeutralRelativeRotation);
            }
            else
            {
                actualChestNeutralRelativeRotation = Quaternion.identity;
                actualChestForward = candidateChestForward = Vector3.forward;
                actualCandidateAngularDifference = -1f;
            }

            VirtualNeckPoseState neckPose = neckSolvedPoseReader != null
                ? neckSolvedPoseReader.CurrentPose
                : null;
            if (neckPose != null && neckPose.Valid && candidateValid)
            {
                Vector3 headOriginRelativeTarget =
                    lastBodyRelativeTargetDirection;
                if (neckPose.EyeMidpointAvailable && targetDriver != null &&
                    targetDriver.HasOutputPosition &&
                    canonicalBodyFrames != null &&
                    canonicalBodyFrames.CurrentBodyOrientation != null)
                {
                    Vector3 eyeToTarget =
                        targetDriver.OutputWorldPosition -
                        neckPose.EyeMidpointWorld;
                    if (eyeToTarget.sqrMagnitude > 0.000001f)
                    {
                        headOriginRelativeTarget =
                            Quaternion.Inverse(
                                canonicalBodyFrames.CurrentBodyOrientation
                                    .WorldRotation) *
                            eyeToTarget.normalized;
                    }
                }
                // VRMNeckSolvedPoseReader intentionally reports the solved
                // Neck+Head delta relative to its parent reference. U1 adds a
                // Chest delta before LookAt, so compose the observed Chest
                // delta to evaluate the final solved head direction in the
                // canonical BodyOrientation frame.
                Vector3 solvedHeadForwardInBodyFrame =
                    actual != null && actual.CanonicalState != null &&
                    actual.CanonicalState.Valid
                        ? actual.CanonicalState.NeutralRelativeRotation *
                          neckPose.HeadForwardInBodyFrame
                        : neckPose.HeadForwardInBodyFrame;
                headTargetResidualDegrees = Vector3.Angle(
                    solvedHeadForwardInBodyFrame,
                    headOriginRelativeTarget);
            }
            else
            {
                headTargetResidualDegrees = -1f;
            }
        }
    }
}
