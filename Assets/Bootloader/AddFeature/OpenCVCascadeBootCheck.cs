// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

public class OpenCVCascadeBootCheck : BootCheckBase
{
    public enum BasePath
    {
        PersistentDataPath,
        StreamingAssetsPath,
        AbsolutePath
    }

    [Header("OpenCV Cascade")]
    [SerializeField] private BasePath basePath = BasePath.PersistentDataPath;

    [SerializeField]
    private string relativeOrAbsolutePath =
        "opencvforunity/OpenCVForUnityExamples/objdetect/haarcascade_frontalface_alt.xml";

    [Header("Validation")]
    [SerializeField] private long minBytes = 1024;

    [Tooltip("ONにすると中身がOpenCV cascade XMLらしく見えない場合もBootを止めます。通常はOFF推奨。")]
    [SerializeField] private bool strictContentValidation = false;

    [SerializeField] private int headReadBytes = 4096;

    protected override IEnumerator RunCheck()
    {
        string path = ResolvePath(relativeOrAbsolutePath);

        if (string.IsNullOrWhiteSpace(path))
        {
            Fail("Cascade path is empty.");
            yield break;
        }

        if (!File.Exists(path))
        {
            Fail("Cascade file not found: " + path);
            yield break;
        }

        FileInfo info = null;
        string fileInfoError = null;

        try
        {
            info = new FileInfo(path);
        }
        catch (System.Exception ex)
        {
            fileInfoError = ex.Message;
        }

        if (!string.IsNullOrEmpty(fileInfoError) || info == null)
        {
            Fail("Cascade file info failed: " + fileInfoError + " path=" + path);
            yield break;
        }

        if (info.Length < minBytes)
        {
            Fail($"Cascade file too small: {info.Length} bytes / {path}");
            yield break;
        }

        string head = string.Empty;
        string contentReadError = null;

        try
        {
            head = ReadHeadAsText(path, headReadBytes);
        }
        catch (System.Exception ex)
        {
            contentReadError = ex.Message;
        }

        if (!string.IsNullOrEmpty(contentReadError))
        {
            // ファイルの存在・サイズはOKなので、読み取り内容チェックだけでBootを止めすぎない。
            if (strictContentValidation)
            {
                Fail("Cascade content read failed: " + contentReadError + " path=" + path);
                yield break;
            }

            Debug.LogWarning(
                "[OpenCVCascadeBootCheck] Cascade content read warning. " +
                "Boot continues because file exists and size is valid. " +
                "error=" + contentReadError + " path=" + path
            );

            Pass($"OpenCV cascade file exists. content-read-warning size={info.Length} path={path}");
            yield return null;
            yield break;
        }

        // OpenCV cascade XMLは配布元・変換状態によって先頭表現が多少違う可能性があるため、
        // ここはBoot停止条件にしすぎない。
        bool contentLooksLikeCascade =
            head.Contains("opencv_storage") ||
            head.Contains("<cascade") ||
            head.Contains("haarcascade") ||
            head.Contains("features") ||
            head.Contains("stages");

        if (!contentLooksLikeCascade)
        {
            string message =
                $"Cascade file exists but content signature was not recognized. " +
                $"size={info.Length} path={path}";

            if (strictContentValidation)
            {
                Fail(message);
                yield break;
            }

            Debug.LogWarning("[OpenCVCascadeBootCheck] " + message + " Boot continues.");
            Pass("OpenCV cascade file exists. content-signature-warning " + message);
            yield return null;
            yield break;
        }

        Pass($"OpenCV cascade OK size={info.Length} path={path}");
        yield return null;
    }

    private string ResolvePath(string path)
    {
        if (basePath == BasePath.AbsolutePath)
            return (path ?? string.Empty).Replace('\\', '/');

        string root = basePath == BasePath.PersistentDataPath
            ? Application.persistentDataPath
            : Application.streamingAssetsPath;

        return Path.Combine(root, path ?? string.Empty).Replace('\\', '/');
    }

    private static string ReadHeadAsText(string path, int maxBytes)
    {
        int safeMaxBytes = Mathf.Clamp(maxBytes, 128, 64 * 1024);
        byte[] buffer = new byte[safeMaxBytes];

        using (FileStream fs = File.OpenRead(path))
        {
            int read = fs.Read(buffer, 0, buffer.Length);
            if (read <= 0)
                return string.Empty;

            // UTF-8として読めない文字があっても置換して読む。
            return Encoding.UTF8.GetString(buffer, 0, read);
        }
    }
}
