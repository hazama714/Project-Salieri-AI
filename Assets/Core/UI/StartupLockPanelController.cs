// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;
using UnityEngine.UI;
using TMPro;

using SalieriAI.Core.Opening;
using SalieriAI.Core.State;

/// <summary>
/// Phase 10-C:
/// 起動準備中に前面パネルを表示し、OpeningCeremony完了後に「起動」ボタンを有効化する。
///
/// Phase 10-C-5:
/// 起動ボタン押下後、必要であれば SpeechInputController へ音声待機開始を要求する。
///
/// 注意:
/// - このクラスは AndroidSTTReceiver を直接呼ばない。
/// - STT開始可否の判断は SpeechInputController に委譲する。
/// - Camera / Servo / LLM / Stop制御はまだ扱わない。
/// </summary>
public class StartupLockPanelController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private OpeningCeremonyManager openingCeremonyManager;

    [SerializeField]
    private InteractionStateController stateController;

    [Tooltip("起動ボタン押下後に音声待機を開始する場合に接続する。未設定でもパネルゲートとして動作する。")]
    [SerializeField] private SpeechInputController speechInputController;

    [Header("UI")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button startButton;
    [SerializeField] private Text statusText;

    [Header("Messages")]
    [SerializeField] private string preparingMessage = "起動準備中...";
    [SerializeField] private string readyMessage = "起動できます";
    [SerializeField] private string startedMessage = "起動しました";
    [SerializeField] private string startFailedMessage = "起動できません";
    [SerializeField] private string alreadyStartingMessage = "音声待機を準備中...";

    [Header("Behavior")]
    [SerializeField] private bool showPanelOnAwake = true;
    [SerializeField] private bool hidePanelOnStart = true;

    [Tooltip("ONの場合、起動ボタン押下時に SpeechInputController.RequestStartListening() を呼ぶ。")]
    [SerializeField] private bool requestSpeechInputOnStart = true;

    [Tooltip("SpeechInputControllerが未接続でも、パネルを閉じることを許可する。")]
    [SerializeField] private bool allowStartWithoutSpeechInputController = true;

    [Tooltip("SpeechInputControllerの開始要求が失敗した場合でも、パネルを閉じることを許可する。初期段階ではfalse推奨。")]
    [SerializeField] private bool allowPanelCloseWhenSpeechStartFailed = false;

    [SerializeField] private bool logEnabled = true;

    private bool isReady;
    private bool isStarted;

    public bool IsReady => isReady;
    public bool IsStarted => isStarted;

    private void Awake()
    {
        if (stateController == null)
        {
            stateController = FindObjectOfType<InteractionStateController>();
        }

        if (showPanelOnAwake)
        {
            ShowPreparing();
        }
        else
        {
            SetPanelVisible(false);
        }

        SetStartButtonInteractable(false);
    }

    private void OnEnable()
    {
        if (openingCeremonyManager != null)
        {
            openingCeremonyManager.OnOpeningCompleted += HandleOpeningCompleted;
        }

        if (startButton != null)
        {
            startButton.onClick.AddListener(HandleStartButtonClicked);
        }
    }

    private void OnDisable()
    {
        if (openingCeremonyManager != null)
        {
            openingCeremonyManager.OnOpeningCompleted -= HandleOpeningCompleted;
        }

        if (startButton != null)
        {
            startButton.onClick.RemoveListener(HandleStartButtonClicked);
        }
    }

    /// <summary>
    /// Inspectorや他ControllerからOpening完了を手動通知したい場合の入口。
    /// </summary>
    [ContextMenu("Debug Mark Ready")]
    public void MarkReady()
    {
        HandleOpeningCompleted();
    }

    /// <summary>
    /// OpeningCeremony から準備完了表示を要求するための入口。
    /// </summary>
    public void ShowReady()
    {
        HandleOpeningCompleted();
    }

    public void ShowPreparing()
    {
        isReady = false;
        isStarted = false;

        SetPanelVisible(true);
        SetStatus(preparingMessage);
        SetStartButtonInteractable(false);

        if (logEnabled)
        {
            Debug.Log("[StartupLockPanel] ShowPreparing");
        }
    }

    /// <summary>
    /// OpeningCeremony完了後に呼ばれる。
    /// ここでは運用開始はせず、起動ボタンを押せる状態にするだけ。
    /// </summary>
    private void HandleOpeningCompleted()
    {
        if (isStarted)
        {
            if (logEnabled)
            {
                Debug.Log("[StartupLockPanel] OpeningCompleted ignored. already started.");
            }
            return;
        }

        isReady = true;

        SetPanelVisible(true);
        SetStatus(readyMessage);
        SetStartButtonInteractable(true);

        if (logEnabled)
        {
            Debug.Log("[StartupLockPanel] Ready. Start button enabled.");
        }
    }

    /// <summary>
    /// 「起動」ボタン押下。
    /// Phase 10-C-5では、SpeechInputControllerへ音声待機開始要求を出せる。
    /// </summary>

    private bool TryEnterRuntimeIdle()
    {
        if (stateController == null)
        {
            stateController = FindObjectOfType<InteractionStateController>();
        }

        if (stateController == null)
        {
            if (logEnabled)
            {
                Debug.LogWarning(
                    "[StartupLockPanel] Runtime gate open failed. " +
                    "InteractionStateController is not assigned."
                );
            }

            return false;
        }

        stateController.SetState(InteractionState.Idle);

        if (logEnabled)
        {
            Debug.Log(
                "[StartupLockPanel] Runtime gate state changed: Booting -> Idle."
            );
        }

        return true;
    }

    private void RestoreBootingAfterFailedStart()
    {
        if (stateController == null)
        {
            return;
        }

        stateController.SetState(InteractionState.Booting);

        if (logEnabled)
        {
            Debug.LogWarning(
                "[StartupLockPanel] Runtime gate rolled back: Idle -> Booting."
            );
        }
    }

    private void HandleStartButtonClicked()
    {
        if (!isReady)
        {
            if (logEnabled)
            {
                Debug.LogWarning("[StartupLockPanel] Start ignored. not ready.");
            }
            return;
        }

        if (isStarted)
        {
            if (logEnabled)
            {
                Debug.Log("[StartupLockPanel] Start ignored. already started.");
            }
            return;
        }

        bool canOpenGate = TryEnterRuntimeIdle();

        if (canOpenGate && requestSpeechInputOnStart)
        {
            if (speechInputController == null)
            {
                if (allowStartWithoutSpeechInputController)
                {
                    if (logEnabled)
                    {
                        Debug.LogWarning(
                            "[StartupLockPanel] SpeechInputController is not assigned. " +
                            "Continue because allowStartWithoutSpeechInputController=true."
                        );
                    }
                }
                else
                {
                    canOpenGate = false;

                    if (logEnabled)
                    {
                        Debug.LogWarning(
                            "[StartupLockPanel] Start failed. SpeechInputController is not assigned."
                        );
                    }
                }
            }
            else
            {
                bool speechStarted =
                    speechInputController.RequestStartListening("startup-lock-panel");

                if (!speechStarted)
                {
                    bool alreadyStartingOrListening =
                        speechInputController.IsListeningOrStartRequested();

                    if (alreadyStartingOrListening)
                    {
                        SetStatus(alreadyStartingMessage);

                        if (logEnabled)
                        {
                            Debug.Log(
                                "[StartupLockPanel] SpeechInputController rejected start, " +
                                "but STT is already requested/listening. Treat as startup accepted."
                            );
                        }
                    }
                    else if (!allowPanelCloseWhenSpeechStartFailed)
                    {
                        canOpenGate = false;

                        if (logEnabled)
                        {
                            Debug.LogWarning(
                                "[StartupLockPanel] Start failed. SpeechInputController rejected start."
                            );
                        }
                    }
                }
            }
        }

        if (!canOpenGate)
        {
            RestoreBootingAfterFailedStart();

            SetStatus(startFailedMessage);
            SetStartButtonInteractable(true);
            return;
        }

        isStarted = true;
        SetStatus(startedMessage);
        SetStartButtonInteractable(false);

        if (logEnabled)
        {
            Debug.Log("[StartupLockPanel] Start clicked. runtime gate opened.");
        }

        if (hidePanelOnStart)
        {
            SetPanelVisible(false);
        }

        // Future:
        // RuntimeStartController.StartOperatingMode();
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(visible);
        }
        else if (logEnabled)
        {
            Debug.LogWarning("[StartupLockPanel] panelRoot is not assigned.");
        }
    }

    private void SetStartButtonInteractable(bool interactable)
    {
        if (startButton != null)
        {
            startButton.interactable = interactable;
        }
        else if (logEnabled)
        {
            Debug.LogWarning("[StartupLockPanel] startButton is not assigned.");
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        else if (logEnabled)
        {
            Debug.LogWarning("[StartupLockPanel] statusText is not assigned.");
        }
    }
}
