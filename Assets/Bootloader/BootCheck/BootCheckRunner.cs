// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BootCheckRunner : MonoBehaviour
{
    [Header("Installer")]
    [SerializeField] private InitialAssetInstaller installer;

    [Header("Boot Checks")]
    [SerializeField] private bool runOnStart = true;
    [SerializeField] private List<BootCheckBase> checks = new List<BootCheckBase>();

    [Header("Wait")]
    [SerializeField] private float installerTimeoutSeconds = 600f;
    [SerializeField] private float pollIntervalSeconds = 0.25f;

    [Header("UI")]
    [SerializeField] private Text statusText;

    public bool IsRunning { get; private set; }
    public bool IsCompleted { get; private set; }
    public bool IsFailed { get; private set; }
    public string LastError { get; private set; }
    public int WarningCount { get; private set; }

    private Coroutine runCoroutine;

    private void Start()
    {
        if (runOnStart)
            StartRun();
    }

    public void StartRun()
    {
        if (runCoroutine != null)
        {
            Debug.LogWarning("[BootCheckRunner] StartRun ignored. Already running.");
            return;
        }

        runCoroutine = StartCoroutine(RunAll());
    }

    public IEnumerator RunAll()
    {
        IsRunning = true;
        IsCompleted = false;
        IsFailed = false;
        LastError = string.Empty;
        WarningCount = 0;

        Debug.Log("[BootCheckRunner] RunAll begin.");

        yield return WaitForInstaller();

        if (IsFailed)
        {
            FinishRunning();
            yield break;
        }

        if (checks == null || checks.Count == 0)
        {
            Debug.LogWarning("[BootCheckRunner] No boot checks configured.");
            IsCompleted = true;
            UpdateStatus("Boot Check Complete\nNo checks configured");
            FinishRunning();
            yield break;
        }

        for (int i = 0; i < checks.Count; i++)
        {
            BootCheckBase check = checks[i];

            if (check == null)
            {
                Debug.LogWarning($"[BootCheckRunner] Check slot {i} is null. Skipped.");
                continue;
            }

            UpdateStatus($"Boot Check...\n{check.DisplayName} ({i + 1}/{checks.Count})");

            yield return check.Run();

            if (!check.IsSuccess)
            {
                if (check.Required)
                {
                    IsFailed = true;
                    LastError = $"{check.DisplayName}: {check.Message}";
                    UpdateStatus("Boot Check Failed\n" + LastError);
                    Debug.LogError("[BootCheckRunner] " + LastError);
                    FinishRunning();
                    yield break;
                }

                WarningCount++;
                Debug.LogWarning(
                    $"[BootCheckRunner] Optional check warning: {check.DisplayName}: {check.Message}"
                );
            }
        }

        IsCompleted = true;

        string completeMessage = WarningCount > 0
            ? $"Boot Check Complete\nWarnings: {WarningCount}"
            : "Boot Check Complete";

        UpdateStatus(completeMessage);
        Debug.Log($"[BootCheckRunner] All boot checks completed. Warnings:{WarningCount}");

        FinishRunning();
    }

    private IEnumerator WaitForInstaller()
    {
        if (installer == null)
        {
            Debug.LogWarning("[BootCheckRunner] installer is null. Run checks without install wait.");
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < installerTimeoutSeconds)
        {
            if (installer.IsCompleted)
            {
                if (installer.WarningCount > 0)
                {
                    WarningCount += installer.WarningCount;
                    Debug.LogWarning(
                        $"[BootCheckRunner] Installer completed with warnings: {installer.WarningCount}"
                    );
                }

                yield break;
            }

            if (installer.IsFailed)
            {
                IsFailed = true;
                LastError = installer.LastError;
                UpdateStatus("Initial Install Failed\n" + LastError);
                Debug.LogError("[BootCheckRunner] Initial install failed: " + LastError);
                yield break;
            }

            elapsed += pollIntervalSeconds;
            yield return new WaitForSeconds(pollIntervalSeconds);
        }

        IsFailed = true;
        LastError = "Initial asset install timeout.";
        UpdateStatus("Initial Install Timeout");
        Debug.LogError("[BootCheckRunner] " + LastError);
    }

    private void FinishRunning()
    {
        IsRunning = false;
        runCoroutine = null;
    }

    private void UpdateStatus(string text)
    {
        if (statusText != null)
            statusText.text = text;
    }
}
