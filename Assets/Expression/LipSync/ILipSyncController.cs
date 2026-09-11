// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

// 機能: VRM0/VRM1共通で口パク・表情連携を行うインターフェース
// バージョン: v1.0.0
// 更新日: 2025-06-09

public interface ILipSyncController
{
    /// <summary>
    /// テキストと感情に基づいてリップシンクと表情を再生
    /// </summary>
    void PlayLipSyncWithEmotion(string emotion, string text, float duration, UnityEngine.AudioClip clip = null);

    /// <summary>
    /// まばたき開始
    /// </summary>
    void StartBlinking();

    /// <summary>
    /// まばたき停止
    /// </summary>
    void StopBlinking();
}
