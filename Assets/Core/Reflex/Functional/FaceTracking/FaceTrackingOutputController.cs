// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;
using SalieriAI.Core.Limbo;
using SalieriAI.Body.Retargeting.Neck;
using SalieriAI.Core.Perception.Attention;

/// <summary>
/// Guarded neck-orientation output controller.
/// Receives already-retargeted yaw/pitch candidates from the single owner
/// and outputs them to the neck only when normal mode and Limbo permissions allow it.
///
/// The historical class name is retained for scene and source compatibility.
/// This class must not choose targets, choose actions, speak, move the crawler,
/// or call LLM/runtime decisions.
/// </summary>
public class FaceTrackingOutputController : MonoBehaviour
{
    private const string LogPrefix = "[OrientationTrackingOutput]";

    [Header("Mode")]
    public ControlModeManager modeManager;

    [Header("Body Output")]
    public NeckController neckController;

    [Header("Limbo")]
    [SerializeField] private LimboPermission limboPermission;

    private float yawAngle = 0f;
    private float pitchAngle = 0f;

    private float outputYawAngle = 0f;
    private float outputPitchAngle = 0f;
    private float neutralYawAngle;
    private float neutralPitchAngle;
    private PhysicalNeckMotionIntent motionIntent =
        PhysicalNeckMotionIntent.TargetLost;

    private float lastYawAngle = 999f;
    private float lastPitchAngle = 999f;
    private PhysicalNeckMotionIntent lastMotionIntent =
        (PhysicalNeckMotionIntent)(-1);

    private bool warnedModeManagerMissing = false;
    private bool warnedNeckControllerMissing = false;
    private bool wasBlockedByLimbo = false;

    // Prevents sending stale angles immediately after Limbo blocks orientation output.
    private bool needResetAfterResume = false;

    private bool hasDispatchedOutput;
    private float lastDispatchedYaw;
    private float lastDispatchedPitch;
    private int outputDispatchCount;
    private string lastGuardReason = "NotEvaluated";

    public float CandidateYaw => yawAngle;
    public float CandidatePitch => pitchAngle;
    public float SmoothedOutputYaw => outputYawAngle;
    public float SmoothedOutputPitch => outputPitchAngle;
    public bool HasDispatchedOutput => hasDispatchedOutput;
    public float LastDispatchedYaw => lastDispatchedYaw;
    public float LastDispatchedPitch => lastDispatchedPitch;
    public int OutputDispatchCount => outputDispatchCount;
    public string LastGuardReason => lastGuardReason;
    public PhysicalNeckMotionIntent MotionIntent => motionIntent;
    public bool IsOutputGuardOpen =>
        modeManager != null &&
        modeManager.IsNormalMode() &&
        neckController != null &&
        (limboPermission == null ||
            (!limboPermission.IsEmergencyMode &&
             limboPermission.CanTrackOrientation &&
             limboPermission.CanMoveServo));

    private void Update()
    {
        if (modeManager == null)
        {
            lastGuardReason = "ModeManagerMissing";
            if (!warnedModeManagerMissing)
            {
                Debug.LogWarning($"{LogPrefix} modeManager is not set.");
                warnedModeManagerMissing = true;
            }

            return;
        }

        if (!modeManager.IsNormalMode())
        {
            lastGuardReason = "ModeNotNormal";
            return;
        }

        if (neckController == null)
        {
            lastGuardReason = "NeckControllerMissing";
            if (!warnedNeckControllerMissing)
            {
                Debug.LogWarning($"{LogPrefix} neckController is not set.");
                warnedNeckControllerMissing = true;
            }

            return;
        }

        if (!CanOutputToBody())
            return;

        lastGuardReason = string.Empty;

        bool resumedFromLimboBlock = wasBlockedByLimbo;

        wasBlockedByLimbo = false;

        if (resumedFromLimboBlock && needResetAfterResume)
        {
            needResetAfterResume = false;

            outputYawAngle = yawAngle;
            outputPitchAngle = pitchAngle;

            lastYawAngle = outputYawAngle;
            lastPitchAngle = outputPitchAngle;

            Debug.Log(
                $"{LogPrefix} Resume reset Yaw:{outputYawAngle} Pitch:{outputPitchAngle}"
            );

            return;
        }

        UpdateOutputAngles();

        bool changed =
            Mathf.Abs(outputYawAngle - lastYawAngle) > 0.01f ||
            Mathf.Abs(outputPitchAngle - lastPitchAngle) > 0.01f ||
            motionIntent != lastMotionIntent;

        if (!changed)
            return;

        lastYawAngle = outputYawAngle;
        lastPitchAngle = outputPitchAngle;
        lastMotionIntent = motionIntent;

        Debug.Log(
            $"{LogPrefix} LookAt Yaw:{outputYawAngle} Pitch:{outputPitchAngle}"
        );

        neckController.SetDesiredPhysicalPose(
            outputYawAngle,
            outputPitchAngle,
            neutralYawAngle,
            neutralPitchAngle,
            motionIntent);
        hasDispatchedOutput = true;
        lastDispatchedYaw = outputYawAngle;
        lastDispatchedPitch = outputPitchAngle;
        outputDispatchCount++;
    }

    private bool CanOutputToBody()
    {
        if (limboPermission == null)
            return true;

        if (limboPermission.IsEmergencyMode)
        {
            lastGuardReason = "Emergency";
            needResetAfterResume = true;
            LogBlockedOnce($"{LogPrefix} Blocked by Limbo: Emergency");
            return false;
        }

        if (!limboPermission.CanTrackOrientation)
        {
            lastGuardReason = "CanTrackOrientationFalse";
            needResetAfterResume = true;
            LogBlockedOnce(
                $"{LogPrefix} Blocked by Limbo: " +
                "CanTrackOrientation=false"
            );
            return false;
        }

        if (!limboPermission.CanMoveServo)
        {
            lastGuardReason = "CanMoveServoFalse";
            needResetAfterResume = true;
            LogBlockedOnce($"{LogPrefix} Blocked by Limbo: CanMoveServo=false");
            return false;
        }

        return true;
    }

    private void LogBlockedOnce(string message)
    {
        if (wasBlockedByLimbo)
            return;

        Debug.Log(message);
        wasBlockedByLimbo = true;
    }

    private void UpdateOutputAngles()
    {
        outputYawAngle = yawAngle;
        outputPitchAngle = pitchAngle;
    }

    /// <summary>
    /// Single-owner-only Virtual Body retarget candidate acceptance. This does
    /// not send immediately; Update retains the existing Mode/Limbo/Emergency
    /// gate and remains the sole NeckController writer above the physical
    /// controller.
    /// </summary>
    internal void AcceptOwnedVirtualRetargetAngles(float yaw, float pitch)
    {
        yawAngle = yaw;
        pitchAngle = pitch;
        neutralYawAngle = 0f;
        neutralPitchAngle = 0f;
        motionIntent = PhysicalNeckMotionIntent.Tracking;
    }

    internal void AcceptOwnedVirtualRetargetState(
        VirtualNeckRetargetShadowState state)
    {
        if (state == null || !state.Valid)
            return;

        yawAngle = state.CandidatePhysicalYaw;
        pitchAngle = state.CandidatePhysicalPitch;
        neutralYawAngle = state.NeutralCandidateYaw;
        neutralPitchAngle = state.NeutralCandidatePitch;
        motionIntent = ToMotionIntent(state.SourceKind);
    }

    internal void AcceptOwnedVirtualRetargetUnavailable(
        VirtualNeckRetargetShadowState state)
    {
        if (state != null)
        {
            neutralYawAngle = state.NeutralCandidateYaw;
            neutralPitchAngle = state.NeutralCandidatePitch;
        }

        motionIntent = PhysicalNeckMotionIntent.TargetLost;
    }

    private static PhysicalNeckMotionIntent ToMotionIntent(
        OrientationResolutionKind sourceKind)
    {
        switch (sourceKind)
        {
            case OrientationResolutionKind.Face:
            case OrientationResolutionKind.Object:
            case OrientationResolutionKind.HoldPrevious:
            case OrientationResolutionKind.AttentionContinuityHold:
            case OrientationResolutionKind.TargetDirection:
                return PhysicalNeckMotionIntent.Tracking;

            case OrientationResolutionKind.Neutral:
                return PhysicalNeckMotionIntent.ExplicitNeutral;

            case OrientationResolutionKind.AttentionContinuityExpired:
                return PhysicalNeckMotionIntent
                    .AttentionContinuityExpired;

            case OrientationResolutionKind.None:
            default:
                return PhysicalNeckMotionIntent.TargetLost;
        }
    }
}
