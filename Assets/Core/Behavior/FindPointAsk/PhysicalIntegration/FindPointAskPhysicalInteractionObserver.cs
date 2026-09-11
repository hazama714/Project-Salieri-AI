// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Body.Command;
using SalieriAI.Body.Retargeting.Neck;
using SalieriAI.Body.Servo.Safety;

using UnityEngine;

namespace SalieriAI.Core.Behavior.FindPointAsk.PhysicalIntegration
{
    /// <summary>
    /// Read-only adapter from existing production diagnostics to the pure M6
    /// causal tracker. It never selects a target, arms output, sends a command,
    /// or claims physical position completion.
    /// </summary>
    public sealed class FindPointAskPhysicalInteractionObserver
    {
        private const int FirstNeckServoId = 0;
        private const int LastNeckServoId = 1;
        private const int FirstArmServoId = 2;
        private const int LastArmServoId = 9;

        private readonly PhysicalNeckOutputOwner neckOutputOwner;
        private readonly global::FaceTrackingOutputController neckOutputController;
        private readonly ArmServoOutputController armOutputController;
        private readonly BodyCommandCoordinator commandCoordinator;
        private readonly bool[] hadRunBaseline = new bool[10];
        private readonly float[] runBaselineDispatchTime = new float[10];
        private readonly bool[] hadPointBaseline = new bool[10];
        private readonly float[] pointBaselineDispatchTime = new float[10];
        private bool pointBaselineCaptured;

        public bool HasNeckObservationBoundary =>
            neckOutputOwner != null &&
            neckOutputController != null &&
            commandCoordinator != null;

        public bool HasArmObservationBoundary =>
            armOutputController != null &&
            commandCoordinator != null;

        public bool NeckOutputGateOpen =>
            neckOutputController != null &&
            neckOutputController.IsOutputGuardOpen;

        public bool ArmOutputArmed =>
            armOutputController != null &&
            armOutputController.State == ArmServoOutputState.Armed;

        public FindPointAskPhysicalInteractionObserver(
            PhysicalNeckOutputOwner neckOutputOwner,
            global::FaceTrackingOutputController neckOutputController,
            ArmServoOutputController armOutputController,
            BodyCommandCoordinator commandCoordinator)
        {
            this.neckOutputOwner = neckOutputOwner;
            this.neckOutputController = neckOutputController;
            this.armOutputController = armOutputController;
            this.commandCoordinator = commandCoordinator;
        }

        public static FindPointAskPhysicalInteractionObserver ResolveFromScene()
        {
            return new FindPointAskPhysicalInteractionObserver(
                UnityEngine.Object.FindObjectOfType<PhysicalNeckOutputOwner>(),
                UnityEngine.Object.FindObjectOfType<global::FaceTrackingOutputController>(),
                UnityEngine.Object.FindObjectOfType<ArmServoOutputController>(),
                UnityEngine.Object.FindObjectOfType<BodyCommandCoordinator>());
        }

        public void BeginRun()
        {
            pointBaselineCaptured = false;
            CaptureBaseline(
                FirstNeckServoId,
                LastArmServoId,
                hadRunBaseline,
                runBaselineDispatchTime);
        }

        public void BeginPoint()
        {
            pointBaselineCaptured = true;
            CaptureBaseline(
                FirstArmServoId,
                LastArmServoId,
                hadPointBaseline,
                pointBaselineDispatchTime);
        }

        public void Pump(FindPointAskPhysicalInteractionTracker tracker)
        {
            if (tracker == null || commandCoordinator == null)
                return;

            FindPointAskPhysicalInteractionState state = tracker.CurrentState;
            if (state.LookLogicalConfirmed && !state.NeckCommandDispatched)
                ObserveNeckDispatch(tracker, state.RunTarget.TargetKey);

            if (pointBaselineCaptured && state.PointRequested &&
                !state.ArmCommandDispatched)
            {
                ObserveArmDispatch(tracker, state.RunTarget.TargetKey);
            }
        }

        private void ObserveNeckDispatch(
            FindPointAskPhysicalInteractionTracker tracker,
            string targetKey)
        {
            if (neckOutputOwner == null || neckOutputController == null ||
                !string.Equals(
                    neckOutputOwner.LastTargetKey,
                    targetKey,
                    StringComparison.Ordinal))
            {
                return;
            }

            for (int servoId = FirstNeckServoId;
                 servoId <= LastNeckServoId;
                 servoId++)
            {
                if (!TryGetNewDispatch(
                        servoId,
                        hadRunBaseline,
                        runBaselineDispatchTime,
                        out int angle))
                {
                    continue;
                }

                if (tracker.TryRecordNeckCommandDispatched(
                        targetKey,
                        servoId,
                        angle,
                        NeckOutputGateOpen))
                {
                    Debug.Log(
                        "[FindPointAskM6][NECK_DISPATCH] " +
                        "TargetKey=" + targetKey +
                        " ServoId=" + servoId +
                        " Angle=" + angle +
                        " Completion=PhysicalCompletionUnverified");
                }
                return;
            }
        }

        private void ObserveArmDispatch(
            FindPointAskPhysicalInteractionTracker tracker,
            string targetKey)
        {
            for (int servoId = FirstArmServoId;
                 servoId <= LastArmServoId;
                 servoId++)
            {
                if (!TryGetNewDispatch(
                        servoId,
                        hadPointBaseline,
                        pointBaselineDispatchTime,
                        out int angle))
                {
                    continue;
                }

                if (tracker.TryRecordArmCommandDispatched(
                        targetKey,
                        servoId,
                        angle,
                        ArmOutputArmed))
                {
                    Debug.Log(
                        "[FindPointAskM6][ARM_DISPATCH] " +
                        "TargetKey=" + targetKey +
                        " ServoId=" + servoId +
                        " Angle=" + angle +
                        " Completion=PhysicalCompletionUnverified");
                }
                return;
            }
        }

        private void CaptureBaseline(
            int firstServoId,
            int lastServoId,
            bool[] hadBaseline,
            float[] dispatchTimes)
        {
            for (int servoId = firstServoId;
                 servoId <= lastServoId;
                 servoId++)
            {
                int ignoredAngle;
                float dispatchTime = -1f;
                hadBaseline[servoId] = commandCoordinator != null &&
                    commandCoordinator.TryGetLastServoDispatch(
                        servoId,
                        out ignoredAngle,
                        out dispatchTime);
                dispatchTimes[servoId] = hadBaseline[servoId]
                    ? dispatchTime
                    : -1f;
            }
        }

        private bool TryGetNewDispatch(
            int servoId,
            bool[] hadBaseline,
            float[] dispatchTimes,
            out int angle)
        {
            angle = 0;
            if (!commandCoordinator.TryGetLastServoDispatch(
                    servoId,
                    out int currentAngle,
                    out float currentTime))
            {
                return false;
            }

            bool isNew = !hadBaseline[servoId] ||
                currentTime > dispatchTimes[servoId];
            if (!isNew)
                return false;

            angle = currentAngle;
            return true;
        }
    }
}
