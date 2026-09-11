// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Threading;

using SalieriAI.Expression.Voice.Synthesis;

using UnityEngine;

public class VoicevoxAndroidBridge : MonoBehaviour,
    IAndroidVoicevoxSynthesisInvoker
{
    private const string PluginClassName =
        "com.hazama.voicevoxruntime.VoicevoxUnityBridge";

    private AndroidJavaClass bridgeClass;
    private int backgroundAttachCount;
    private int backgroundDetachCount;
    private int backgroundSynthesisCallCount;
    private int lastBackgroundJniThreadId;

    public bool IsBackgroundSynthesisReady => bridgeClass != null;
    public int BackgroundAttachCount => Volatile.Read(ref backgroundAttachCount);
    public int BackgroundDetachCount => Volatile.Read(ref backgroundDetachCount);
    public int BackgroundSynthesisCallCount =>
        Volatile.Read(ref backgroundSynthesisCallCount);
    public int LastBackgroundJniThreadId =>
        Volatile.Read(ref lastBackgroundJniThreadId);

    private void Awake()
    {
        Debug.Log("[VoicevoxAndroidBridge] Awake");
        Debug.Log($"[VoicevoxAndroidBridge] Application.platform:{Application.platform}");

#if UNITY_ANDROID
        Debug.Log("[VoicevoxAndroidBridge] UNITY_ANDROID ENABLED");
#else
        Debug.Log("[VoicevoxAndroidBridge] UNITY_ANDROID DISABLED");
#endif

#if UNITY_EDITOR
        Debug.Log("[VoicevoxAndroidBridge] UNITY_EDITOR ENABLED");
#else
        Debug.Log("[VoicevoxAndroidBridge] UNITY_EDITOR DISABLED");
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            Debug.Log("[VoicevoxAndroidBridge] Create AndroidJavaClass");

            bridgeClass = new AndroidJavaClass(PluginClassName);

            Debug.Log("[VoicevoxAndroidBridge] bridgeClass created");

            using (AndroidJavaClass unityPlayer =
                   new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                Debug.Log("[VoicevoxAndroidBridge] UnityPlayer class OK");

                AndroidJavaObject activity =
                    unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

                Debug.Log("[VoicevoxAndroidBridge] currentActivity OK");

                bridgeClass.CallStatic("setContext", activity);

                Debug.Log("[VoicevoxAndroidBridge] setContext OK");
            }

            bridgeClass.CallStatic("initialize");

            Debug.Log("[VoicevoxAndroidBridge] initialize OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[VoicevoxAndroidBridge] Initialize failed: {e}");
        }
#else
        Debug.Log("[VoicevoxAndroidBridge] Editor or non-Android mode");
#endif
    }

    private void Start()
    {
        Debug.Log("[VoicevoxAndroidBridge] Start");
    }

    /// <summary>
    /// 既存経路。内部でAndroid側に再生まで任せる。
    /// ここは既存互換のため残す。
    /// </summary>
    public void Speak(
        string text,
        string modelFileName,
        int styleId
    )
    {
        Debug.Log(
            $"[VoicevoxAndroidBridge] Speak called Text:{text} Model:{modelFileName} StyleId:{styleId}"
        );

        if (string.IsNullOrEmpty(text))
        {
            Debug.LogError("[VoicevoxAndroidBridge] text is empty");
            return;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        if (bridgeClass == null)
        {
            Debug.LogError("[VoicevoxAndroidBridge] bridgeClass is null");
            return;
        }

        try
        {
            bridgeClass.CallStatic(
                "speak",
                text,
                modelFileName,
                styleId
            );

            Debug.Log("[VoicevoxAndroidBridge] speak sent to Android");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[VoicevoxAndroidBridge] speak failed: {e}");
        }
#else
        Debug.Log("[VoicevoxAndroidBridge] Editor or non-Android speak skipped");
#endif
    }

    /// <summary>
    /// TTSManager連携用。
    /// TTSManagerは string path または byte[] wav を返すメソッドだけを採用するため、
    /// 既存のvoid Speak()とは別に、WAVファイルパスを返す入口を追加する。
    ///
    /// TTSManagerの候補名に合わせて SpeakToFile / SynthesizeToFile / SynthesizeToWavFile を用意する。
    /// </summary>
    public string SpeakToFile(string text, int styleId, string fileName)
    {
        return SynthesizeToFile(text, styleId, fileName);
    }

    public string SynthesizeToWavFile(string text, int styleId, string fileName)
    {
        return SynthesizeToFile(text, styleId, fileName);
    }

    public string TextToWavFile(string text, int styleId, string fileName)
    {
        return SynthesizeToFile(text, styleId, fileName);
    }

    public string GenerateWavFile(string text, int styleId, string fileName)
    {
        return SynthesizeToFile(text, styleId, fileName);
    }

    /// <summary>
    /// TTSManager本命入口。
    /// Android側に「WAVを生成してファイルパスを返すstaticメソッド」が必要。
    /// 推奨Android側メソッド名:
    ///   synthesizeToFile(String text, int styleId, String fileName): String
    /// </summary>
    public string SynthesizeToFile(string text, int styleId, string fileName)
    {
        Debug.Log(
            $"[VoicevoxAndroidBridge] SynthesizeToFile called Text:{text} StyleId:{styleId} File:{fileName}"
        );

        if (string.IsNullOrEmpty(text))
        {
            Debug.LogError("[VoicevoxAndroidBridge] SynthesizeToFile text is empty");
            return null;
        }

        if (string.IsNullOrEmpty(fileName))
        {
            fileName = "tts_out.wav";
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        if (bridgeClass == null)
        {
            Debug.LogError("[VoicevoxAndroidBridge] bridgeClass is null");
            return null;
        }

        // Android側の候補メソッドを順番に試す。
        // 戻り値は必ず String path を期待する。
        string[] methodNames =
        {
            "synthesizeToFile",
            "synthesizeToWavFile",
            "speakToFile",
            "ttsToFile",
            "generateWavFile"
        };

        foreach (string methodName in methodNames)
        {
            // (text, styleId, fileName)
            try
            {
                string path = bridgeClass.CallStatic<string>(
                    methodName,
                    text,
                    styleId,
                    fileName
                );

                if (!string.IsNullOrEmpty(path))
                {
                    Debug.Log($"[VoicevoxAndroidBridge] {methodName}(text,styleId,fileName) => {path}");
                    return path;
                }

                Debug.LogWarning($"[VoicevoxAndroidBridge] {methodName}(text,styleId,fileName) returned empty");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[VoicevoxAndroidBridge] {methodName}(text,styleId,fileName) failed: {e.Message}");
            }

            // (text, fileName, styleId)
            try
            {
                string path = bridgeClass.CallStatic<string>(
                    methodName,
                    text,
                    fileName,
                    styleId
                );

                if (!string.IsNullOrEmpty(path))
                {
                    Debug.Log($"[VoicevoxAndroidBridge] {methodName}(text,fileName,styleId) => {path}");
                    return path;
                }

                Debug.LogWarning($"[VoicevoxAndroidBridge] {methodName}(text,fileName,styleId) returned empty");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[VoicevoxAndroidBridge] {methodName}(text,fileName,styleId) failed: {e.Message}");
            }
        }

        Debug.LogError("[VoicevoxAndroidBridge] No Android synthesize-to-file method succeeded. Android/Kotlin bridge needs path-return method.");
        return null;
#else
        Debug.Log("[VoicevoxAndroidBridge] Editor or non-Android SynthesizeToFile skipped");
        return null;
#endif
    }

    /// <summary>
    /// AR1-B Wave 7 worker-only JNI boundary. The caller supplies an immutable
    /// request snapshot. This method deliberately performs no logging,
    /// playback, Unity object lookup, resource deployment, or fallback method
    /// probing. One invocation maps to one canonical Kotlin synthesis call.
    /// </summary>
    public string SynthesizeToFileOnWorkerThread(
        string text,
        int styleId,
        string outputFileName)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaClass capturedBridge = bridgeClass;
        if (capturedBridge == null)
            throw new InvalidOperationException("VOICEVOX Android bridge is not ready.");

        int attachResult = AndroidJNI.AttachCurrentThread();
        if (attachResult != 0)
        {
            throw new InvalidOperationException(
                "Android JNI worker attach failed: " + attachResult);
        }

        Interlocked.Increment(ref backgroundAttachCount);
        Volatile.Write(
            ref lastBackgroundJniThreadId,
            Thread.CurrentThread.ManagedThreadId);
        try
        {
            Interlocked.Increment(ref backgroundSynthesisCallCount);
            return capturedBridge.CallStatic<string>(
                "synthesizeToFile",
                text,
                styleId,
                outputFileName);
        }
        finally
        {
            AndroidJNI.DetachCurrentThread();
            Interlocked.Increment(ref backgroundDetachCount);
        }
#else
        throw new PlatformNotSupportedException(
            "Android VOICEVOX worker synthesis requires an Android player.");
#endif
    }
}
