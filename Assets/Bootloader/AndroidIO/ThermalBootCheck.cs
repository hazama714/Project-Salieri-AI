// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;

public class ThermalBootCheck : BootCheckBase
{
    [Header("Thermal")]
    [SerializeField] private float warningCelsius = 45f;
    [SerializeField] private float criticalCelsius = 55f;
    [SerializeField] private string[] androidThermalPaths =
    {
        "/sys/class/thermal/thermal_zone0/temp",
        "/sys/class/thermal/thermal_zone1/temp",
        "/sys/class/thermal/thermal_zone2/temp",
        "/sys/class/thermal/thermal_zone3/temp"
    };

    protected override IEnumerator RunCheck()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        bool found = false;
        float maxTemp = float.MinValue;
        string foundPath = "";

        foreach (string path in androidThermalPaths)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                continue;

            string raw = File.ReadAllText(path).Trim();

            if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                continue;

            float celsius = value > 1000f ? value / 1000f : value;
            found = true;

            Debug.Log($"[ThermalBootCheck] {path} = {celsius:0.0} C");

            if (celsius > maxTemp)
            {
                maxTemp = celsius;
                foundPath = path;
            }
        }

        if (!found)
        {
            Fail("No readable thermal zone found.");
            yield break;
        }

        if (maxTemp >= criticalCelsius)
        {
            Fail($"Thermal critical: {maxTemp:0.0} C / {foundPath}");
            yield break;
        }

        if (maxTemp >= warningCelsius)
        {
            Fail($"Thermal warning: {maxTemp:0.0} C / {foundPath}");
            yield break;
        }

        Pass($"Thermal OK: {maxTemp:0.0} C / {foundPath}");
#else
        Pass("Skipped thermal check outside Android runtime.");
#endif
        yield return null;
    }
}
