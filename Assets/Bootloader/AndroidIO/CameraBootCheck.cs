// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System.Collections;
using UnityEngine;

public class CameraBootCheck : BootCheckBase
{
    [Header("Camera")]
    [SerializeField] private bool requirePermission = true;
    [SerializeField] private bool startTinyProbe = false;
    [SerializeField] private float probeSeconds = 0.5f;

    protected override IEnumerator RunCheck()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (requirePermission && !Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            Fail("Camera permission is not granted.");
            yield break;
        }
#endif

        WebCamDevice[] devices = WebCamTexture.devices;

        if (devices == null || devices.Length == 0)
        {
            Fail("No camera devices found.");
            yield break;
        }

        for (int i = 0; i < devices.Length; i++)
            Debug.Log($"[CameraBootCheck] Device {i}: {devices[i].name} Front:{devices[i].isFrontFacing}");

        if (!startTinyProbe)
        {
            Pass("Camera device count: " + devices.Length);
            yield break;
        }

        WebCamTexture tex = null;

        try
        {
            tex = new WebCamTexture(devices[0].name, 16, 16, 5);
            tex.Play();
        }
        catch (System.Exception ex)
        {
            Fail("Camera probe start failed: " + ex.Message);
            yield break;
        }

        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < probeSeconds)
            yield return null;

        bool ok = tex != null && tex.isPlaying;

        if (tex != null)
        {
            tex.Stop();
            Destroy(tex);
        }

        if (!ok)
        {
            Fail("Camera probe did not start.");
            yield break;
        }

        Pass("Camera probe OK. Device count: " + devices.Length);
    }
}
