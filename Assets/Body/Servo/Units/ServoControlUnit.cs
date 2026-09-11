// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;
using SalieriAI.Runtime;

[System.Serializable]
public class ServoControlUnit
{
    [Header("Servo")]
    public int servoIndex = 0;
    public string servoName = "Servo";

    [Header("Limit")]
    public int minAngle = 0;
    public int maxAngle = 180;

    [Header("Correction")]
    public bool invert = false;
    public int offset = 0;

    private ICommandSender sender;
    private RuntimeConnectionSettings runtimeSettings;

    private int lastSentAngle = -999;

    public void Initialize(
        ICommandSender commandSender,
        RuntimeConnectionSettings settings)
    {
        sender = commandSender;
        runtimeSettings = settings;

        Debug.Log(
            $"[ServoControlUnit][INIT] " +
            $"{servoName} ID:{servoIndex} Instance:{GetHashCode()}"
        );
    }

    public void SetAngle(int inputAngle)
    {
        TrySetAngle(
            inputAngle,
            forceSend: false,
            out _,
            out _
        );
    }

    /// <summary>
    /// Applies the existing calibration path and optionally bypasses only the
    /// local duplicate guard. A successful result means the configured sender
    /// accepted SendServo; it does not mean physical arrival.
    /// </summary>
    public bool TrySetAngle(
        int inputAngle,
        bool forceSend,
        out int finalAngle,
        out string reason
    )
    {
        finalAngle = inputAngle;

        if (invert)
        {
            finalAngle = 180 - finalAngle;
        }

        finalAngle += offset;
        finalAngle = Mathf.Clamp(finalAngle, minAngle, maxAngle);

        Debug.Log(
            $"[ServoControlUnit][CALC] " +
            $"{servoName} ID:{servoIndex} Input:{inputAngle} Final:{finalAngle}"
        );

        if (runtimeSettings != null &&
            !runtimeSettings.useServo)
        {
            Debug.Log(
                $"[ServoControlUnit][SEND_SKIP_DISABLED] " +
                $"{servoName} ID:{servoIndex} Angle:{finalAngle}"
            );

            reason = "servo_output_disabled";
            return false;
        }

        if (sender == null)
        {
            Debug.LogWarning(
                $"[ServoControlUnit][ERROR] " +
                $"sender is null / {servoName} ID:{servoIndex}"
            );

            reason = "servo_sender_missing";
            return false;
        }

        if (!forceSend && lastSentAngle == finalAngle)
        {
            Debug.Log(
                $"[ServoControlUnit][SEND_SKIP_DUPLICATE] " +
                $"{servoName} ID:{servoIndex} Angle:{finalAngle}"
            );

            reason = "servo_duplicate_suppressed";
            return false;
        }

        lastSentAngle = finalAngle;

        Debug.Log(
            $"[ServoControlUnit][SEND_CALL] " +
            $"{servoName} ID:{servoIndex} Angle:{finalAngle} " +
            $"Forced={forceSend}"
        );

        sender.SendServo(servoIndex, finalAngle);
        reason = string.Empty;
        return true;
    }
}
