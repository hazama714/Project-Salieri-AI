// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class InitialAssetInstaller : MonoBehaviour
{
    public const string PrefKeyPrefix = "Salieri.InitialAsset.";

    [Serializable]
    public class InstallItem
    {
        public string id;

        [Header("Source")]
        public string streamingAssetsRelativePath;

        [Header("Destination")]
        public string destinationRelativePath;

        [Header("Validation")]
        public long minBytes = 1024;
        public string requiredExtension;

        [Header("Options")]
        public bool required = true;
        public bool overwrite = false;
    }

    [Header("Install Items")]
    public List<InstallItem> installItems = new List<InstallItem>();

    [Header("Options")]
    public bool runOnStart = true;

    [Range(0, 5)]
    public int maxRetry = 2;

    [Header("UI")]
    public Text progressText;

    public bool IsRunning { get; private set; }
    public bool IsCompleted { get; private set; }
    public bool IsFailed { get; private set; }
    public string LastError { get; private set; }
    public int WarningCount { get; private set; }

    private Coroutine installCoroutine;

    private void Start()
    {
        if (runOnStart)
            StartInstall();
    }

    public void StartInstall()
    {
        if (installCoroutine != null)
        {
            Debug.LogWarning("[InitialAssetInstaller] StartInstall ignored. Already running.");
            return;
        }

        installCoroutine = StartCoroutine(EnsureAllInstalled());
    }

    public IEnumerator EnsureAllInstalled()
    {
        IsRunning = true;
        IsCompleted = false;
        IsFailed = false;
        LastError = string.Empty;
        WarningCount = 0;

        if (installItems == null || installItems.Count == 0)
        {
            IsCompleted = true;
            UpdateStatus("Initial Setup Complete\nNo install items");
            Debug.Log("[InitialAssetInstaller] No install items configured.");
            FinishRunning();
            yield break;
        }

        for (int i = 0; i < installItems.Count; i++)
        {
            InstallItem item = installItems[i];

            if (item == null || string.IsNullOrEmpty(item.id))
            {
                Debug.LogWarning($"[InitialAssetInstaller] Install item {i} is null or has no id. Skipped.");
                continue;
            }

            UpdateStatus(
                $"Initial Setup...\n" +
                $"{item.id} ({i + 1}/{installItems.Count})"
            );

            bool success = false;
            string message = string.Empty;

            yield return EnsureInstalled(
                item,
                (ok, result) =>
                {
                    success = ok;
                    message = result;
                }
            );

            if (!success)
            {
                if (item.required)
                {
                    IsFailed = true;
                    LastError = $"Install failed: {item.id} / {message}";
                    Debug.LogError("[InitialAssetInstaller] " + LastError);
                    UpdateStatus("Initial Setup Failed\n" + item.id);
                    FinishRunning();
                    yield break;
                }

                WarningCount++;
                Debug.LogWarning(
                    $"[InitialAssetInstaller] Optional install failed: {item.id} / {message}"
                );
            }
        }

        IsCompleted = true;

        string completeMessage = WarningCount > 0
            ? $"Initial Assets Ready\nWarnings: {WarningCount}"
            : "Initial Assets Ready";

        UpdateStatus(completeMessage);

        Debug.Log($"[InitialAssetInstaller] All install items completed. Warnings:{WarningCount}");

        FinishRunning();
    }

    private IEnumerator EnsureInstalled(InstallItem item, Action<bool, string> onComplete)
    {
        if (item == null)
        {
            onComplete?.Invoke(false, "item is null");
            yield break;
        }

        if (string.IsNullOrEmpty(item.streamingAssetsRelativePath))
        {
            onComplete?.Invoke(false, "streamingAssetsRelativePath is empty");
            yield break;
        }

        if (string.IsNullOrEmpty(item.destinationRelativePath))
        {
            onComplete?.Invoke(false, "destinationRelativePath is empty");
            yield break;
        }

        string source = GetStreamingAssetsPath(item.streamingAssetsRelativePath);
        string dest = GetPersistentPath(item.destinationRelativePath);

        Debug.Log($"[InitialAssetInstaller] Install check: {item.id}");
        Debug.Log($"[InitialAssetInstaller] SA='{source}'");
        Debug.Log($"[InitialAssetInstaller] PD='{dest}'");

        if (!item.overwrite && ValidateFile(dest, item, out string okReason))
        {
            SaveInstalledPath(item, dest);

            Debug.Log(
                $"[InitialAssetInstaller] Already installed: {item.id} / {okReason}"
            );

            onComplete?.Invoke(true, okReason);
            yield break;
        }

        TryDeleteQuiet(dest);

        string lastReason = string.Empty;

        for (int attempt = 0; attempt <= maxRetry; attempt++)
        {
            Debug.Log($"[InitialAssetInstaller] Copy attempt {attempt + 1}/{maxRetry + 1}: {item.id}");

            yield return CopyFromStreamingAssets(item, source, dest);

            if (ValidateFile(dest, item, out string reason))
            {
                SaveInstalledPath(item, dest);

                Debug.Log(
                    $"[InitialAssetInstaller] Installed OK: {item.id} / {reason}"
                );

                onComplete?.Invoke(true, reason);
                yield break;
            }

            lastReason = reason;

            Debug.LogWarning(
                $"[InitialAssetInstaller] Validate failed: {item.id} / {reason}"
            );

            TryDeleteQuiet(dest);
        }

        onComplete?.Invoke(false, lastReason);
    }

    private IEnumerator CopyFromStreamingAssets(
        InstallItem item,
        string source,
        string dest
    )
    {
        string dir = Path.GetDirectoryName(dest);

        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        long totalBytes = 0;

        using (UnityWebRequest head = UnityWebRequest.Head(source))
        {
            yield return head.SendWebRequest();

            if (head.result == UnityWebRequest.Result.Success)
            {
                long.TryParse(
                    head.GetResponseHeader("Content-Length"),
                    out totalBytes
                );
            }
            else
            {
                Debug.LogWarning(
                    $"[InitialAssetInstaller] HEAD failed: {item.id} / {head.error}"
                );
            }
        }

        using (UnityWebRequest uwr = UnityWebRequest.Get(source))
        {
            DownloadHandlerFile handler = new DownloadHandlerFile(dest)
            {
                removeFileOnAbort = true
            };

            uwr.downloadHandler = handler;

            float start = Time.realtimeSinceStartup;

            UnityWebRequestAsyncOperation op = uwr.SendWebRequest();

            while (!op.isDone)
            {
                long downloaded = (long)uwr.downloadedBytes;

                float elapsed =
                    Mathf.Max(0.001f, Time.realtimeSinceStartup - start);

                double copiedMb = downloaded / 1024.0 / 1024.0;
                double totalMb = totalBytes / 1024.0 / 1024.0;
                double speed = copiedMb / elapsed;

                if (progressText != null)
                {
                    if (totalBytes > 0)
                    {
                        int pct = Mathf.Clamp(
                            Mathf.RoundToInt(
                                (float)(downloaded * 100.0 / totalBytes)
                            ),
                            0,
                            100
                        );

                        progressText.text =
                            $"Initial Setup...\n" +
                            $"{item.id}\n" +
                            $"{pct}%\n" +
                            $"{copiedMb:0} / {totalMb:0} MB\n" +
                            $"Speed: {speed:0.0} MB/s";
                    }
                    else
                    {
                        progressText.text =
                            $"Initial Setup...\n" +
                            $"{item.id}\n" +
                            $"{copiedMb:0} MB\n" +
                            $"Speed: {speed:0.0} MB/s";
                    }
                }

                yield return null;
            }

            if (uwr.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(
                    $"[InitialAssetInstaller] Copy failed: {item.id} / {uwr.error}"
                );
            }
        }
    }

    private bool ValidateFile(
        string path,
        InstallItem item,
        out string reason
    )
    {
        reason = string.Empty;

        if (string.IsNullOrEmpty(path))
        {
            reason = "path is empty";
            return false;
        }

        if (!File.Exists(path))
        {
            reason = "file not found";
            return false;
        }

        FileInfo info = new FileInfo(path);

        if (info.Length < item.minBytes)
        {
            reason = $"too small: {info.Length} bytes";
            return false;
        }

        if (!string.IsNullOrEmpty(item.requiredExtension))
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();

            string req = item.requiredExtension.ToLowerInvariant();

            if (!req.StartsWith("."))
                req = "." + req;

            if (ext != req)
            {
                reason = $"extension mismatch: {ext} != {req}";
                return false;
            }
        }

        reason = $"OK size={info.Length}";
        return true;
    }

    private void SaveInstalledPath(InstallItem item, string path)
    {
        PlayerPrefs.SetString(
            PrefKeyPrefix + item.id + ".Path",
            path
        );

        PlayerPrefs.SetString(
            PrefKeyPrefix + item.id + ".Time",
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        );

        PlayerPrefs.Save();
    }

    public static string GetInstalledPath(string id)
    {
        return PlayerPrefs.GetString(
            PrefKeyPrefix + id + ".Path",
            string.Empty
        );
    }

    private static string GetStreamingAssetsPath(string relative)
    {
        return Path.Combine(
            Application.streamingAssetsPath,
            relative
        ).Replace('\\', '/');
    }

    private static string GetPersistentPath(string relative)
    {
        return Path.Combine(
            Application.persistentDataPath,
            relative
        ).Replace('\\', '/');
    }

    private static void TryDeleteQuiet(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            Debug.LogWarning(
                "[InitialAssetInstaller] Delete failed: " + ex.Message
            );
        }
    }

    private void FinishRunning()
    {
        IsRunning = false;
        installCoroutine = null;
    }

    private void UpdateStatus(string text)
    {
        if (progressText != null)
            progressText.text = text;
    }
}
