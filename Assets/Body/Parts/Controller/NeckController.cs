// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using UnityEngine;
using SalieriAI.Body.Command;
using SalieriAI.Body.Retargeting.Neck;
using SalieriAI.Runtime;

public class NeckController : MonoBehaviour
{
    [Header("Sender")]
    public MonoBehaviour senderBehaviour;

    [Header("Runtime")]
    [SerializeField] private RuntimeConnectionSettings runtimeSettings;

    private ICommandSender sender;
    private IConnectionStateProvider connectionStateProvider;
    private Action<bool> connectionStateChangedHandler;

    [Header("Servos")]
    public ServoControlUnit yawServo = new ServoControlUnit
    {
        servoIndex = 0,
        servoName = "Neck Yaw",
        minAngle = 0,
        maxAngle = 180
    };

    public ServoControlUnit pitchServo = new ServoControlUnit
    {
        servoIndex = 1,
        servoName = "Neck Pitch",
        minAngle = 45,
        maxAngle = 135
    };

    [Header("Center")]
    [SerializeField] private float yawCenterAngle = 90f;
    [SerializeField] private float pitchCenterAngle = 90f;

    [Header("Relative Limit")]
    [SerializeField] private float yawLimit = 45f;
    [SerializeField] private float pitchLimit = 30f;

    [Header("Smooth Follow")]
    // Retained for serialized compatibility with older scenes. M6.5 replaces
    // this non-dimensional Lerp behavior with the strict rates below.
    [SerializeField] private float followSpeed = 4f;
    [SerializeField] private float nearTargetSpeed = 2f;
    [SerializeField] private float slowDownAngle = 6f;
    [SerializeField] private float stopDeadZone = 0.8f;

    [Header("Physical Motion Rate Control (Initial, Uncalibrated)")]
    [SerializeField] private float maxYawSpeedDegreesPerSecond =
        PhysicalNeckMotionRateSettings.InitialYawSpeedDegreesPerSecond;
    [SerializeField] private float maxPitchSpeedDegreesPerSecond =
        PhysicalNeckMotionRateSettings.InitialPitchSpeedDegreesPerSecond;
    [SerializeField] private float returnToNeutralSpeedDegreesPerSecond =
        PhysicalNeckMotionRateSettings.InitialReturnSpeedDegreesPerSecond;
    [SerializeField] private float targetLostHoldSeconds =
        PhysicalNeckMotionRateSettings.InitialTargetLostHoldSeconds;

    [Header("Send Control")]
    [SerializeField] private float sendInterval = 0.08f;
    [SerializeField] private float minSendDelta = 1f;

    [Header("Connection Safety")]
    [Tooltip(
        "When the body sender becomes connected, send the configured neck " +
        "center angles first. The current software target is preserved and " +
        "smooth following resumes from the center baseline."
    )]
    [SerializeField] private bool forceCenterOnConnection = true;

    private float targetYaw;
    private float targetPitch;

    private float currentYaw;
    private float currentPitch;

    private int lastSentYawServoAngle = int.MinValue;
    private int lastSentPitchServoAngle = int.MinValue;

    private float sendTimer;

    private bool initialized;
    private PhysicalNeckMotionRateController motionRateController;
    private PhysicalNeckMotionPhase lastLoggedMotionPhase =
        (PhysicalNeckMotionPhase)(-1);

    // Last angles actually accepted by the neck command path, expressed as
    // relative angles from the configured center positions.
    // These are command-based estimates, not measured servo feedback.
    public float LastSentYawRelativeAngle =>
        lastSentYawServoAngle == int.MinValue
            ? currentYaw
            : lastSentYawServoAngle - yawCenterAngle;

    public float LastSentPitchRelativeAngle =>
        lastSentPitchServoAngle == int.MinValue
            ? currentPitch
            : lastSentPitchServoAngle - pitchCenterAngle;

    // Phase 5 read-only diagnostics. "Submitted" means accepted by this
    // controller and passed to ServoControlUnit; it is not measured physical
    // feedback. BodyCommandCoordinator exposes the final dispatched command.
    public float TargetYawRelativeAngle => targetYaw;
    public float TargetPitchRelativeAngle => targetPitch;
    public float CurrentYawRelativeAngle => currentYaw;
    public float CurrentPitchRelativeAngle => currentPitch;
    public bool HasSubmittedYawServoInput =>
        lastSentYawServoAngle != int.MinValue;
    public bool HasSubmittedPitchServoInput =>
        lastSentPitchServoAngle != int.MinValue;
    public int LastSubmittedYawServoInputAngle =>
        HasSubmittedYawServoInput ? lastSentYawServoAngle : 0;
    public int LastSubmittedPitchServoInputAngle =>
        HasSubmittedPitchServoInput ? lastSentPitchServoAngle : 0;
    public bool IsBodyConnectionReadyForDiagnostics =>
        IsBodyConnectionReady();
    public bool IsServoRuntimeEnabled =>
        runtimeSettings == null || runtimeSettings.useServo;
    public float DesiredYawRelativeAngle => targetYaw;
    public float DesiredPitchRelativeAngle => targetPitch;
    public float CommandedYawRelativeAngle => currentYaw;
    public float CommandedPitchRelativeAngle => currentPitch;
    public float ConfiguredMaximumYawSpeedDegreesPerSecond =>
        maxYawSpeedDegreesPerSecond;
    public float ConfiguredMaximumPitchSpeedDegreesPerSecond =>
        maxPitchSpeedDegreesPerSecond;
    public float ConfiguredReturnToNeutralSpeedDegreesPerSecond =>
        returnToNeutralSpeedDegreesPerSecond;
    public float ConfiguredTargetLostHoldSeconds => targetLostHoldSeconds;
    public float LegacySlowDownAngleForSerializedCompatibility =>
        slowDownAngle;
    public float LastMotionDeltaTimeSeconds => motionRateController != null
        ? motionRateController.LastDeltaTimeSeconds
        : 0f;
    public float TargetLostHoldRemainingSeconds => motionRateController != null
        ? motionRateController.HoldRemainingSeconds
        : 0f;
    public PhysicalNeckMotionPhase MotionPhase => motionRateController != null
        ? motionRateController.Phase
        : PhysicalNeckMotionPhase.Tracking;
    public bool StartupPhysicalAngleKnown => false;

    // Read-only effective command ranges. These expose the intersection of
    // the controller's relative limit and the configured servo range without
    // changing command ownership or output behavior.
    public float EffectiveYawMinRelativeAngle => Mathf.Max(
        -yawLimit,
        yawServo.minAngle - yawCenterAngle);
    public float EffectiveYawMaxRelativeAngle => Mathf.Min(
        yawLimit,
        yawServo.maxAngle - yawCenterAngle);
    public float EffectivePitchMinRelativeAngle => Mathf.Max(
        -pitchLimit,
        pitchServo.minAngle - pitchCenterAngle);
    public float EffectivePitchMaxRelativeAngle => Mathf.Min(
        pitchLimit,
        pitchServo.maxAngle - pitchCenterAngle);

    private bool openingSmoothActive;
    private float baseFollowSpeed;
    private float baseNearTargetSpeed;

    private void Awake()
    {
        if (runtimeSettings == null)
        {
            runtimeSettings = FindObjectOfType<RuntimeConnectionSettings>();
        }

        sender = senderBehaviour as ICommandSender;

        if (sender == null)
        {
            Debug.LogError(
                "[NeckController] senderBehaviour does not implement ICommandSender."
            );

            enabled = false;
            return;
        }

        connectionStateProvider =
            senderBehaviour as IConnectionStateProvider;

        yawServo.Initialize(sender, runtimeSettings);
        pitchServo.Initialize(sender, runtimeSettings);

        baseFollowSpeed = followSpeed;
        baseNearTargetSpeed = nearTargetSpeed;

        targetYaw = 0f;
        targetPitch = 0f;
        currentYaw = 0f;
        currentPitch = 0f;

        motionRateController = CreateMotionRateController();
        motionRateController.SubmitDesired(
            0f,
            0f,
            0f,
            0f,
            PhysicalNeckMotionIntent.Tracking);

        InvalidateLastSentAngles();

        initialized = true;
    }

    private void OnEnable()
    {
        if (!initialized)
            return;

        SubscribeConnectionStateProvider();
    }

    private void OnDisable()
    {
        UnsubscribeConnectionStateProvider();
    }

    private void OnDestroy()
    {
        UnsubscribeConnectionStateProvider();
    }

    private void Update()
    {
        if (!initialized)
            return;

        EnsureMotionRateController();
        motionRateController.Step(Time.deltaTime);
        currentYaw = motionRateController.CommandedYawDegrees;
        currentPitch = motionRateController.CommandedPitchDegrees;
        LogMotionPhaseChangeIfNeeded();

        sendTimer += Time.deltaTime;

        if (sendTimer < sendInterval)
            return;

        sendTimer = 0f;

        SendCurrentAngles(force: false);

        if (openingSmoothActive &&
            Mathf.Abs(currentYaw - targetYaw) < stopDeadZone &&
            Mathf.Abs(currentPitch - targetPitch) < stopDeadZone)
        {
            openingSmoothActive = false;

            followSpeed = baseFollowSpeed;
            nearTargetSpeed = baseNearTargetSpeed;

            Debug.Log("[NeckController] Opening smooth completed. Speed restored.");
        }
    }

    public void LookAt(float yaw, float pitch)
    {
        SetDesiredPhysicalPose(
            yaw,
            pitch,
            0f,
            0f,
            PhysicalNeckMotionIntent.Tracking);
    }

    public void SetDesiredPhysicalPose(
        float yaw,
        float pitch,
        float neutralYaw,
        float neutralPitch,
        PhysicalNeckMotionIntent intent,
        float explicitNeutralRateScale = 1f)
    {
        targetYaw = Mathf.Clamp(yaw, -yawLimit, yawLimit);
        targetPitch = Mathf.Clamp(pitch, -pitchLimit, pitchLimit);
        float safeNeutralYaw = Mathf.Clamp(neutralYaw, -yawLimit, yawLimit);
        float safeNeutralPitch = Mathf.Clamp(
            neutralPitch,
            -pitchLimit,
            pitchLimit);

        EnsureMotionRateController();
        motionRateController.SubmitDesired(
            targetYaw,
            targetPitch,
            safeNeutralYaw,
            safeNeutralPitch,
            intent,
            explicitNeutralRateScale);
    }

    private void SendCurrentAngles(bool force)
    {
        if (connectionStateProvider != null &&
            !IsBodyConnectionReady())
        {
            return;
        }

        int yawServoAngle = Mathf.RoundToInt(yawCenterAngle + currentYaw);
        int pitchServoAngle = Mathf.RoundToInt(pitchCenterAngle + currentPitch);

        if (force ||
            Mathf.Abs(yawServoAngle - lastSentYawServoAngle) >= minSendDelta)
        {
            yawServo.SetAngle(yawServoAngle);
            lastSentYawServoAngle = yawServoAngle;
        }

        if (force ||
            Mathf.Abs(pitchServoAngle - lastSentPitchServoAngle) >= minSendDelta)
        {
            pitchServo.SetAngle(pitchServoAngle);
            lastSentPitchServoAngle = pitchServoAngle;
        }
    }

    public void SetYaw(float yaw)
    {
        LookAt(yaw, targetPitch);
    }

    public void SetPitch(float pitch)
    {
        LookAt(targetYaw, pitch);
    }

    public void SetYawServoRaw(int angle)
    {
        if (connectionStateProvider != null &&
            !IsBodyConnectionReady())
        {
            Debug.LogWarning(
                "[NeckController] SetYawServoRaw skipped: body is not connected."
            );

            return;
        }

        yawServo.SetAngle(angle);
        lastSentYawServoAngle = angle;
    }

    public void SetPitchServoRaw(int angle)
    {
        if (connectionStateProvider != null &&
            !IsBodyConnectionReady())
        {
            Debug.LogWarning(
                "[NeckController] SetPitchServoRaw skipped: body is not connected."
            );

            return;
        }

        pitchServo.SetAngle(angle);
        lastSentPitchServoAngle = angle;
    }

    public void ReturnCenter()
    {
        openingSmoothActive = false;

        followSpeed = baseFollowSpeed;
        nearTargetSpeed = baseNearTargetSpeed;

        SetDesiredPhysicalPose(
            0f,
            0f,
            0f,
            0f,
            PhysicalNeckMotionIntent.ExplicitNeutral);

        Debug.Log("[NeckController] ReturnCenter");
    }

    public void ReturnCenterForOpening(float speedMultiplier = 0.25f)
    {
        openingSmoothActive = true;

        followSpeed = baseFollowSpeed * Mathf.Clamp01(speedMultiplier);
        nearTargetSpeed = baseNearTargetSpeed * Mathf.Clamp01(speedMultiplier);

        SetDesiredPhysicalPose(
            0f,
            0f,
            0f,
            0f,
            PhysicalNeckMotionIntent.ExplicitNeutral,
            Mathf.Clamp01(speedMultiplier));

        Debug.Log(
            $"[NeckController] ReturnCenterForOpening speedMultiplier:{speedMultiplier}"
        );
    }

    private void SubscribeConnectionStateProvider()
    {
        UnsubscribeConnectionStateProvider();

        if (connectionStateProvider == null)
        {
            Debug.LogWarning(
                "[NeckController] Sender has no IConnectionStateProvider. " +
                "Using legacy immediate startup send."
            );

            SendCurrentAngles(force: true);
            return;
        }

        connectionStateChangedHandler =
            HandleBodyConnectionStateChanged;

        connectionStateProvider.ConnectionStateChanged +=
            connectionStateChangedHandler;

        if (IsBodyConnectionReady())
        {
            HandleBodyConnectionStateChanged(true);
        }
        else
        {
            InvalidateLastSentAngles();

            Debug.Log(
                "[NeckController] Waiting for body connection before " +
                "initial center handshake."
            );
        }
    }

    private void UnsubscribeConnectionStateProvider()
    {
        if (connectionStateProvider != null &&
            connectionStateChangedHandler != null)
        {
            try
            {
                connectionStateProvider.ConnectionStateChanged -=
                    connectionStateChangedHandler;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        connectionStateChangedHandler = null;
    }

    private void HandleBodyConnectionStateChanged(bool connected)
    {
        if (!initialized)
            return;

        bool verifiedConnected =
            connected && IsBodyConnectionReady();

        if (!verifiedConnected)
        {
            InvalidateLastSentAngles();

            Debug.Log(
                "[NeckController] Body connection lost. " +
                "Last-sent neck command state invalidated."
            );

            return;
        }

        openingSmoothActive = false;
        followSpeed = baseFollowSpeed;
        nearTargetSpeed = baseNearTargetSpeed;

        InvalidateLastSentAngles();

        if (forceCenterOnConnection)
        {
            // Preserve targetYaw/targetPitch. Only the software position baseline
            // is reset, so subsequent Update calls approach the current target
            // smoothly after the physical center handshake.
            currentYaw = 0f;
            currentPitch = 0f;
            EnsureMotionRateController();
            motionRateController.ResetCommandedBaseline(0f, 0f);

            Debug.Log(
                "[NeckController] Body connected. " +
                "Forcing center handshake Yaw:0 Pitch:0."
            );
        }
        else
        {
            Debug.Log(
                "[NeckController] Body connected. " +
                "Forcing current-angle resend."
            );
        }

        sendTimer = 0f;
        SendCurrentAngles(force: true);
    }

    private bool IsBodyConnectionReady()
    {
        if (connectionStateProvider == null)
            return true;

        try
        {
            return connectionStateProvider.IsConnected;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            return false;
        }
    }

    private void InvalidateLastSentAngles()
    {
        lastSentYawServoAngle = int.MinValue;
        lastSentPitchServoAngle = int.MinValue;
    }

    private PhysicalNeckMotionRateController CreateMotionRateController()
    {
        return new PhysicalNeckMotionRateController(
            new PhysicalNeckMotionRateSettings(
                Mathf.Max(0f, maxYawSpeedDegreesPerSecond),
                Mathf.Max(0f, maxPitchSpeedDegreesPerSecond),
                Mathf.Max(0f, returnToNeutralSpeedDegreesPerSecond),
                Mathf.Max(0f, targetLostHoldSeconds),
                isCalibrated: false),
            currentYaw,
            currentPitch);
    }

    private void EnsureMotionRateController()
    {
        if (motionRateController == null)
            motionRateController = CreateMotionRateController();
    }

    private void LogMotionPhaseChangeIfNeeded()
    {
        if (motionRateController == null ||
            motionRateController.Phase == lastLoggedMotionPhase)
        {
            return;
        }

        lastLoggedMotionPhase = motionRateController.Phase;
        Debug.Log(
            "[NeckController][MOTION_STATE] " +
            $"Phase={motionRateController.Phase} " +
            $"Desired=({targetYaw:F2},{targetPitch:F2}) " +
            $"Commanded=({currentYaw:F2},{currentPitch:F2}) " +
            $"HoldRemaining={motionRateController.HoldRemainingSeconds:F3} " +
            "StartupPhysicalAngle=UNKNOWN",
            this);
    }

    private void OnValidate()
    {
        maxYawSpeedDegreesPerSecond = Mathf.Max(
            0f,
            maxYawSpeedDegreesPerSecond);
        maxPitchSpeedDegreesPerSecond = Mathf.Max(
            0f,
            maxPitchSpeedDegreesPerSecond);
        returnToNeutralSpeedDegreesPerSecond = Mathf.Max(
            0f,
            returnToNeutralSpeedDegreesPerSecond);
        targetLostHoldSeconds = Mathf.Max(0f, targetLostHoldSeconds);
    }
}
