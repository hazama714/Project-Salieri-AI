// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

public sealed class AudioMouthDriver : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private MonoBehaviour mouthControllerSource;

    [Header("Settings")]
    [SerializeField] private float gain = 20f;
    [SerializeField] private float smoothSpeed = 12f;
    [SerializeField] private float silenceThreshold = 0.01f;

    [Header("Debug")]
    [SerializeField] private bool editorTestMode = true;
    [SerializeField] private float testSpeed = 8f;
    [SerializeField] private float testStrength = 1f;

    private readonly float[] samples = new float[256];

    private IMouthController mouthController;
    private float currentOpen;

    private void Awake()
    {
        mouthController = mouthControllerSource as IMouthController;

        if (mouthController == null)
            Debug.LogWarning("[AudioMouthDriver] mouthControllerSource is not IMouthController");
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (editorTestMode)
        {
            float test =
                ((Mathf.Sin(Time.time * testSpeed) + 1f) * 0.5f)
                * testStrength;

            currentOpen = Mathf.Clamp01(test);
            mouthController?.SetMouthOpen(currentOpen);
            return;
        }
#endif

        if (audioSource == null || mouthController == null)
            return;

        if (!audioSource.isPlaying)
        {
            currentOpen = Mathf.Lerp(currentOpen, 0f, Time.deltaTime * smoothSpeed);
            mouthController.SetMouthOpen(currentOpen);
            return;
        }

        audioSource.GetOutputData(samples, 0);

        float sum = 0f;

        for (int i = 0; i < samples.Length; i++)
            sum += samples[i] * samples[i];

        float rms = Mathf.Sqrt(sum / samples.Length);

        float targetOpen = rms < silenceThreshold ? 0f : rms * gain;
        targetOpen = Mathf.Clamp01(targetOpen);

        currentOpen = Mathf.Lerp(
            currentOpen,
            targetOpen,
            Time.deltaTime * smoothSpeed
        );

        mouthController.SetMouthOpen(currentOpen);
    }

    private void OnDisable()
    {
        mouthController?.CloseMouth();
    }
}