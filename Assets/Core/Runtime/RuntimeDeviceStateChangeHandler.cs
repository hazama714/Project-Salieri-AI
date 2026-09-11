// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

using SalieriAI.Core.Input;
using SalieriAI.Core.State;
using SalieriAI.Core.Limbo;

namespace SalieriAI.Core.Runtime
{
    public sealed class RuntimeDeviceStateChangeHandler : MonoBehaviour
    {
        [Header("Runtime State Refs")]
        [SerializeField]
        private InteractionStateController stateController;

        [SerializeField]
        private LimboPermission limboPermission;

        [Header("Debug")]
        [SerializeField]
        private bool verboseLog = true;

        private sealed class DeviceStateRuntimeContext
        {
            public ExternalInputEvent SourceEvent;
            public string Payload;
            public InteractionState CurrentState;
            public bool HasStateController;
            public bool HasLimboPermission;
            public bool IsEmergency;
        }

        public void Process(ExternalInputEvent inputEvent)
        {
            DeviceStateRuntimeContext context =
                BuildDeviceStateRuntimeContext(inputEvent);

            if (verboseLog)
            {
                Debug.Log(
                    "[RuntimeDeviceStateChangeHandler] " +
                    "DeviceStateChange received: " +
                    "payload=" +
                    context.Payload +
                    " state=" +
                    FormatStateForLog(
                        context.HasStateController,
                        context.CurrentState
                    ) +
                    " emergency=" +
                    FormatBoolForLog(
                        context.HasLimboPermission,
                        context.IsEmergency
                    )
                );
            }

            PrepareDeviceStateChangeHandoff(context);
        }

        private DeviceStateRuntimeContext
            BuildDeviceStateRuntimeContext(
                ExternalInputEvent inputEvent
            )
        {
            bool hasStateController =
                stateController != null;

            bool hasLimboPermission =
                limboPermission != null;

            InteractionState currentState =
                hasStateController
                    ? stateController.CurrentState
                    : InteractionState.Idle;

            return new DeviceStateRuntimeContext
            {
                SourceEvent = inputEvent,
                Payload = inputEvent != null
                    ? (inputEvent.Payload ?? string.Empty)
                    : string.Empty,
                CurrentState = currentState,
                HasStateController = hasStateController,
                HasLimboPermission = hasLimboPermission,
                IsEmergency =
                    hasLimboPermission &&
                    limboPermission.IsEmergencyMode
            };
        }

        private void PrepareDeviceStateChangeHandoff(
            DeviceStateRuntimeContext context
        )
        {
            if (context == null)
            {
                Debug.LogWarning(
                    "[RuntimeDeviceStateChangeHandler] " +
                    "DeviceStateChange handoff blocked: " +
                    "context is null."
                );

                return;
            }

            if (verboseLog)
            {
                Debug.Log(
                    "[RuntimeDeviceStateChangeHandler] " +
                    "DeviceStateChange handoff prepared: " +
                    "payload=" +
                    context.Payload +
                    " route=future DeviceState / " +
                    "ExecutionResource / RuntimeState update"
                );
            }
        }

        private string FormatStateForLog(
            bool hasStateController,
            InteractionState state
        )
        {
            return hasStateController
                ? state.ToString()
                : "Unknown";
        }

        private string FormatBoolForLog(
            bool hasValue,
            bool value
        )
        {
            return hasValue
                ? value.ToString()
                : "Unknown";
        }
    }
}