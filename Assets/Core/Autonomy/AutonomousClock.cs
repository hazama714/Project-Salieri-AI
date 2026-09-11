// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Threading.Tasks;
using UnityEngine;

using SalieriAI.Core.Input;
using SalieriAI.Core.Runtime;
using SalieriAI.Core.State;
using SalieriAI.Core.Limbo;

namespace SalieriAI.Core.Execution
{
    public class AutonomousClock : MonoBehaviour
    {
        /// <summary>
        /// Generic, ungated input-poll cadence pulse. It is emitted before
        /// ExternalInputBuffer inspection, including when the buffer is null
        /// or empty. Subscribers own all domain-specific interpretation.
        /// </summary>
        public event Action<DateTime> OnInputPollPulseUtc;

        [Header("Clock")]
        [SerializeField] private bool autoStart = true;

        [Tooltip("ExternalInputBufferを確認する間隔。音声入力などを早く拾うため短めにする。")]
        [SerializeField] private float inputPollIntervalSeconds = 0.2f;

        [Tooltip("IdleTickを発行する間隔。将来の自律思考起動間隔に相当する。")]
        [SerializeField] private float thinkIntervalSeconds = 3f;

        [Header("IdleTick")]
        [Tooltip("trueなら、自律思考を直接起動せず IdleTick Event として ExternalInputBuffer に流す。")]
        [SerializeField] private bool emitIdleTickEvent = true;

        [SerializeField]
        private ExternalInputPriority idleTickPriority =
            ExternalInputPriority.Low;

        [SerializeField] private string idleTickPayload = "IdleTick";

        [Header("Think Suppression")]
        [Tooltip("外部入力を処理した直後、自律思考を抑制する秒数。FaceEvent以外の汎用値。")]
        [SerializeField] private float suppressThinkAfterExternalInputSeconds = 5f;

        [Tooltip("UserSpeechを処理した直後、自律思考を抑制する秒数。会話反応と被らないよう少し長めにする。")]
        [SerializeField] private float suppressThinkAfterUserSpeechSeconds = 8f;

        [Tooltip("FaceEvent StableFound 後、自律思考を軽く抑制する秒数。")]
        [SerializeField] private float suppressThinkAfterFaceStableFoundSeconds = 0.5f;

        [Tooltip("FaceEvent TemporaryLost 後、自律思考を軽く抑制する秒数。")]
        [SerializeField] private float suppressThinkAfterFaceTemporaryLostSeconds = 0.5f;

        [Tooltip("FaceEvent FullyLost 後、自律思考を少し抑制する秒数。")]
        [SerializeField] private float suppressThinkAfterFaceFullyLostSeconds = 1.0f;

        [Header("Refs")]
        [SerializeField] private ExternalInputBuffer externalInputBuffer;
        [SerializeField] private RuntimeProcessor runtimeProcessor;
        [SerializeField] private InteractionStateController stateController;
        [SerializeField] private LimboPermission limboPermission;

        [Header("Resource Gate")]
        [Tooltip("StateExecutionProfile に基づき、LLM / FaceTracking / Voice 等の実行資源を管理する。未設定なら従来通り動作する。")]
        [SerializeField] private ExecutionResourceManager executionResourceManager;

        [Header("Debug")]
        [SerializeField] private bool verboseLog = true;

        private bool running;
        private bool isThinking;

        private float nextInputPollTime;
        private float nextThinkTime;
        private float suppressThinkUntilTime;

        private void Start()
        {
            if (autoStart)
            {
                StartClock();
            }
        }

        private void Update()
        {
            if (!running)
                return;

            float now = Time.time;

            if (now >= nextInputPollTime)
            {
                nextInputPollTime = now + inputPollIntervalSeconds;
                EmitInputPollPulseUtc();
                ProcessExternalInputTick();
            }

            if (now >= nextThinkTime)
            {
                nextThinkTime = now + thinkIntervalSeconds;
                _ = ThinkTickAsync();
            }
        }

        public void StartClock()
        {
            running = true;

            float now = Time.time;
            nextInputPollTime = now + inputPollIntervalSeconds;
            nextThinkTime = now + thinkIntervalSeconds;

            if (verboseLog)
            {
                Debug.Log("[AutonomousClock] StartClock");
            }
        }

        public void StopClock()
        {
            running = false;

            if (verboseLog)
            {
                Debug.Log("[AutonomousClock] StopClock");
            }
        }

        public void TickNow()
        {
            EmitInputPollPulseUtc();
            ProcessExternalInputTick();
            _ = ThinkTickAsync();
        }

        private void EmitInputPollPulseUtc()
        {
            try
            {
                OnInputPollPulseUtc?.Invoke(DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[AutonomousClock] OnInputPollPulseUtc exception: " +
                    ex
                );
            }
        }

        private void ProcessExternalInputTick()
        {
            if (externalInputBuffer == null)
                return;

            if (!externalInputBuffer.TryConsume(out var inputEvent))
                return;

            if (verboseLog)
            {
                Debug.Log(
                    "[AutonomousClock] external input tick: " +
                    inputEvent.Type
                );
            }

            SuppressThinkAfterInput(inputEvent);

            if (runtimeProcessor != null)
            {
                runtimeProcessor.Process(inputEvent);
            }
            else
            {
                Debug.LogWarning(
                    "[AutonomousClock] RuntimeProcessor is not assigned. " +
                    "External input was dropped: " +
                    inputEvent.Type
                );
            }
        }

        private void SuppressThinkAfterInput(ExternalInputEvent inputEvent)
        {
            if (inputEvent == null)
                return;

            float seconds = suppressThinkAfterExternalInputSeconds;
            string reason = "after " + inputEvent.Type;

            if (inputEvent.Type == ExternalInputType.UserSpeech)
            {
                seconds = suppressThinkAfterUserSpeechSeconds;
            }
            else if (inputEvent.Type == ExternalInputType.FaceEvent)
            {
                seconds = GetFaceEventSuppressSeconds(inputEvent.Payload);
                reason = "after FaceEvent " + (inputEvent.Payload ?? string.Empty);
            }
            else if (inputEvent.Type == ExternalInputType.IdleTick)
            {
                // IdleTick は自分自身が発行する拍動Eventなので、
                // ここでThink抑制をかけない。
                seconds = 0f;
                reason = "after IdleTick";
            }

            SuppressThinkForSeconds(
                seconds,
                reason
            );
        }

        private float GetFaceEventSuppressSeconds(string payload)
        {
            switch (payload)
            {
                case "StableFound":
                    return suppressThinkAfterFaceStableFoundSeconds;

                case "TemporaryLost":
                    return suppressThinkAfterFaceTemporaryLostSeconds;

                case "FullyLost":
                    return suppressThinkAfterFaceFullyLostSeconds;

                default:
                    return 0.5f;
            }
        }

        public void SuppressThinkForSeconds(float seconds, string reason = "")
        {
            if (seconds <= 0f)
                return;

            float until = Time.time + seconds;

            if (until > suppressThinkUntilTime)
            {
                suppressThinkUntilTime = until;
            }

            if (verboseLog)
            {
                Debug.Log(
                    "[AutonomousClock] Suppress autonomous think for " +
                    seconds.ToString("F1") +
                    "s" +
                    (string.IsNullOrEmpty(reason) ? "" : " reason=" + reason)
                );
            }
        }

        private async Task ThinkTickAsync()
        {
            if (isThinking)
                return;

            if (!CanThinkNow())
                return;

            try
            {
                isThinking = true;

                if (verboseLog)
                {
                    Debug.Log(
                        emitIdleTickEvent
                            ? "[AutonomousClock] IdleTick schedule"
                            : "[AutonomousClock] Autonomous Think Tick"
                    );
                }

                if (emitIdleTickEvent)
                {
                    EmitIdleTickEvent();
                }
                else
                {
                    await RunAutonomousThinkAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[AutonomousClock] ThinkTickAsync Exception: " +
                    ex
                );
            }
            finally
            {
                isThinking = false;
            }
        }

        private void EmitIdleTickEvent()
        {
            if (externalInputBuffer == null)
            {
                Debug.LogWarning(
                    "[AutonomousClock] ExternalInputBuffer is not assigned. IdleTick was not emitted."
                );
                return;
            }

            externalInputBuffer.Push(
                ExternalInputType.IdleTick,
                idleTickPriority,
                idleTickPayload
            );

            if (verboseLog)
            {
                Debug.Log(
                    "[AutonomousClock] Emit IdleTick event priority=" +
                    idleTickPriority +
                    " payload=" +
                    idleTickPayload
                );
            }
        }

        private bool CanThinkNow()
        {
            if (Time.time < suppressThinkUntilTime)
            {
                if (verboseLog)
                {
                    Debug.Log(
                        "[AutonomousClock] CanThinkNow=false suppressed until " +
                        suppressThinkUntilTime.ToString("F2")
                    );
                }

                return false;
            }

            if (!CanThinkByInteractionState())
                return false;

            if (!CanThinkByLimboPermission())
                return false;

            if (!CanThinkByExecutionResource())
                return false;

            return true;
        }

        private bool CanThinkByInteractionState()
        {
            if (stateController == null)
                return true;

            InteractionState state = stateController.CurrentState;

            if (state == InteractionState.Booting ||
                state == InteractionState.Thinking ||
                state == InteractionState.Speaking ||
                state == InteractionState.Listening ||
                state == InteractionState.Acting ||
                state == InteractionState.Recovering ||
                state == InteractionState.Emergency)
            {
                if (verboseLog)
                {
                    Debug.Log(
                        "[AutonomousClock] CanThinkNow=false state=" +
                        state
                    );
                }

                return false;
            }

            return true;
        }

        private bool CanThinkByLimboPermission()
        {
            if (limboPermission == null)
                return true;

            if (limboPermission.IsEmergencyMode)
            {
                if (verboseLog)
                {
                    Debug.Log(
                        "[AutonomousClock] CanThinkNow=false Limbo Emergency"
                    );
                }

                return false;
            }

            if (!limboPermission.CanThink)
            {
                if (verboseLog)
                {
                    Debug.Log(
                        "[AutonomousClock] CanThinkNow=false Limbo CanThink=false"
                    );
                }

                return false;
            }

            return true;
        }

        private bool CanThinkByExecutionResource()
        {
            if (executionResourceManager == null)
                return true;

            bool canRunAutonomousThink =
                executionResourceManager.CanRunAutonomousThink();

            if (!canRunAutonomousThink)
            {
                if (verboseLog)
                {
                    Debug.Log(
                        "[AutonomousClock] CanThinkNow=false ExecutionResourceManager CanRunAutonomousThink=false"
                    );
                }

                return false;
            }

            return true;
        }

        private async Task RunAutonomousThinkAsync()
        {
            await Task.Yield();

            if (verboseLog)
            {
                Debug.Log(
                    "[AutonomousClock] RunAutonomousThinkAsync"
                );
            }
        }
    }
}
