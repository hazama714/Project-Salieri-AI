// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

// Version: M2-ONRESPONSE-1.0.0
// Timestamp: 2025-09-02T17:00:00+09:00 (JST)
// Comment: onResponse(string) の集中ハブ。どこからでも発火・購読できる静的イベント。既存構造は一切変更せず“追加のみ”。

using System;
using UnityEngine;

public static class UnifiedResponseEventHub
{
    /// <summary>
    /// 応答テキストを購読するための静的イベント。
    /// 例外時も空文字やエラーメッセージを流せば購読側で分岐可能。
    /// </summary>
    public static event Action<string> onResponse;

    /// <summary>
    /// 外部から応答を通知するための唯一のエントリ。
    /// null → string.Empty に正規化し、Invoke 前後でログを残す。
    /// </summary>
    public static void Raise(string text)
    {
        var payload = text ?? string.Empty;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[onResponse][Hub] raise: \"{(payload.Length > 80 ? payload.Substring(0, 80) + "…" : payload)}\"");
#endif
        try
        {
            onResponse?.Invoke(payload);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[onResponse][Hub] subscriber threw: {ex}");
        }
    }

    /// <summary>
    /// 登録（購読）用のユーティリティ。解除忘れの軽減目的。
    /// </summary>
    public static void Subscribe(Action<string> handler)
    {
        if (handler == null) return;
        onResponse += handler;
    }

    /// <summary>
    /// 解除（購読解除）用のユーティリティ。
    /// </summary>
    public static void Unsubscribe(Action<string> handler)
    {
        if (handler == null) return;
        onResponse -= handler;
    }
}
