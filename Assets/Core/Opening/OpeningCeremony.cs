// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using SalieriAI.Core.Perception.Attention;
using SalieriAI.Core.State;

namespace SalieriAI.Core.Opening
{
    /// <summary>
    /// Phase 10-F compatible opening sequence.
    ///
    /// 役割:
    /// - 起動直後に Runtime を Booting にする。
    /// - 起動準備中は StartupLockPanel を準備表示にする。
    /// - 首を安全に中央へ戻す。
    /// - Camera / OpenCV の warmup を待つ。
    /// - 準備完了後も Runtime は Booting のまま保持する。
    /// - StartupLockPanel の起動ボタン押下後に Runtime を Idle に切り替える。
    /// - StartupLockPanel を Ready にして、Start ボタンから SpeechInputController へ進ませる。
    ///
    /// 注意:
    /// - このクラスは STT を直接開始しない。
    /// - このクラスは ConversationService / LLM / VoicePlayback を直接呼ばない。
    /// - Permission / Limbo の詳細制御は InteractionStateController 側の状態変化に任せる。
    /// </summary>
    public class OpeningCeremony : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private InteractionStateController stateController;

        [SerializeField]
        private OrientationPriorityRequestService orientationPriorityRequestService;

        [Tooltip("StartupLockPanel / StartupLockPanelController が付いた Component。起動準備中/準備完了の表示切替に使う。")]
        [SerializeField]
        private MonoBehaviour startupLockPanel;

        [Header("Opening Settings")]
        [SerializeField]
        private bool runOnStart = true;

        [SerializeField]
        private bool setBootingOnAwake = true;

        [SerializeField]
        private bool showPreparingOnAwake = true;

        [SerializeField]
        private bool moveNeckToNeutral = true;

        [SerializeField]
        private bool showReadyWhenCompleted = true;

        [Header("Timings")]
        [SerializeField]
        [Min(0.0f)]
        private float bootWaitSeconds = 1.0f;

        [SerializeField]
        [Min(0.0f)]
        private float servoNeutralWaitSeconds = 1.0f;

        [SerializeField]
        [Min(0.0f)]
        private float cameraWarmupSeconds = 2.0f;

        [SerializeField]
        [Min(0.0f)]
        private float openCvWarmupSeconds = 2.0f;

        [Header("Debug")]
        [SerializeField]
        private bool enableDebugLog = true;

        private Coroutine openingRoutine;
        private bool completed;

        public bool IsCompleted => completed;

        /// <summary>
        /// StartupLockPanelController が起動完了を待つための互換イベント。
        /// OpeningSequence 完了時に1回だけ通知する。
        /// </summary>
        public event System.Action OnOpeningCompleted;

        private void Reset()
        {
            AutoFindReferences();
        }

        private void Awake()
        {
            AutoFindReferences();

            if (showPreparingOnAwake)
            {
                ShowStartupPreparing();
            }

            if (setBootingOnAwake)
            {
                SetRuntimeState(InteractionState.Booting, "Awake");
            }

            Log("[OpeningCeremony] Awake: Runtime State = " + GetRuntimeStateText());
        }

        private void Start()
        {
            if (!runOnStart)
            {
                Log("[OpeningCeremony] Start skipped. runOnStart=false");
                return;
            }

            StartOpening();
        }

        [ContextMenu("Start Opening")]
        public void StartOpening()
        {
            if (openingRoutine != null)
            {
                StopCoroutine(openingRoutine);
                openingRoutine = null;
            }

            completed = false;
            openingRoutine = StartCoroutine(CoOpeningSequence());
        }

        /// <summary>
        /// StartupLockPanelController 等から呼ばれても破綻しない互換入口。
        /// </summary>
        public void StartCeremony()
        {
            StartOpening();
        }

        /// <summary>
        /// StartupLockPanelController 等から呼ばれても破綻しない互換入口。
        /// </summary>
        public void BeginOpening()
        {
            StartOpening();
        }

        private IEnumerator CoOpeningSequence()
        {
            Log("[OpeningCeremony] START");
            Log("[OpeningCeremony] Runtime State = " + GetRuntimeStateText());

            if (bootWaitSeconds > 0.0f)
            {
                Log("[OpeningCeremony] Boot wait: " + bootWaitSeconds.ToString("0.00") + "s");
                yield return new WaitForSeconds(bootWaitSeconds);
            }

            if (moveNeckToNeutral)
            {
                Log("[OpeningCeremony] Request Virtual Body neutral");
                SubmitOpeningNeutralRequest();
            }

            if (servoNeutralWaitSeconds > 0.0f)
            {
                Log("[OpeningCeremony] Servo neutral wait: " + servoNeutralWaitSeconds.ToString("0.00") + "s");
                yield return new WaitForSeconds(servoNeutralWaitSeconds);
            }

            if (cameraWarmupSeconds > 0.0f)
            {
                Log("[OpeningCeremony] Camera warmup: " + cameraWarmupSeconds.ToString("0.00") + "s");
                yield return new WaitForSeconds(cameraWarmupSeconds);
            }

            if (openCvWarmupSeconds > 0.0f)
            {
                Log("[OpeningCeremony] OpenCV warmup: " + openCvWarmupSeconds.ToString("0.00") + "s");
                yield return new WaitForSeconds(openCvWarmupSeconds);
            }

            Log(
                "[OpeningCeremony] Runtime remains Booting until StartupLockPanel start. " +
                "Current State = " + GetRuntimeStateText()
            );

            if (showReadyWhenCompleted)
            {
                ShowStartupReady();
            }

            completed = true;
            openingRoutine = null;

            try
            {
                OnOpeningCompleted?.Invoke();
            }
            catch (Exception ex)
            {
                LogWarning("[OpeningCeremony] OnOpeningCompleted listener threw exception: " + ex.Message);
            }

            Log("[OpeningCeremony] COMPLETE");
        }

        private void OnDestroy()
        {
            if (openingRoutine != null)
            {
                StopCoroutine(openingRoutine);
                openingRoutine = null;
            }
        }

        private void AutoFindReferences()
        {
            if (stateController == null)
            {
#if UNITY_2023_1_OR_NEWER
                stateController = FindFirstObjectByType<InteractionStateController>();
#else
                stateController = FindObjectOfType<InteractionStateController>();
#endif
            }

            if (orientationPriorityRequestService == null)
            {
                orientationPriorityRequestService =
                    FindObjectOfType<OrientationPriorityRequestService>();
            }

            if (startupLockPanel == null)
            {
                startupLockPanel = FindMonoBehaviourByTypeName("StartupLockPanelController");

                if (startupLockPanel == null)
                {
                    startupLockPanel = FindMonoBehaviourByTypeName("StartupLockPanel");
                }
            }
        }

        private void SubmitOpeningNeutralRequest()
        {
            if (orientationPriorityRequestService == null)
            {
                LogWarning(
                    "[OpeningCeremony] Neutral request skipped: " +
                    "OrientationPriorityRequestService is not assigned.");
                return;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long durationMilliseconds = Math.Max(
                1L,
                (long)Math.Round(
                    Math.Max(0.05f, servoNeutralWaitSeconds) * 1000.0));

            var request = new OrientationPriorityRequest(
                Guid.NewGuid().ToString("N"),
                "opening-ceremony",
                OrientationPriorityDirective.Neutral,
                OrientationPriorityReason.Default,
                95,
                now,
                now + durationMilliseconds);

            if (!orientationPriorityRequestService.SubmitOrReplace(
                    request,
                    out string error))
            {
                LogWarning(
                    "[OpeningCeremony] Neutral request rejected: " + error);
            }
        }

        private void SetRuntimeState(InteractionState state, string reason)
        {
            if (stateController == null)
            {
                LogWarning("[OpeningCeremony] SetRuntimeState skipped: InteractionStateController is not assigned. state=" + state + " reason=" + reason);
                return;
            }

            stateController.SetState(state);
        }

        private string GetRuntimeStateText()
        {
            if (stateController == null)
            {
                return "<state-controller-null>";
            }

            return stateController.CurrentState.ToString();
        }

        private void ShowStartupPreparing()
        {
            bool invoked = InvokeFirstNoArg(
                startupLockPanel,
                "StartupLockPanel",
                "ShowPreparing",
                "SetPreparing",
                "Preparing"
            );

            if (!invoked)
            {
                LogWarning("[OpeningCeremony] StartupLockPanel preparing call skipped. StartupLockPanel is not assigned or has no preparing method.");
            }
        }

        private void ShowStartupReady()
        {
            bool invoked = InvokeFirstNoArg(
                startupLockPanel,
                "StartupLockPanel",
                "ShowReady",
                "SetReady",
                "Ready"
            );

            if (!invoked)
            {
                LogWarning("[OpeningCeremony] StartupLockPanel ready call skipped. StartupLockPanel is not assigned or has no ready method.");
            }
        }

        private bool InvokeFirstNoArg(MonoBehaviour target, string label, params string[] methodNames)
        {
            if (target == null)
            {
                LogWarning("[OpeningCeremony] " + label + " call skipped: target is not assigned.");
                return false;
            }

            System.Type type = target.GetType();

            for (int i = 0; i < methodNames.Length; i++)
            {
                string methodName = methodNames[i];
                MethodInfo method = type.GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    System.Type.EmptyTypes,
                    null
                );

                if (method == null)
                {
                    continue;
                }

                method.Invoke(target, null);
                return true;
            }

            LogWarning("[OpeningCeremony] " + label + " call skipped: no compatible method found on " + type.Name + ".");
            return false;
        }

        private static MonoBehaviour FindMonoBehaviourByTypeName(string typeName)
        {
#if UNITY_2023_1_OR_NEWER
            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
#else
            MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>();
#endif
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                {
                    continue;
                }

                if (behaviour.GetType().Name == typeName)
                {
                    return behaviour;
                }
            }

            return null;
        }

        private void Log(string message)
        {
            if (!enableDebugLog)
            {
                return;
            }

            Debug.Log(message);
        }

        private void LogWarning(string message)
        {
            if (!enableDebugLog)
            {
                return;
            }

            Debug.LogWarning(message);
        }
    }
}
