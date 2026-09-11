// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System.Collections;
using UnityEngine;

public class LightSensorBootCheck : BootCheckBase
{
    protected override IEnumerator RunCheck()
    {
        if (!SystemInfo.supportsGyroscope)
        {
            Debug.Log("[LightSensorBootCheck] Gyro support is unrelated; Unity has no standard light sensor API.");
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject sensorManager = activity.Call<AndroidJavaObject>("getSystemService", "sensor"))
            using (AndroidJavaObject lightSensor = sensorManager.Call<AndroidJavaObject>("getDefaultSensor", 5))
            {
                if (lightSensor == null)
                {
                    Fail("Light sensor is not available on this device.");
                    yield break;
                }

                string name = lightSensor.Call<string>("getName");
                Pass("Light sensor available: " + name);
            }
        }
        catch (System.Exception ex)
        {
            Fail("Light sensor check failed: " + ex.Message);
        }
#else
        Pass("Skipped light sensor check outside Android runtime.");
#endif
        yield return null;
    }
}
