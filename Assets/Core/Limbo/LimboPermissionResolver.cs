// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;
using UnityEngine.Serialization;
using SalieriAI.Core.State;

namespace SalieriAI.Core.Limbo
{
    public sealed class LimboPermissionResolver : MonoBehaviour
    {
        [SerializeField] private InteractionStateController stateController;
        [SerializeField] private LimboPermission limboPermission;

        [Header("Startup Orientation Safety")]
        [FormerlySerializedAs("idleTrackFaceDelaySeconds")]
        [SerializeField] private float idleOrientationTrackDelaySeconds = 1.5f;

        private InteractionState lastState;
        private float idleEnteredTime = -1f;
        private bool idleOrientationTrackingReleased;

        private void Start()
        {
            Apply();
        }

        private void Update()
        {
            if (stateController == null)
                return;

            if (lastState == stateController.CurrentState)
            {
                ApplyIdleDelayOverride();
                return;
            }

            Apply();
        }

        private void Apply()
        {
            if (stateController == null || limboPermission == null)
                return;

            lastState = stateController.CurrentState;

            if (lastState == InteractionState.Idle)
            {
                idleEnteredTime = Time.time;
                idleOrientationTrackingReleased = false;
            }
            else
            {
                idleEnteredTime = -1f;
                idleOrientationTrackingReleased = false;
            }

            limboPermission.LockAll();

            switch (lastState)
            {
                case InteractionState.Booting:
                    limboPermission.AllowServo();
                    break;

                case InteractionState.Idle:
                    limboPermission.EnterRuntime();
                    limboPermission.AllowInterrupt();
                    break;

                case InteractionState.Tracking:
                    limboPermission.AllowOrientationTracking();
                    limboPermission.AllowServo();
                    limboPermission.AllowSpeak();
                    limboPermission.AllowThinking();
                    limboPermission.AllowInterrupt();
                    break;

                case InteractionState.TemporaryLost:
                    limboPermission.AllowOrientationTracking();
                    limboPermission.AllowServo();
                    limboPermission.AllowSearch();
                    limboPermission.AllowInterrupt();
                    break;

                case InteractionState.FullyLost:
                    limboPermission.AllowOrientationTracking();
                    limboPermission.AllowServo();
                    limboPermission.AllowSearch();
                    limboPermission.AllowInterrupt();
                    break;

                case InteractionState.Searching:
                    limboPermission.AllowOrientationTracking();
                    limboPermission.AllowServo();
                    limboPermission.AllowSearch();
                    limboPermission.AllowInterrupt();
                    break;

                case InteractionState.Thinking:
                    limboPermission.AllowServo();
                    limboPermission.AllowInterrupt();
                    break;

                case InteractionState.Speaking:
                    limboPermission.AllowServo();
                    limboPermission.AllowSpeak();
                    limboPermission.AllowInterrupt();

                    // Speaking temporarily stops orientation tracking.
                    limboPermission.DenyOrientationTracking();
                    break;

                case InteractionState.Listening:
                    limboPermission.AllowOrientationTracking();
                    limboPermission.AllowServo();
                    limboPermission.AllowInterrupt();
                    break;

                case InteractionState.Acting:
                    // Phase 4 canonical neck actions are Orientation Target
                    // requests. Keep the shared downstream gate authoritative
                    // while allowing the solved Virtual Body pose to reach it.
                    limboPermission.AllowOrientationTracking();
                    limboPermission.AllowServo();
                    limboPermission.AllowInterrupt();
                    break;

                case InteractionState.Recovering:
                    limboPermission.AllowOrientationTracking();
                    limboPermission.AllowServo();
                    limboPermission.AllowSpeak();
                    limboPermission.AllowInterrupt();
                    break;

                case InteractionState.Emergency:
                    limboPermission.EnterEmergencyMode();
                    break;
            }

            ApplyIdleDelayOverride();

            Debug.Log($"[LimboPermissionResolver] Applied: {lastState}");
        }

        private void ApplyIdleDelayOverride()
        {
            if (limboPermission == null)
                return;

            if (lastState != InteractionState.Idle)
                return;

            if (idleEnteredTime < 0f)
                return;

            float elapsed = Time.time - idleEnteredTime;

            if (elapsed < idleOrientationTrackDelaySeconds)
            {
                limboPermission.DenyOrientationTracking();

                Debug.Log(
                    $"[LimboPermissionResolver] Idle delay: " +
                    $"CanTrackOrientation=false elapsed:{elapsed:F2}"
                );

                return;
            }

            if (!idleOrientationTrackingReleased)
            {
                limboPermission.AllowOrientationTracking();
                idleOrientationTrackingReleased = true;

                Debug.Log(
                    "[LimboPermissionResolver] Idle delay finished: " +
                    "CanTrackOrientation=true"
                );
            }
        }
    }
}
