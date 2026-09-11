// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Windows Editor / Windows Standalone VOICEVOX synthesis provider.
///
/// This component owns only native initialization, WAV synthesis and native
/// shutdown. Audio playback, UI, lip sync and InteractionState are owned by
/// VoicePlaybackController and the existing Salieri runtime.
/// </summary>
[DisallowMultipleComponent]
public sealed class WindowsVoicevoxSynthesisProvider : MonoBehaviour
{
    private const string NativeLibraryName = "voicevox_unity_bridge";
    private const string OutputPathExceptionDataKey =
        "WindowsVoicevoxOutputPath";

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern int voicevox_unity_initialize(
        string openJtalkDictDir,
        string voiceModelPath
    );

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern int voicevox_unity_tts_to_file(
        string textUtf8,
        int styleId,
        string wavOutPath
    );

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void voicevox_unity_shutdown();

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int voicevox_unity_get_last_error(
        StringBuilder outBuf,
        int outCap
    );

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr voicevox_unity_get_core_version();
#endif

    [Header("VOICEVOX Assets")]
    [SerializeField]
    private string voiceVoxRootRelativePath = "VoiceVox";

    [SerializeField]
    private string openJtalkDicRelativePath =
        "open_jtalk_dic_utf_8-1.11";

    [SerializeField]
    private string modelFileName = "1.vvm";

    [Header("Debug")]
    [SerializeField]
    private bool logNativeVersionOnAwake = true;

    // VOICEVOX native state is process-global. A static gate prevents two
    // provider instances or overlapping requests from entering it together.
    private static readonly SemaphoreSlim NativeGate =
        new SemaphoreSlim(1, 1);

    private static readonly object NativeStateLock = new object();

    private static bool nativeInitialized;
    private static string initializedModelPath;
    private static Task activeNativeTask;

    private bool acceptsRequests;
    private bool lifecycleShutdownRequested;

    public bool IsSupported
    {
        get
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            return true;
#else
            return false;
#endif
        }
    }

    /// <summary>
    /// Retrieves the unique output path attached to a native synthesis error.
    /// This lets the request-generation owner remove a possible partial WAV
    /// without moving file ownership into the native provider.
    /// </summary>
    public static bool TryGetGeneratedOutputPath(
        Exception exception,
        out string outputPath
    )
    {
        outputPath = null;

        if (exception == null ||
            !exception.Data.Contains(OutputPathExceptionDataKey))
        {
            return false;
        }

        outputPath = exception.Data[OutputPathExceptionDataKey] as string;
        return !string.IsNullOrEmpty(outputPath);
    }

    private void Awake()
    {
        acceptsRequests = true;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if (!logNativeVersionOnAwake)
        {
            return;
        }

        try
        {
            IntPtr versionPointer = voicevox_unity_get_core_version();
            string version = versionPointer != IntPtr.Zero
                ? Marshal.PtrToStringAnsi(versionPointer)
                : "(null)";

            Debug.Log(
                "[WindowsVoicevoxSynthesisProvider] " +
                "VOICEVOX Core version: " + version
            );
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "[WindowsVoicevoxSynthesisProvider] " +
                "Native version check failed: " + exception.Message
            );
        }
#endif
    }

    private void OnEnable()
    {
        acceptsRequests = true;
        lifecycleShutdownRequested = false;
    }

    private void OnDisable()
    {
        RequestShutdownWithoutWaiting();
    }

    private void OnDestroy()
    {
        RequestShutdownWithoutWaiting();
    }

    private void OnApplicationQuit()
    {
        RequestShutdownWithoutWaiting();
    }

    /// <summary>
    /// Creates a uniquely named Windows WAV file.
    ///
    /// Unity paths and Inspector values are captured before Task.Run. The
    /// background task calls only the native bridge and System.IO APIs.
    /// Cancellation can stop a request while it waits for NativeGate. Once the
    /// native TTS call has begun, the native API has no cancellation entry
    /// point, so the call is allowed to finish and its result is handled by the
    /// request-generation owner in VoicePlaybackController.
    /// </summary>
    public async Task<string> SynthesizeToFileAsync(
        string text,
        int styleId,
        CancellationToken cancellationToken
    )
    {
#if !UNITY_EDITOR_WIN && !UNITY_STANDALONE_WIN
        throw new PlatformNotSupportedException(
            "Windows VOICEVOX synthesis is available only on Windows."
        );
#else
        if (!IsSupported)
        {
            throw new PlatformNotSupportedException(
                "Windows VOICEVOX synthesis is available only on Windows."
            );
        }

        if (!acceptsRequests || lifecycleShutdownRequested)
        {
            throw new InvalidOperationException(
                "Windows VOICEVOX provider is shutting down."
            );
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException(
                "Synthesis text is empty.",
                nameof(text)
            );
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Capture every Unity/Inspector-derived value on the calling thread.
        string streamingAssetsPath = Application.streamingAssetsPath;
        string temporaryCachePath = Application.temporaryCachePath;
        string rootRelativePath = voiceVoxRootRelativePath;
        string dictionaryRelativePath = openJtalkDicRelativePath;
        string selectedModelFileName = modelFileName;

        string dictionaryPath = Path.Combine(
            streamingAssetsPath,
            rootRelativePath,
            dictionaryRelativePath
        );

        string modelPath = Path.Combine(
            streamingAssetsPath,
            rootRelativePath,
            "models",
            selectedModelFileName
        );

        if (!Directory.Exists(dictionaryPath))
        {
            throw new DirectoryNotFoundException(
                "OpenJTalk dictionary not found: " + dictionaryPath
            );
        }

        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException(
                "VOICEVOX model not found.",
                modelPath
            );
        }

        string outputDirectory = Path.Combine(
            temporaryCachePath,
            "VoiceVox"
        );
        Directory.CreateDirectory(outputDirectory);

        string outputPath = Path.Combine(
            outputDirectory,
            "voicevox_" +
            DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") +
            "_" + Guid.NewGuid().ToString("N") +
            ".wav"
        );

        await NativeGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            Task<string> nativeTask = Task.Run(
                () => SynthesizeNative(
                    text,
                    styleId,
                    dictionaryPath,
                    modelPath,
                    outputPath
                )
            );

            lock (NativeStateLock)
            {
                activeNativeTask = nativeTask;
            }

            string resultPath;

            try
            {
                // Do not pass the token to Task.Run. Native synthesis cannot be
                // interrupted safely after entry and must run to completion.
                resultPath = await nativeTask.ConfigureAwait(false);
            }
            finally
            {
                lock (NativeStateLock)
                {
                    if (ReferenceEquals(activeNativeTask, nativeTask))
                    {
                        activeNativeTask = null;
                    }
                }
            }

            return resultPath;
        }
        finally
        {
            NativeGate.Release();
        }
#endif
    }

    private void RequestShutdownWithoutWaiting()
    {
        if (lifecycleShutdownRequested)
        {
            return;
        }

        lifecycleShutdownRequested = true;
        acceptsRequests = false;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        // Fire-and-observe is intentional: Unity lifecycle callbacks must not
        // synchronously wait. ShutdownWhenIdleAsync catches every exception.
        _ = ShutdownWhenIdleAsync();
#endif
    }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    private static string SynthesizeNative(
        string text,
        int styleId,
        string dictionaryPath,
        string modelPath,
        string outputPath
    )
    {
        try
        {
            EnsureInitializedNative(dictionaryPath, modelPath);

            int result = voicevox_unity_tts_to_file(
                text,
                styleId,
                outputPath
            );

            if (result != 0)
            {
                throw new InvalidOperationException(
                    "voicevox_unity_tts_to_file failed. " +
                    "result=" + result +
                    " error=" + GetLastNativeError()
                );
            }

            if (!File.Exists(outputPath))
            {
                throw new FileNotFoundException(
                    "VOICEVOX WAV was not created.",
                    outputPath
                );
            }

            if (new FileInfo(outputPath).Length <= 0)
            {
                throw new InvalidDataException(
                    "VOICEVOX WAV is empty: " + outputPath
                );
            }

            return outputPath;
        }
        catch (Exception exception)
        {
            exception.Data[OutputPathExceptionDataKey] = outputPath;
            throw;
        }
    }

    private static void EnsureInitializedNative(
        string dictionaryPath,
        string modelPath
    )
    {
        if (nativeInitialized &&
            string.Equals(
                initializedModelPath,
                modelPath,
                StringComparison.OrdinalIgnoreCase
            ))
        {
            return;
        }

        if (nativeInitialized)
        {
            voicevox_unity_shutdown();
            nativeInitialized = false;
            initializedModelPath = null;
        }

        int result = voicevox_unity_initialize(
            dictionaryPath,
            modelPath
        );

        if (result != 0)
        {
            throw new InvalidOperationException(
                "voicevox_unity_initialize failed. " +
                "result=" + result +
                " error=" + GetLastNativeError()
            );
        }

        nativeInitialized = true;
        initializedModelPath = modelPath;
    }

    private static async Task ShutdownWhenIdleAsync()
    {
        try
        {
            await NativeGate.WaitAsync().ConfigureAwait(false);

            try
            {
                // NativeGate guarantees that no initialize/TTS call is active.
                await Task.Run(ShutdownNative).ConfigureAwait(false);
            }
            finally
            {
                NativeGate.Release();
            }
        }
        catch (Exception)
        {
            // Lifecycle shutdown is best-effort. Do not surface an unobserved
            // Task exception during Scene unload or application shutdown.
        }
    }

    private static void ShutdownNative()
    {
        if (!nativeInitialized)
        {
            return;
        }

        voicevox_unity_shutdown();
        nativeInitialized = false;
        initializedModelPath = null;
    }

    private static string GetLastNativeError()
    {
        try
        {
            StringBuilder builder = new StringBuilder(2048);
            int length = voicevox_unity_get_last_error(
                builder,
                builder.Capacity
            );

            return length > 0
                ? builder.ToString()
                : string.Empty;
        }
        catch (Exception exception)
        {
            return "voicevox_unity_get_last_error failed: " +
                exception.Message;
        }
    }
#endif
}
