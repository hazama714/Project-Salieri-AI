// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System.Collections;
using System.IO;
using UnityEngine;

public class FileExistsBootCheck : BootCheckBase
{
    public enum BasePath
    {
        PersistentDataPath,
        StreamingAssetsPath,
        AbsolutePath
    }

    [Header("File")]
    [SerializeField] private BasePath basePath = BasePath.PersistentDataPath;
    [SerializeField] private string relativeOrAbsolutePath;
    [SerializeField] private long minBytes = 1024;
    [SerializeField] private string requiredExtension;

    protected override IEnumerator RunCheck()
    {
        string path = ResolvePath(relativeOrAbsolutePath);

        if (!File.Exists(path))
        {
            Fail("File not found: " + path);
            yield break;
        }

        FileInfo info = new FileInfo(path);

        if (info.Length < minBytes)
        {
            Fail($"File too small: {info.Length} bytes / {path}");
            yield break;
        }

        if (!string.IsNullOrEmpty(requiredExtension))
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            string req = requiredExtension.ToLowerInvariant();

            if (!req.StartsWith("."))
                req = "." + req;

            if (ext != req)
            {
                Fail($"Extension mismatch: {ext} != {req} / {path}");
                yield break;
            }
        }

        Pass($"OK size={info.Length} path={path}");
        yield return null;
    }

    private string ResolvePath(string path)
    {
        if (basePath == BasePath.AbsolutePath)
            return path.Replace('\\', '/');

        string root = basePath == BasePath.PersistentDataPath
            ? Application.persistentDataPath
            : Application.streamingAssetsPath;

        return Path.Combine(root, path).Replace('\\', '/');
    }
}
