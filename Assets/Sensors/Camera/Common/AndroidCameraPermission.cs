// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

public class AndroidCameraPermission : MonoBehaviour
{
    public bool IsGranted { get; private set; }

    private void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            Debug.Log("[AndroidCameraPermission][REQUEST]");
            Permission.RequestUserPermission(Permission.Camera);
        }
        else
        {
            IsGranted = true;
            Debug.Log("[AndroidCameraPermission][GRANTED_ALREADY]");
        }
#else
        IsGranted = true;
        Debug.Log("[AndroidCameraPermission][EDITOR]");
#endif
    }

    private void Update()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!IsGranted && Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            IsGranted = true;
            Debug.Log("[AndroidCameraPermission][GRANTED]");
        }
#endif
    }
}