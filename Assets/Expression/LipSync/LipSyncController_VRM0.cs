// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

// Version: SALIERI-LipSyncController_VRM0 v1.4.1 (AIUEO Random + double-stop guard)
// Timestamp (JST): 2025-10-15 09:36
// Comment:
// - 口形をAIUEOランダム切替で駆動（軽量・実機向け）。音量同期は未使用。
// - 二重停止対策：isSpeakingフラグで StopLipSync の多重呼び出しを無視。コルーチン終了側でも適切にフラグを落とす。
// - 競合回避：口形/ブリンクのみリセット。感情5種（Joy/Angry/Sorrow/Fun/Neutral）は保持。
// - ILipSyncController準拠（StartBlinking/StopBlinking 実装）。TTS連携用に StopLipSync() を提供。
// - 互換用：OnLipSyncComplete() とイベント LipSyncCompleted を提供。
// - 既存ログ文言は維持（解析互換）。

using System.Collections;
using UnityEngine;
using VRM;

[DisallowMultipleComponent]
public class LipSyncController_VRM0 : MonoBehaviour, ILipSyncController
{
    [Header("References")]
    [SerializeField] private VRMBlendShapeProxy blendShapeProxy;
    [SerializeField] private AudioSource audioSource;

    [Header("Options")]
    [Tooltip("発声終了時に口形/ブリンクだけをリセットする")]
    [SerializeField] private bool autoResetAfterSpeak = true;

    [Tooltip("口形切替の最短間隔（秒）")]
    [SerializeField] private float visemeChangeIntervalMin = 0.05f;

    [Tooltip("口形切替の最長間隔（秒）")]
    [SerializeField] private float visemeChangeIntervalMax = 0.18f;

    // 状態管理（多重停止防止）
    private bool isSpeaking = false;

    // 任意購読用イベント（必要なら外部で +=）
    public event System.Action LipSyncCompleted;

    /// <summary>互換用：外部から直接呼ばれる完了通知。</summary>
    public void OnLipSyncComplete()
    {
        LipSyncCompleted?.Invoke();
    }

    // ===========================
    // 公開API（既存IFを維持）
    // ===========================

    /// <summary>感情を適用し、音声を再生し、口パク（AIUEOランダム）を開始。</summary>
    public void PlayLipSyncWithEmotion(string emotion, string text, float duration, AudioClip clip)
    {
        // 進行中なら一旦止める（状態整合）
        if (isSpeaking) { StopAllCoroutines(); }

        isSpeaking = true;

        // 1) 感情表情を先に適用
        SetEmotionExpression(emotion);

        // 2) 音声再生
        if (audioSource != null)
        {
            audioSource.clip = clip;
            if (clip != null) audioSource.Play();
        }

        // 3) 口パク開始（AIUEOランダム）
        StartCoroutine(LipSyncRoutine(duration));
    }

    /// <summary>感情表情を適用（EmotionExtractorの5タグ準拠）。</summary>
    public void SetEmotionExpression(string emotion)
    {
        if (blendShapeProxy == null)
        {
            Debug.LogWarning("[LipSync_VRM0] blendShapeProxy 未設定のため感情表情適用不可");
            return;
        }

        string em = string.IsNullOrEmpty(emotion) ? "Neutral" : emotion;

        // 口形＋ブリンクのみをリセット（感情は保持）
        ResetMouthAndBlinkOnly();

        // 感情プリセットON
        var preset = MapEmotionToPreset(em);
        var key = BlendShapeKey.CreateFromPreset(preset);
        blendShapeProxy.ImmediatelySetValue(key, 1.0f);
        blendShapeProxy.Apply();

        Debug.Log($"[LipSync_VRM0] 感情表情適用: {em} → {preset}");
    }

    // === ILipSyncController 準拠メソッド ===
    public void StartBlinking() { /* no-op（他システムに委譲）*/ }
    public void StopBlinking() { ResetBlinkOnly(); }

    /// <summary>TTS側から反射で呼ばれる停止API（多重呼び出し防止付き）。</summary>
    public void StopLipSync()
    {
        if (!isSpeaking) return; // 既に停止済みなら無視

        isSpeaking = false;
        StopAllCoroutines();
        ResetMouthAndBlinkOnly();
        Debug.Log("[LipSync_VRM0] StopLipSync() 呼び出し → 口形停止");
        OnLipSyncComplete(); // 互換通知
    }

    // ===========================
    // 内部処理
    // ===========================

    /// <summary>AIUEOランダムの簡易リップシンク。</summary>
    private IEnumerator LipSyncRoutine(float duration)
    {
        if (blendShapeProxy == null)
        {
            isSpeaking = false;
            yield break;
        }

        var visemes = new[]
        {
            BlendShapePreset.A,
            BlendShapePreset.I,
            BlendShapePreset.U,
            BlendShapePreset.E,
            BlendShapePreset.O
        };

        float elapsed = 0f;
        float nextChange = 0f;
        var current = BlendShapePreset.A;

        while (elapsed < duration)
        {
            // 外部停止（StopLipSync等）により isSpeaking=false なら中断
            if (!isSpeaking) yield break;

            elapsed += Time.deltaTime;
            nextChange -= Time.deltaTime;

            // 一定間隔ごとにランダム切替
            if (nextChange <= 0f)
            {
                current = visemes[Random.Range(0, visemes.Length)];
                nextChange = Random.Range(
                    Mathf.Max(0.01f, visemeChangeIntervalMin),
                    Mathf.Max(visemeChangeIntervalMin + 0.01f, visemeChangeIntervalMax)
                );
            }

            // 全口形を一旦0 → 現在の口形のみ1.0
            for (int i = 0; i < visemes.Length; i++)
            {
                var key = BlendShapeKey.CreateFromPreset(visemes[i]);
                float v = (visemes[i] == current) ? 1.0f : 0f;
                blendShapeProxy.ImmediatelySetValue(key, v);
            }
            blendShapeProxy.Apply();

            yield return null;
        }

        // 自然終了（TTS側からの停止と重複しないようガード）
        if (isSpeaking)
        {
            if (autoResetAfterSpeak)
            {
                ResetMouthAndBlinkOnly();
            }
            isSpeaking = false;
            OnLipSyncComplete(); // 互換通知
        }
    }

    /// <summary>口形(A/I/U/E/O)とブリンク系だけを0に戻す（感情5種は触らない）。</summary>
    private void ResetMouthAndBlinkOnly()
    {
        if (blendShapeProxy == null) return;

        var toReset = new[]
        {
            BlendShapePreset.A, BlendShapePreset.I, BlendShapePreset.U,
            BlendShapePreset.E, BlendShapePreset.O,
            BlendShapePreset.Blink, BlendShapePreset.Blink_L, BlendShapePreset.Blink_R
        };

        foreach (var p in toReset)
        {
            var k = BlendShapeKey.CreateFromPreset(p);
            blendShapeProxy.ImmediatelySetValue(k, 0f);
        }
        blendShapeProxy.Apply();

        // 既存ログ文言を維持（解析互換用）
        Debug.Log("[LipSync_VRM0] 全表情リセット完了");
    }

    /// <summary>ブリンク系のみ0に戻す（口形は維持）。</summary>
    private void ResetBlinkOnly()
    {
        if (blendShapeProxy == null) return;

        var blinkOnly = new[]
        {
            BlendShapePreset.Blink, BlendShapePreset.Blink_L, BlendShapePreset.Blink_R
        };

        foreach (var p in blinkOnly)
        {
            var k = BlendShapeKey.CreateFromPreset(p);
            blendShapeProxy.ImmediatelySetValue(k, 0f);
        }
        blendShapeProxy.Apply();
    }

    /// <summary>EmotionExtractor の5タグに厳密対応。Fun未搭載モデルはJoyへフォールバック。</summary>
    private BlendShapePreset MapEmotionToPreset(string emotion)
    {
        string lower = emotion.ToLower();

        if (lower.Contains("joy")) return BlendShapePreset.Joy;
        if (lower.Contains("angry")) return BlendShapePreset.Angry;
        if (lower.Contains("sorrow")) return BlendShapePreset.Sorrow;

        if (lower.Contains("fun"))
        {
            return HasPreset(BlendShapePreset.Fun) ? BlendShapePreset.Fun : BlendShapePreset.Joy;
        }

        if (lower.Contains("neutral")) return BlendShapePreset.Neutral;

        return BlendShapePreset.Neutral;
    }

    /// <summary>指定PresetがAvatarに存在するかの簡易チェック。</summary>
    private bool HasPreset(BlendShapePreset preset)
    {
        var avatar = blendShapeProxy != null ? blendShapeProxy.BlendShapeAvatar : null;
        if (avatar == null) return true;
        var clips = avatar.Clips;
        if (clips == null) return true;

        foreach (var clip in clips)
        {
            if (clip != null && clip.Preset == preset) return true;
        }
        return false;
    }
}
