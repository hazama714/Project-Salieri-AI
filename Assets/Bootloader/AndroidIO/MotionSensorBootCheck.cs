// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System.Collections;
using UnityEngine;

public class MotionSensorBootCheck : BootCheckBase
{
    [Header("Motion Sensors")]
    [SerializeField] private bool checkAccelerometer = true;
    [SerializeField] private bool checkGyroscope = true;

    protected override IEnumerator RunCheck()
    {
        if (checkAccelerometer)
        {
            Vector3 a = Input.acceleration;
            Debug.Log("[MotionSensorBootCheck] Acceleration: " + a);
        }

        if (checkGyroscope && !SystemInfo.supportsGyroscope)
        {
            Fail("Gyroscope is not available.");
            yield break;
        }

        if (checkGyroscope)
        {
            Input.gyro.enabled = true;
            yield return null;
            Debug.Log("[MotionSensorBootCheck] Gyro attitude: " + Input.gyro.attitude);
        }

        Pass($"Motion sensor check OK. Gyro:{SystemInfo.supportsGyroscope}");
    }
}
