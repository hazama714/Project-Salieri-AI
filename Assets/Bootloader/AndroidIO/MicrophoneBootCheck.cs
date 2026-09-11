// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System.Collections;
using UnityEngine;

public class MicrophoneBootCheck : BootCheckBase
{
    [Header("Microphone")]
    [SerializeField] private bool requirePermission = true;

    protected override IEnumerator RunCheck()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (requirePermission && !Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            Fail("Microphone permission is not granted.");
            yield break;
        }
#endif

        string[] devices = Microphone.devices;

        if (devices == null || devices.Length == 0)
        {
            Fail("No microphone devices found.");
            yield break;
        }

        for (int i = 0; i < devices.Length; i++)
            Debug.Log($"[MicrophoneBootCheck] Device {i}: {devices[i]}");

        Pass("Microphone device count: " + devices.Length);
        yield return null;
    }
}
