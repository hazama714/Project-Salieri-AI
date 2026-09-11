// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;
using UnityEngine;
using SalieriAI.Body.Command;
using SalieriAI.Body.Servo.Bridge;

namespace SalieriAI.Body.Servo.Safety
{
    public enum ArmServoOutputState
    {
        Disarmed,
        Arming,
        Armed,
        Fault
    }

    public enum ArmDisarmReason
    {
        None,
        UserRequest,
        ConnectionLost,
        Emergency,
        ConfigurationInvalid,
        PartialArm,
        BaselineFailed,
        StartupSynchronizationFailed,
        ArmingTimeout,
        ControllerDisabled,
        ApplicationPause,
        ApplicationQuit,
        RuntimeFault
    }

    public enum ArmStartupSynchronizationPhase
    {
        Inactive,
        AwaitingBaselines,
        AwaitingDispatch,
        EstimatedSettling,
        Complete
    }

    [DefaultExecutionOrder(300)]
    public sealed class ArmServoOutputController : MonoBehaviour
    {
        private const int MinimumArmServoId = 2;
        private const int MaximumArmServoId = 9;
        private const int RequiredArmBridgeCount = 8;

        [Header("Arm Servo Bridges")]

        [SerializeField]
        private BodyJointServoBridge[] armBridges;

        [SerializeField]
        private BodyCommandCoordinator commandCoordinator;

        [Header("Arming Safety")]

        [SerializeField]
        [Min(0.1f)]
        private float armingTimeoutSeconds = 2.0f;

        [SerializeField]
        [Min(0f)]
        [Tooltip(
            "Estimated non-blocking settle interval after all eight startup " +
            "commands were dispatched. This is not PhysicalReached verification."
        )]
        private float startupSettleSeconds = 1.0f;

        private ArmServoOutputState state =
            ArmServoOutputState.Disarmed;

        private bool connectionReady;
        private bool emergencyActive;
        private int armRequestFrame = -1;
        private float armRequestTime;
        private bool disarmInProgress;
        private bool armRequestPending;
        private bool armOperationInProgress;
        private bool applicationQuitting;
        private IConnectionStateProvider connectionStateProvider;
        private bool connectionProviderReadExceptionLogged;
        private ArmStartupSynchronizationPhase startupSynchronizationPhase =
            ArmStartupSynchronizationPhase.Inactive;
        private readonly Dictionary<int, long>
            startupDispatchSequencesBeforeRequest =
                new Dictionary<int, long>();
        private readonly HashSet<int> startupDispatchedServoIds =
            new HashSet<int>();
        private float startupSettleStartedAt;

        public ArmServoOutputState State => state;

        public bool IsConnectionReady => connectionReady;

        public bool IsEmergencyActive => emergencyActive;

        public ArmStartupSynchronizationPhase StartupSynchronizationPhase =>
            startupSynchronizationPhase;

        public int StartupSynchronizationDispatchedCount =>
            startupDispatchedServoIds.Count;

        public int StartupSynchronizationRequiredCount =>
            RequiredArmBridgeCount;

        public float StartupSettleSeconds =>
            Mathf.Max(0f, startupSettleSeconds);

        public bool HasValidConfiguration
        {
            get
            {
                return TryValidateConfiguration(
                    out _
                );
            }
        }

        public bool IsAllArmed
        {
            get
            {
                if (armBridges == null ||
                    armBridges.Length != RequiredArmBridgeCount)
                {
                    return false;
                }

                for (int i = 0; i < armBridges.Length; i++)
                {
                    BodyJointServoBridge bridge = armBridges[i];

                    if (bridge == null ||
                        !bridge.IsOutputArmed)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public bool IsAnyArmed
        {
            get
            {
                if (armBridges == null)
                {
                    return false;
                }

                for (int i = 0; i < armBridges.Length; i++)
                {
                    BodyJointServoBridge bridge = armBridges[i];

                    if (bridge != null &&
                        bridge.IsOutputArmed)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public event Action<ArmServoOutputState> StateChanged;

        private void OnEnable()
        {
            applicationQuitting = false;
            armRequestPending = false;
            armOperationInProgress = false;
            disarmInProgress = false;
            connectionReady = false;
            armRequestFrame = -1;
            armRequestTime = 0f;
            ResetStartupSynchronizationState();

            DisarmConfiguredBridges();

            int removedCount =
                ClearPendingArmServoCommands();

            bool stateChanged =
                AssignState(ArmServoOutputState.Disarmed);

            Debug.Log(
                $"[ArmServoOutputController] Enabled in Disarmed state. " +
                $"removedPending={removedCount}",
                this
            );

            if (stateChanged)
            {
                RaiseStateChanged();
            }

            SubscribeConnectionStateProvider();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            SynchronizeConnectionState();

            switch (state)
            {
                case ArmServoOutputState.Arming:
                    if (armRequestPending)
                    {
                        BeginPendingArmRequest();
                        return;
                    }

                    MonitorArming();
                    break;

                case ArmServoOutputState.Armed:
                    MonitorArmed();
                    break;

                case ArmServoOutputState.Disarmed:
                case ArmServoOutputState.Fault:
                    if (IsAnyArmed)
                    {
                        Debug.LogWarning(
                            "[ArmServoOutputController] " +
                            "Direct or partial Arm detected outside manager.",
                            this
                        );

                        DisarmAll(ArmDisarmReason.PartialArm);
                    }
                    break;
            }
        }

        private void OnDisable()
        {
            UnsubscribeConnectionStateProvider();
            connectionReady = false;
            armRequestPending = false;
            DisarmAll(ArmDisarmReason.ControllerDisabled);
        }

        private void OnDestroy()
        {
            UnsubscribeConnectionStateProvider();
            connectionReady = false;
            armRequestPending = false;
            DisarmConfiguredBridges();
            ClearPendingArmServoCommands();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                armRequestPending = false;
                DisarmAll(ArmDisarmReason.ApplicationPause);
            }
        }

        private void OnApplicationQuit()
        {
            applicationQuitting = true;
            armRequestPending = false;
            DisarmAll(ArmDisarmReason.ApplicationQuit);
        }

        public bool ArmAll()
        {
            if (!Application.isPlaying)
            {
                LogArmRejected("Application is not playing.");
                return false;
            }

            if (applicationQuitting)
            {
                LogArmRejected("Application is quitting.");
                return false;
            }

            if (!isActiveAndEnabled)
            {
                LogArmRejected("Controller is not active and enabled.");
                return false;
            }

            if (disarmInProgress)
            {
                LogArmRejected("Disarm is in progress.");
                return false;
            }

            if (armOperationInProgress)
            {
                LogArmRejected("Arm operation is in progress.");
                return false;
            }

            if (armRequestPending)
            {
                LogArmRejected("Arm request is already pending.");
                return false;
            }

            if (state == ArmServoOutputState.Armed)
            {
                return true;
            }

            if (state == ArmServoOutputState.Arming)
            {
                LogArmRejected("Arming is already in progress.");
                return false;
            }

            if (emergencyActive)
            {
                LogArmRejected("Emergency is active.");
                return false;
            }

            if (!connectionReady)
            {
                LogArmRejected("Connection is not ready.");
                return false;
            }

            if (!ValidateConfiguration(out string error))
            {
                LogArmRejected(
                    $"Configuration is invalid: {error}"
                );

                DisarmAll(
                    ArmDisarmReason.ConfigurationInvalid
                );
                return false;
            }

            if (IsAnyArmed)
            {
                LogArmRejected(
                    "One or more Bridges are already armed."
                );

                DisarmAll(ArmDisarmReason.PartialArm);
                return false;
            }

            int removedCount =
                commandCoordinator.ClearPendingServoCommands(
                    MinimumArmServoId,
                    MaximumArmServoId
                );

            armRequestPending = true;
            armRequestFrame = -1;
            armRequestTime = 0f;
            ResetStartupSynchronizationState();

            bool stateChanged =
                AssignState(ArmServoOutputState.Arming);

            Debug.Log(
                $"[ArmServoOutputController] Arm request reserved. " +
                $"frame={Time.frameCount} " +
                $"removedPending={removedCount}",
                this
            );

            if (stateChanged)
            {
                RaiseStateChanged();
            }

            if (state != ArmServoOutputState.Arming ||
                !armRequestPending ||
                disarmInProgress)
            {
                return false;
            }

            return true;
        }

        public void DisarmAll(ArmDisarmReason reason)
        {
            if (disarmInProgress)
            {
                return;
            }

            disarmInProgress = true;
            armRequestPending = false;
            bool stateChanged = false;

            try
            {
                DisarmConfiguredBridges();

                int removedCount =
                    ClearPendingArmServoCommands();

                armRequestFrame = -1;
                armRequestTime = 0f;
                ResetStartupSynchronizationState();

                ArmServoOutputState finalState =
                    IsFaultReason(reason)
                        ? ArmServoOutputState.Fault
                        : ArmServoOutputState.Disarmed;

                stateChanged = AssignState(finalState);

                Debug.Log(
                    $"[ArmServoOutputController] Disarmed. " +
                    $"reason={reason} " +
                    $"removedPending={removedCount} " +
                    $"state={state}",
                    this
                );
            }
            finally
            {
                disarmInProgress = false;
            }

            if (stateChanged)
            {
                RaiseStateChanged();
            }
        }

        public void SetConnectionReady(bool ready)
        {
            connectionReady = ready;

            if (!ready)
            {
                DisarmAll(ArmDisarmReason.ConnectionLost);
            }
        }

        public void SetEmergencyActive(bool active)
        {
            emergencyActive = active;

            if (active)
            {
                DisarmAll(ArmDisarmReason.Emergency);
            }
        }

        public bool ValidateConfiguration(out string error)
        {
            bool valid =
                TryValidateConfiguration(out error);

            if (!valid)
            {
                Debug.LogWarning(
                    $"[ArmServoOutputController] " +
                    $"Configuration invalid: {error}",
                    this
                );
            }

            return valid;
        }

        private bool TryValidateConfiguration(out string error)
        {
            if (armBridges == null)
            {
                error = "armBridges is null.";
                return false;
            }

            if (armBridges.Length != RequiredArmBridgeCount)
            {
                error =
                    $"armBridges must contain exactly " +
                    $"{RequiredArmBridgeCount} elements. " +
                    $"actual={armBridges.Length}";
                return false;
            }

            HashSet<BodyJointServoBridge> uniqueBridges =
                new HashSet<BodyJointServoBridge>();

            HashSet<int> uniqueServoIds =
                new HashSet<int>();

            for (int i = 0; i < armBridges.Length; i++)
            {
                BodyJointServoBridge bridge = armBridges[i];

                if (bridge == null)
                {
                    error = $"armBridges[{i}] is null.";
                    return false;
                }

                if (!uniqueBridges.Add(bridge))
                {
                    error =
                        $"Duplicate Bridge reference at index {i}: " +
                        $"{bridge.name}.";
                    return false;
                }

                if (bridge.Servo == null)
                {
                    error =
                        $"Servo is null at index {i}: " +
                        $"name={bridge.name}.";
                    return false;
                }

                int servoId = bridge.ServoId;

                if (servoId < MinimumArmServoId ||
                    servoId > MaximumArmServoId)
                {
                    error =
                        $"Servo ID is outside the arm range at " +
                        $"index {i}: id={servoId}.";
                    return false;
                }

                if (!uniqueServoIds.Add(servoId))
                {
                    error = $"Duplicate Servo ID: {servoId}.";
                    return false;
                }

                if (!bridge.isActiveAndEnabled)
                {
                    error =
                        $"Bridge is not active and enabled: " +
                        $"id={servoId} name={bridge.name}.";
                    return false;
                }

                if (!bridge.OutputEnabled)
                {
                    error =
                        $"Bridge output is disabled: " +
                        $"id={servoId} name={bridge.name}.";
                    return false;
                }

                if (bridge.JointConstraint == null)
                {
                    error =
                        $"JointConstraint is null: " +
                        $"id={servoId} name={bridge.name}.";
                    return false;
                }
            }

            for (int servoId = MinimumArmServoId;
                servoId <= MaximumArmServoId;
                servoId++)
            {
                if (!uniqueServoIds.Contains(servoId))
                {
                    error = $"Required Servo ID is missing: {servoId}.";
                    return false;
                }
            }

            if (commandCoordinator == null)
            {
                error = "commandCoordinator is null.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void MonitorArming()
        {
            if (!connectionReady)
            {
                DisarmAll(ArmDisarmReason.ConnectionLost);
                return;
            }

            if (emergencyActive)
            {
                DisarmAll(ArmDisarmReason.Emergency);
                return;
            }

            if (!TryValidateConfiguration(out string error))
            {
                Debug.LogWarning(
                    $"[ArmServoOutputController] " +
                    $"Configuration became invalid while arming: {error}",
                    this
                );

                DisarmAll(
                    ArmDisarmReason.ConfigurationInvalid
                );
                return;
            }

            if (IsAnyArmed && !IsAllArmed)
            {
                DisarmAll(ArmDisarmReason.PartialArm);
                return;
            }

            if (armRequestPending || armRequestFrame < 0)
            {
                return;
            }

            if (Time.frameCount == armRequestFrame)
            {
                return;
            }

            if (HasCompletedBaselineFailure())
            {
                DisarmAll(ArmDisarmReason.BaselineFailed);
                return;
            }

            switch (startupSynchronizationPhase)
            {
                case ArmStartupSynchronizationPhase.AwaitingBaselines:
                    if (AreAllStartupBaselinesReady())
                    {
                        RequestStartupSynchronization();
                    }
                    break;

                case ArmStartupSynchronizationPhase.AwaitingDispatch:
                    ObserveStartupSynchronizationDispatches();
                    break;

                case ArmStartupSynchronizationPhase.EstimatedSettling:
                    CompleteStartupSynchronizationAfterSettle();
                    break;
            }

            if (state == ArmServoOutputState.Armed)
            {
                return;
            }

            float elapsed =
                Time.unscaledTime - armRequestTime;

            if (elapsed > Mathf.Max(0.1f, armingTimeoutSeconds))
            {
                DisarmAll(ArmDisarmReason.ArmingTimeout);
            }
        }

        private void MonitorArmed()
        {
            if (!connectionReady)
            {
                DisarmAll(ArmDisarmReason.ConnectionLost);
                return;
            }

            if (emergencyActive)
            {
                DisarmAll(ArmDisarmReason.Emergency);
                return;
            }

            if (!TryValidateConfiguration(out string error))
            {
                Debug.LogWarning(
                    $"[ArmServoOutputController] " +
                    $"Configuration became invalid while armed: {error}",
                    this
                );

                DisarmAll(
                    ArmDisarmReason.ConfigurationInvalid
                );
                return;
            }

            if (!IsAllArmed)
            {
                DisarmAll(ArmDisarmReason.PartialArm);
                return;
            }

            if (!AreAllBridgesOutputReady())
            {
                DisarmAll(ArmDisarmReason.RuntimeFault);
            }
        }

        private bool AreAllBridgesOutputReady()
        {
            if (armBridges == null ||
                armBridges.Length != RequiredArmBridgeCount)
            {
                return false;
            }

            for (int i = 0; i < armBridges.Length; i++)
            {
                BodyJointServoBridge bridge = armBridges[i];

                if (bridge == null ||
                    !bridge.IsOutputReady)
                {
                    return false;
                }
            }

            return true;
        }

        private bool HasCompletedBaselineFailure()
        {
            if (armBridges == null)
            {
                return true;
            }

            for (int i = 0; i < armBridges.Length; i++)
            {
                BodyJointServoBridge bridge = armBridges[i];

                if (bridge == null)
                {
                    return true;
                }

                if (!bridge.IsStartupBaselinePending &&
                    !bridge.HasCompletedStartupBaseline)
                {
                    return true;
                }
            }

            return false;
        }

        private bool AreAllStartupBaselinesReady()
        {
            if (armBridges == null ||
                armBridges.Length != RequiredArmBridgeCount)
            {
                return false;
            }

            for (int i = 0; i < armBridges.Length; i++)
            {
                BodyJointServoBridge bridge = armBridges[i];

                if (bridge == null ||
                    !bridge.HasCompletedStartupBaseline ||
                    !bridge.IsStartupSynchronizationPending)
                {
                    return false;
                }
            }

            return true;
        }

        private void RequestStartupSynchronization()
        {
            startupDispatchSequencesBeforeRequest.Clear();
            startupDispatchedServoIds.Clear();

            for (int i = 0; i < armBridges.Length; i++)
            {
                BodyJointServoBridge bridge = armBridges[i];
                int servoId = bridge.ServoId;

                long priorDispatchSequence = 0L;
                commandCoordinator.TryGetLastServoDispatchSequence(
                    servoId,
                    out priorDispatchSequence
                );

                startupDispatchSequencesBeforeRequest[servoId] =
                    priorDispatchSequence;

                if (!bridge.TrySendStartupSynchronizationCommand(
                    out int servoInputAngle,
                    out string reason))
                {
                    Debug.LogWarning(
                        $"[ArmServoOutputController][STARTUP_SYNC_FAILED] " +
                        $"ServoID={servoId} Reason={reason}",
                        this
                    );

                    DisarmAll(
                        ArmDisarmReason.StartupSynchronizationFailed
                    );
                    return;
                }

                Debug.Log(
                    $"[ArmServoOutputController][STARTUP_SYNC_QUEUED] " +
                    $"ServoID={servoId} ServoInput={servoInputAngle}",
                    this
                );
            }

            startupSynchronizationPhase =
                ArmStartupSynchronizationPhase.AwaitingDispatch;

            Debug.Log(
                $"[ArmServoOutputController][STARTUP_SYNC_REQUESTED] " +
                $"Count={RequiredArmBridgeCount}",
                this
            );
        }

        private void ObserveStartupSynchronizationDispatches()
        {
            for (int i = 0; i < armBridges.Length; i++)
            {
                int servoId = armBridges[i].ServoId;

                if (startupDispatchedServoIds.Contains(servoId) ||
                    !commandCoordinator.TryGetLastServoDispatch(
                        servoId,
                        out int angle,
                        out _) ||
                    !commandCoordinator.TryGetLastServoDispatchSequence(
                        servoId,
                        out long dispatchSequence) ||
                    !startupDispatchSequencesBeforeRequest.TryGetValue(
                        servoId,
                        out long priorDispatchSequence) ||
                    dispatchSequence <= priorDispatchSequence)
                {
                    continue;
                }

                startupDispatchedServoIds.Add(servoId);

                Debug.Log(
                    $"[ArmServoOutputController][STARTUP_SYNC_DISPATCHED] " +
                    $"ServoID={servoId} Angle={angle} " +
                    $"Count={startupDispatchedServoIds.Count}/" +
                    $"{RequiredArmBridgeCount}",
                    this
                );
            }

            if (startupDispatchedServoIds.Count !=
                RequiredArmBridgeCount)
            {
                return;
            }

            startupSettleStartedAt = Time.unscaledTime;
            startupSynchronizationPhase =
                ArmStartupSynchronizationPhase.EstimatedSettling;

            Debug.Log(
                $"[ArmServoOutputController][STARTUP_SYNC_SETTLING] " +
                $"EstimatedSeconds={StartupSettleSeconds:F2} " +
                $"PhysicalReached=UNAVAILABLE",
                this
            );
        }

        private void CompleteStartupSynchronizationAfterSettle()
        {
            if (Time.unscaledTime - startupSettleStartedAt <
                StartupSettleSeconds)
            {
                return;
            }

            for (int i = 0; i < armBridges.Length; i++)
            {
                if (!armBridges[i].CompleteStartupSynchronization(
                    out string reason))
                {
                    Debug.LogWarning(
                        $"[ArmServoOutputController][STARTUP_SYNC_FAILED] " +
                        $"ServoID={armBridges[i].ServoId} Reason={reason}",
                        this
                    );

                    DisarmAll(
                        ArmDisarmReason.StartupSynchronizationFailed
                    );
                    return;
                }
            }

            startupSynchronizationPhase =
                ArmStartupSynchronizationPhase.Complete;

            bool stateChanged = AssignState(ArmServoOutputState.Armed);

            Debug.Log(
                $"[ArmServoOutputController][STARTUP_SYNC_COMPLETE] " +
                $"DispatchCount={startupDispatchedServoIds.Count} " +
                $"PhysicalReached=UNAVAILABLE",
                this
            );

            if (stateChanged)
            {
                RaiseStateChanged();
            }
        }

        private void ResetStartupSynchronizationState()
        {
            startupSynchronizationPhase =
                ArmStartupSynchronizationPhase.Inactive;
            startupDispatchSequencesBeforeRequest.Clear();
            startupDispatchedServoIds.Clear();
            startupSettleStartedAt = 0f;
        }

        private void DisarmConfiguredBridges()
        {
            if (armBridges == null)
            {
                return;
            }

            for (int i = 0; i < armBridges.Length; i++)
            {
                BodyJointServoBridge bridge = armBridges[i];

                if (bridge != null)
                {
                    bridge.DisarmOutput();
                }
            }
        }

        private void BeginPendingArmRequest()
        {
            if (disarmInProgress || armOperationInProgress)
            {
                return;
            }

            if (!connectionReady)
            {
                armRequestPending = false;
                DisarmAll(ArmDisarmReason.ConnectionLost);
                return;
            }

            if (emergencyActive)
            {
                armRequestPending = false;
                DisarmAll(ArmDisarmReason.Emergency);
                return;
            }

            if (!ValidateConfiguration(out _))
            {
                armRequestPending = false;
                DisarmAll(
                    ArmDisarmReason.ConfigurationInvalid
                );
                return;
            }

            if (IsAnyArmed)
            {
                armRequestPending = false;
                DisarmAll(ArmDisarmReason.PartialArm);
                return;
            }

            int removedCount =
                commandCoordinator.ClearPendingServoCommands(
                    MinimumArmServoId,
                    MaximumArmServoId
                );

            bool partialArmDetected = false;
            armOperationInProgress = true;

            try
            {
                for (int i = 0; i < armBridges.Length; i++)
                {
                    armBridges[i].ArmOutput();
                }

                if (!IsAllArmed)
                {
                    partialArmDetected = true;
                }
                else
                {
                    armRequestPending = false;
                    armRequestFrame = Time.frameCount;
                    armRequestTime = Time.unscaledTime;
                    startupSynchronizationPhase =
                        ArmStartupSynchronizationPhase.AwaitingBaselines;
                }
            }
            finally
            {
                armOperationInProgress = false;
            }

            if (partialArmDetected)
            {
                armRequestPending = false;

                Debug.LogWarning(
                    "[ArmServoOutputController] " +
                    "Arming failed: not all Bridges accepted ArmOutput().",
                    this
                );

                DisarmAll(ArmDisarmReason.PartialArm);
                return;
            }

            Debug.Log(
                $"[ArmServoOutputController] Arming started. " +
                $"frame={armRequestFrame} " +
                $"removedPending={removedCount}",
                this
            );
        }

        private void SubscribeConnectionStateProvider()
        {
            UnsubscribeConnectionStateProvider();

            connectionStateProvider =
                commandCoordinator as IConnectionStateProvider;

            if (connectionStateProvider == null)
            {
                connectionReady = false;

                Debug.LogWarning(
                    "[ArmServoOutputController] " +
                    "BodyCommandCoordinator is not available as " +
                    "IConnectionStateProvider.",
                    this
                );
                return;
            }

            try
            {
                connectionStateProvider.ConnectionStateChanged +=
                    HandleConnectionStateChanged;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                connectionStateProvider = null;
                connectionReady = false;
                return;
            }

            bool connected = ReadProviderConnectionState();
            SetConnectionReady(connected);
        }

        private void UnsubscribeConnectionStateProvider()
        {
            if (connectionStateProvider != null)
            {
                try
                {
                    connectionStateProvider.ConnectionStateChanged -=
                        HandleConnectionStateChanged;
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }

            connectionStateProvider = null;
            connectionProviderReadExceptionLogged = false;
        }

        private void HandleConnectionStateChanged(bool connected)
        {
            SetConnectionReady(connected);
        }

        private void SynchronizeConnectionState()
        {
            if (connectionStateProvider == null)
            {
                if (connectionReady)
                {
                    SetConnectionReady(false);
                }

                return;
            }

            bool providerConnected =
                ReadProviderConnectionState();

            if (providerConnected != connectionReady)
            {
                SetConnectionReady(providerConnected);
            }
        }

        private bool ReadProviderConnectionState()
        {
            if (connectionStateProvider == null)
            {
                return false;
            }

            try
            {
                bool connected =
                    connectionStateProvider.IsConnected;

                connectionProviderReadExceptionLogged = false;
                return connected;
            }
            catch (Exception exception)
            {
                if (!connectionProviderReadExceptionLogged)
                {
                    connectionProviderReadExceptionLogged = true;
                    Debug.LogException(exception, this);
                }

                return false;
            }
        }

        private int ClearPendingArmServoCommands()
        {
            if (commandCoordinator == null)
            {
                return 0;
            }

            return commandCoordinator.ClearPendingServoCommands(
                MinimumArmServoId,
                MaximumArmServoId
            );
        }

        private bool AssignState(ArmServoOutputState nextState)
        {
            if (state == nextState)
            {
                return false;
            }

            state = nextState;
            return true;
        }

        private void RaiseStateChanged()
        {
            try
            {
                StateChanged?.Invoke(state);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private static bool IsFaultReason(
            ArmDisarmReason reason
        )
        {
            return reason == ArmDisarmReason.ConfigurationInvalid ||
                reason == ArmDisarmReason.PartialArm ||
                reason == ArmDisarmReason.BaselineFailed ||
                reason == ArmDisarmReason.StartupSynchronizationFailed ||
                reason == ArmDisarmReason.ArmingTimeout ||
                reason == ArmDisarmReason.RuntimeFault;
        }

        private void LogArmRejected(string reason)
        {
            Debug.LogWarning(
                $"[ArmServoOutputController] " +
                $"ArmAll rejected: {reason}",
                this
            );
        }
    }
}
