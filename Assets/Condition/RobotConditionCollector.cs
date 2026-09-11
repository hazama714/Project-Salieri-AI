// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;
using SalieriAI.Core.Limbo;
using SalieriAI.Core.State;

public class RobotConditionCollector : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private InteractionStateController interactionStateController;
    [SerializeField] private LimboPermission limboPermission;
    [SerializeField] private SalieriAI.Core.Perception.Buffer.FacePerceptionBuffer facePerceptionBuffer;

    [Header("Runtime State")]
    [SerializeField] private bool faceDetected;

    [Header("Device / Boot")]
    [SerializeField] private bool cameraAvailable = true;
    [SerializeField] private bool microphoneAvailable = true;
    [SerializeField] private bool bluetoothReady;
    [SerializeField] private bool llamaReady;
    [SerializeField] private bool voicevoxReady;
    [SerializeField] private bool lightSensorAvailable;
    [SerializeField] private bool gyroAvailable;

    private float lastFaceTime;
    private float lastThinkTime;
    private float lastActionTime;
    private float lastSpeechTime;

    private string lastAction = "none";

    private void Awake()
    {
        float now = Time.time;

        lastFaceTime = now;
        lastThinkTime = now;
        lastActionTime = now;
        lastSpeechTime = now;
    }

    public void SetFaceDetected(bool detected)
    {
        faceDetected = detected;

        if (detected)
            lastFaceTime = Time.time;
    }

    public void NotifyThink()
    {
        lastThinkTime = Time.time;
    }

    public void NotifyAction(string actionId)
    {
        lastAction =
            string.IsNullOrWhiteSpace(actionId)
                ? "unknown"
                : actionId;

        lastActionTime = Time.time;
    }

    public void NotifySpeech()
    {
        lastSpeechTime = Time.time;
    }

    public SelfStateSummary BuildSummary()
    {
        return Collect();
    }

    public SelfStateSummary Collect()
    {
        SelfStateSummary summary = new SelfStateSummary();

        float now = Time.time;

        if (facePerceptionBuffer != null)
        {
            summary.faceVisibleDuration = facePerceptionBuffer.FaceVisibleDuration;
            summary.noFaceDuration = facePerceptionBuffer.NoFaceDuration;
        }

        summary.cameraAvailable = cameraAvailable;
        summary.microphoneAvailable = microphoneAvailable;
        summary.bluetoothReady = bluetoothReady;
        summary.llamaReady = llamaReady;
        summary.voicevoxReady = voicevoxReady;
        summary.lightSensorAvailable = lightSensorAvailable;
        summary.gyroAvailable = gyroAvailable;

        summary.batteryLevel = SystemInfo.batteryLevel;
        summary.batteryStatus = SystemInfo.batteryStatus;
        summary.maxTemperatureCelsius = -1f;

        if (interactionStateController != null)
        {
            summary.interactionState = interactionStateController.CurrentState.ToString();
            summary.mode = interactionStateController.CurrentState.ToString();
        }
        else
        {
            summary.interactionState = "Unknown";
            summary.mode = "Unknown";
        }

        summary.faceDetected = faceDetected;
        summary.secondsSinceLastFace = now - lastFaceTime;

        summary.secondsSinceLastThink = now - lastThinkTime;
        summary.secondsSinceLastAction = now - lastActionTime;
        summary.secondsSinceLastSpeech = now - lastSpeechTime;
        summary.lastAction = lastAction;

        summary.isSpeaking = summary.interactionState == "Speaking";
        summary.isActing = summary.interactionState == "Acting";

        if (limboPermission != null)
        {
            summary.canThink = limboPermission.CanThink;
            summary.canSpeak = limboPermission.CanSpeak;
            summary.canMoveServo = limboPermission.CanMoveServo;
            summary.canStartAction = limboPermission.CanStartAction;
            summary.canTrackFace = limboPermission.CanTrackFace;
            summary.canInterrupt = limboPermission.CanInterrupt;
        }

        summary.note = $"LastAction={summary.lastAction}";

        return summary;
    }
}