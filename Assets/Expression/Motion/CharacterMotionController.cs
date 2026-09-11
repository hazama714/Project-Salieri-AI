// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

// CharacterMotionController.cs
// バージョン: v1.1.0（未使用Animator制御削除）
// 更新日: 2025-06-12
// コメント: EmotionState / IsTalking パラメータ制御を廃止

using UnityEngine;

public class CharacterMotionController : MonoBehaviour
{
    private Animator animator;
    private AudioSource audioSource;

    void Awake()
    {
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
    }

    /// <summary>
    /// （現状使用されていない）TTS音声を受け取り自動再生する補助関数（現在は未使用）
    /// </summary>
    public void Speak(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("[CharacterMotionController] AudioClip が null です");
            return;
        }

        audioSource.clip = clip;
        audioSource.Play();
    }

    /// <summary>
    /// 発話開始（現在は未使用。Animatorから分離済）
    /// </summary>
    public void StartTalking() { }

    /// <summary>
    /// 発話終了（現在は未使用。Animatorから分離済）
    /// </summary>
    public void StopTalking() { }

    /// <summary>
    /// 感情の状態設定（Animator未使用のため現在はログのみ）
    /// </summary>
    public void SetEmotionState(string emotion)
    {
        Debug.Log($"[CharacterMotionController] Emotion '{emotion}'（Animatorは使用せず）");
    }
}
