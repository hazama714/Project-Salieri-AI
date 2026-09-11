// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System.Collections;
using UnityEngine;

public class PermissionBootCheck : BootCheckBase
{
    [Header("Permissions")]
    [SerializeField] private bool checkCamera = true;
    [SerializeField] private bool checkMicrophone = true;

    protected override IEnumerator RunCheck()
    {
        if (checkCamera && !Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            Fail("Camera permission is not granted.");
            yield break;
        }

        if (checkMicrophone && !Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            Fail("Microphone permission is not granted.");
            yield break;
        }

        Pass($"Permissions OK. Camera:{checkCamera} Microphone:{checkMicrophone}");
        yield return null;
    }
}
