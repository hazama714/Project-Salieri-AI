// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using UnityEngine;

using SalieriAI.Core.Execution;

namespace SalieriAI.Core.State
{
    /// <summary>
    /// InteractionStateController
    ///
    /// 現在の InteractionState を保持する。
    ///
    /// Phase 5-B:
    /// 状態変更時に ExecutionResourceManager.ApplyProfile(state) を呼び、
    /// InteractionState と StateExecutionProfile を同期する。
    ///
    /// このクラスは「現在状態」を管理するだけで、
    /// 行動・発話・LLM・サーボを直接起動しない。
    /// </summary>
    public sealed class InteractionStateController : MonoBehaviour
    {
        [Header("State")]
        [SerializeField]
        private InteractionState currentState = InteractionState.Booting;

        [Header("Resource Profile")]
        [Tooltip("InteractionState変更時に StateExecutionProfile を適用する。未設定でも従来通り動作する。")]
        [SerializeField]
        private ExecutionResourceManager executionResourceManager;

        [Tooltip("Start時に現在状態のExecutionProfileを適用する。")]
        [SerializeField]
        private bool applyProfileOnStart = true;

        [Header("Debug")]
        [SerializeField]
        private bool verboseLog = true;

        public InteractionState CurrentState => currentState;

        /// <summary>
        /// 状態変更通知。
        /// oldState, newState の順で通知する。
        ///
        /// 将来的に Limbo / Runtime / UI / Debug などが購読できる。
        /// </summary>
        public event Action<InteractionState, InteractionState> OnStateChanged;

        public bool IsBusy =>
            currentState == InteractionState.Thinking ||
            currentState == InteractionState.Speaking ||
            currentState == InteractionState.Acting ||
            currentState == InteractionState.Recovering;

        private void Start()
        {
            if (!applyProfileOnStart)
                return;

            ApplyExecutionProfile(currentState, "Start");
        }

        public void SetState(InteractionState next)
        {
            if (currentState == next)
                return;

            InteractionState previous = currentState;

            Debug.Log($"[InteractionStateController] {previous} -> {next}");

            currentState = next;

            ApplyExecutionProfile(next, "SetState");

            NotifyStateChanged(previous, next);
        }

        public void SetIdle()
        {
            SetState(InteractionState.Idle);
        }

        public void SetTracking()
        {
            SetState(InteractionState.Tracking);
        }

        public void SetTemporaryLost()
        {
            SetState(InteractionState.TemporaryLost);
        }

        public void SetFullyLost()
        {
            SetState(InteractionState.FullyLost);
        }

        public void SetSearching()
        {
            SetState(InteractionState.Searching);
        }

        public void SetThinking()
        {
            SetState(InteractionState.Thinking);
        }

        public void SetSpeaking()
        {
            SetState(InteractionState.Speaking);
        }

        public void SetListening()
        {
            SetState(InteractionState.Listening);
        }

        public void SetActing()
        {
            SetState(InteractionState.Acting);
        }

        public void SetRecovering()
        {
            SetState(InteractionState.Recovering);
        }

        public void SetEmergency()
        {
            SetState(InteractionState.Emergency);
        }

        /// <summary>
        /// 現在の状態に対応する ExecutionProfile を再適用する。
        /// Inspector設定後の確認や、後からExecutionResourceManagerを接続した時に使える。
        /// </summary>
        [ContextMenu("Apply Current Execution Profile")]
        public void ApplyCurrentExecutionProfile()
        {
            ApplyExecutionProfile(currentState, "Manual");
        }

        private void ApplyExecutionProfile(
            InteractionState state,
            string reason
        )
        {
            if (executionResourceManager == null)
            {
                if (verboseLog)
                {
                    Debug.Log(
                        "[InteractionStateController] ExecutionResourceManager not assigned. " +
                        "Profile apply skipped. state=" +
                        state +
                        " reason=" +
                        reason
                    );
                }

                return;
            }

            if (verboseLog)
            {
                Debug.Log(
                    "[InteractionStateController] Apply ExecutionProfile: " +
                    state +
                    " reason=" +
                    reason
                );
            }

            executionResourceManager.ApplyProfile(state);
        }

        private void NotifyStateChanged(
            InteractionState previous,
            InteractionState next
        )
        {
            try
            {
                OnStateChanged?.Invoke(previous, next);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[InteractionStateController] OnStateChanged exception: " +
                    ex
                );
            }
        }
    }
}