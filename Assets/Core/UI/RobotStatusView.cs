// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;
using UnityEngine.UI;
using SalieriAI.Autonomy;
using SalieriAI.Core.State;

/// <summary>
/// Runtime / Execution の現在状態を画面へ表示する簡易ビュー。
///
/// 表示は説明文ではなく、InteractionStateController.CurrentState を
/// 端的に Idle / Listening / Acting / Speaking などで出す。
///
/// 発話表示については、VOICEVOX や ResponseBus に流れる元の発話本文を変更せず、
/// この画面へ表示する時だけ改行を空白へ置き換えて 1 行表示へ正規化する。
/// </summary>
public sealed class RobotStatusView : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Text statusText;
    [SerializeField] private Text speechText;

    [Header("Refs")]
    [SerializeField] private InteractionStateController interactionStateController;

    [Tooltip("旧表示や補助情報が必要な場合のために残す。Execution状態表示には基本使わない。")]
    [SerializeField] private SelfStateCollector selfStateCollector;

    [Header("Display")]
    [SerializeField] private string statusPrefix = "Execution : ";
    [SerializeField] private string speechPrefix = "発話 : ";
    [SerializeField] private string emptySpeechText = "";
    [SerializeField] private bool autoFindReferences = true;

    private string lastSpeech = "";

    private void Reset()
    {
        statusText = GetComponent<Text>();
        AutoFindReferences();
    }

    private void Awake()
    {
        if (autoFindReferences)
        {
            AutoFindReferences();
        }
    }

    private void OnEnable()
    {
        ResponseBus.OnResponse += OnSpeechReceived;
    }

    private void OnDisable()
    {
        ResponseBus.OnResponse -= OnSpeechReceived;
    }

    private void Update()
    {
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (statusText == null)
        {
            return;
        }

        statusText.text = statusPrefix + ResolveExecutionStateText();
    }

    private string ResolveExecutionStateText()
    {
        if (interactionStateController == null)
        {
            return "Unknown";
        }

        InteractionState state = interactionStateController.CurrentState;
        return state.ToString();
    }

    private void OnSpeechReceived(string text)
    {
        string normalizedText = NormalizeSpeechForDisplay(text);

        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return;
        }

        lastSpeech = normalizedText;

        if (speechText != null)
        {
            speechText.text = speechPrefix + lastSpeech;
        }
    }

    /// <summary>
    /// 発話表示欄へ出す文字列を 1 行へ正規化する。
    ///
    /// 対象:
    /// - 実際の改行コード: CRLF / LF / CR
    /// - 文字列として含まれる改行表現: \r\n / \n / \r
    ///
    /// 注意:
    /// この処理は画面表示専用。
    /// ResponseBus や VOICEVOX へ流れる元の発話本文は変更しない。
    /// </summary>
    private static string NormalizeSpeechForDisplay(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return text
            // 文字列として含まれている改行表現を先に処理する。
            .Replace("\\r\\n", " ")
            .Replace("\\n", " ")
            .Replace("\\r", " ")

            // 実際の改行コードを処理する。
            .Replace("\r\n", " ")
            .Replace("\n", " ")
            .Replace("\r", " ")
            .Trim();
    }

    public void ClearSpeech()
    {
        lastSpeech = "";

        if (speechText != null)
        {
            speechText.text = emptySpeechText;
        }
    }

    [ContextMenu("Auto Find References")]
    private void AutoFindReferences()
    {
        if (interactionStateController == null)
        {
#if UNITY_2023_1_OR_NEWER
            interactionStateController = FindFirstObjectByType<InteractionStateController>();
#else
            interactionStateController = FindObjectOfType<InteractionStateController>();
#endif
        }

        if (selfStateCollector == null)
        {
#if UNITY_2023_1_OR_NEWER
            selfStateCollector = FindFirstObjectByType<SelfStateCollector>();
#else
            selfStateCollector = FindObjectOfType<SelfStateCollector>();
#endif
        }
    }
}