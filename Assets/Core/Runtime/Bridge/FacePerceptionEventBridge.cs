// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

using SalieriAI.Core.Input;

using SalieriAI.Core.Perception.Buffer;

namespace SalieriAI.Core.Runtime
{
    /// <summary>
    /// Bridges FacePerceptionBuffer state changes into ExternalInputBuffer.
    ///
    /// This class is perception-event only.
    /// It does not decide actions, move the body, speak, call LLM, or modify state.
    ///
    /// Flow:
    /// FacePerceptionBuffer
    /// -> FacePerceptionEventBridge
    /// -> ExternalInputBuffer
    /// -> AutonomousClock
    /// -> RuntimeProcessor
    /// </summary>
    public sealed class FacePerceptionEventBridge : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private FacePerceptionBuffer facePerceptionBuffer;
        [SerializeField] private ExternalInputBuffer externalInputBuffer;

        [Header("Priority")]
        [SerializeField] private ExternalInputPriority stableFoundPriority = ExternalInputPriority.Normal;
        [SerializeField] private ExternalInputPriority temporaryLostPriority = ExternalInputPriority.Normal;
        [SerializeField] private ExternalInputPriority fullyLostPriority = ExternalInputPriority.High;

        [Header("Debug")]
        [SerializeField] private bool verboseLog = true;

        private void Update()
        {
            if (facePerceptionBuffer == null)
                return;

            if (externalInputBuffer == null)
                return;

            if (facePerceptionBuffer.BecameStableFound)
            {
                PushFaceEvent(
                    "StableFound",
                    stableFoundPriority
                );
            }

            if (facePerceptionBuffer.BecameTemporaryLost)
            {
                PushFaceEvent(
                    "TemporaryLost",
                    temporaryLostPriority
                );
            }

            if (facePerceptionBuffer.BecameFullyLost)
            {
                PushFaceEvent(
                    "FullyLost",
                    fullyLostPriority
                );
            }
        }

        private void PushFaceEvent(
            string payload,
            ExternalInputPriority priority
        )
        {
            externalInputBuffer.Push(
                ExternalInputType.FaceEvent,
                priority,
                payload
            );

            if (verboseLog)
            {
                Debug.Log(
                    "[FacePerceptionEventBridge] Push FaceEvent: " +
                    payload +
                    " priority=" +
                    priority
                );
            }
        }
    }
}