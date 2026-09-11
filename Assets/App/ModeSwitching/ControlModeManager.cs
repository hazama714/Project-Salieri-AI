// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

public enum ControlMode
{
    Normal,
    Maintenance
}

public class ControlModeManager : MonoBehaviour
{
    [Header("Current Mode")]
    public ControlMode currentMode = ControlMode.Maintenance;

    public bool IsNormalMode()
    {
        return currentMode == ControlMode.Normal;
    }

    public bool IsMaintenanceMode()
    {
        return currentMode == ControlMode.Maintenance;
    }

    public void SetNormalMode()
    {
        currentMode = ControlMode.Normal;
        Debug.Log("[ControlModeManager] Mode changed: Normal");
    }

    public void SetMaintenanceMode()
    {
        currentMode = ControlMode.Maintenance;
        Debug.Log("[ControlModeManager] Mode changed: Maintenance");
    }

    public void ToggleMode()
    {
        if (currentMode == ControlMode.Normal)
            SetMaintenanceMode();
        else
            SetNormalMode();
    }
}