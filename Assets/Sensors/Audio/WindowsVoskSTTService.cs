// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.IO;

using SalieriAI.Core.Input;

using UnityEngine;

namespace SalieriAI.Sensors.Audio
{
    /// <summary>
    /// Windows production STT provider. Unity microphone ownership and result
    /// publication stay on the main thread; Vosk model/recognizer work is
    /// owned by one bounded background worker in WindowsVoskBackend.
    /// </summary>
    public sealed class WindowsVoskSTTService : STTService
    {
        internal const int ShutdownDrainMilliseconds = 500;

        private readonly IWindowsVoskBackend injectedBackend;
        private IWindowsVoskBackend backend;
        private AndroidSTTReceiver receiver;
        private long activeGeneration = -1L;
        private long endingGeneration = -1L;
        private bool beginPublished;
        private bool finalPublished;
        private bool shutdown;

        public STTProviderKind ProviderKind => STTProviderKind.WindowsVosk;
        public UserInputSource ResultSource => UserInputSource.LocalSTT;

        public WindowsVoskSTTService()
            : this(null)
        {
        }

        internal WindowsVoskSTTService(IWindowsVoskBackend backendForTesting)
        {
            injectedBackend = backendForTesting;
        }

        public void Initialize(AndroidSTTReceiver targetReceiver)
        {
            if (shutdown || receiver != null || backend != null)
                return;

            receiver = targetReceiver;
            if (receiver == null)
                return;

            try
            {
                backend = injectedBackend ?? CreateRuntimeBackend();
                backend.Initialize();
            }
            catch (Exception ex)
            {
                receiver.OnSttError(
                    "WINDOWS_VOSK_INITIALIZE_FAILED: " +
                    ex.GetType().Name + ": " + ex.Message);
            }
        }

        public STTStartResult StartListening()
        {
            if (shutdown)
            {
                return STTStartResult.Rejected(
                    ProviderKind,
                    "Windows Vosk STT service is shut down.");
            }

            if (receiver == null || backend == null)
            {
                return STTStartResult.Rejected(
                    ProviderKind,
                    "Windows Vosk STT is unavailable.");
            }

            if (activeGeneration >= 0L && backend.IsRunning)
                return STTStartResult.AlreadyRunning(ProviderKind);

            if (activeGeneration >= 0L)
                return STTStartResult.ControlAccepted(ProviderKind);

            beginPublished = false;
            finalPublished = false;
            endingGeneration = -1L;
            if (!backend.TryRequestStart(out long generation, out string error))
            {
                return STTStartResult.Rejected(
                    ProviderKind,
                    string.IsNullOrWhiteSpace(error)
                        ? "Windows Vosk start was rejected."
                        : error);
            }

            activeGeneration = generation;
            return STTStartResult.ControlAccepted(ProviderKind);
        }

        public void UpdateLifecycle()
        {
            if (shutdown || backend == null)
                return;

            backend.UpdateMainThread();
            while (backend.TryDequeueEvent(out WindowsVoskBackendEvent item))
                PublishEventOnMainThread(item);
        }

        public void StopListening()
        {
            if (shutdown || backend == null)
                return;

            if (activeGeneration >= 0L)
                endingGeneration = activeGeneration;
            activeGeneration = -1L;
            backend.StopListening();
            UpdateLifecycle();
        }

        public void Shutdown()
        {
            if (shutdown)
                return;

            shutdown = true;
            activeGeneration = -1L;
            endingGeneration = -1L;
            if (backend != null)
            {
                bool drained = backend.Shutdown(
                    TimeSpan.FromMilliseconds(ShutdownDrainMilliseconds));
                if (!drained)
                {
                    Debug.LogWarning(
                        "[STT][WINDOWS_VOSK] Worker drain exceeded " +
                        ShutdownDrainMilliseconds + " ms; no unbounded wait was performed.");
                }
                backend.Dispose();
                backend = null;
            }
            receiver = null;
        }

        private void PublishEventOnMainThread(WindowsVoskBackendEvent item)
        {
            if (item == null || receiver == null)
                return;

            bool current = item.Generation == activeGeneration;
            bool ending = item.Generation == endingGeneration;
            switch (item.Kind)
            {
                case WindowsVoskBackendEventKind.RecognizerStarted:
                    if (current)
                        receiver.OnSttRecognizerStarted(ProviderKind);
                    break;

                case WindowsVoskBackendEventKind.Begin:
                    if (current && !beginPublished)
                    {
                        beginPublished = true;
                        receiver.OnSttBegin(string.Empty);
                    }
                    break;

                case WindowsVoskBackendEventKind.Partial:
                    if (current && !finalPublished &&
                        !string.IsNullOrWhiteSpace(item.Value))
                    {
                        receiver.OnSttPartial(item.Value);
                    }
                    break;

                case WindowsVoskBackendEventKind.Final:
                    if (current && !finalPublished &&
                        !string.IsNullOrWhiteSpace(item.Value))
                    {
                        if (!beginPublished)
                        {
                            beginPublished = true;
                            receiver.OnSttBegin(string.Empty);
                        }
                        finalPublished = true;
                        endingGeneration = activeGeneration;
                        receiver.OnSttResult(item.Value);
                    }
                    break;

                case WindowsVoskBackendEventKind.End:
                    if (current || ending)
                    {
                        receiver.OnSttEnd(string.Empty);
                        if (current)
                            activeGeneration = -1L;
                        endingGeneration = -1L;
                    }
                    break;

                case WindowsVoskBackendEventKind.Error:
                    if (current || ending || item.Generation < 0L)
                    {
                        receiver.OnSttError(
                            string.IsNullOrWhiteSpace(item.Value)
                                ? "WINDOWS_VOSK_ERROR"
                                : item.Value);
                        activeGeneration = -1L;
                        endingGeneration = -1L;
                    }
                    break;
            }
        }

        private static IWindowsVoskBackend CreateRuntimeBackend()
        {
            string manifestPath = Path.Combine(
                Application.streamingAssetsPath,
                "Vosk",
                "models",
                "vosk-model-small-ja-0.22.manifest.json");
            WindowsVoskModelManifest manifest =
                WindowsVoskModelManifest.Read(manifestPath);
            string archiveRelative = manifest.archiveAssetPath.Replace(
                '/',
                Path.DirectorySeparatorChar);
            string archivePath = Path.Combine(
                Application.streamingAssetsPath,
                archiveRelative);
            string installRoot = Path.Combine(
                Application.persistentDataPath,
                "Vosk",
                "models");

            return new WindowsVoskBackend(
                new WindowsVoskModelInstaller(
                    manifest,
                    archivePath,
                    installRoot),
                new WindowsVoskNativeApi(),
                new WindowsUnityMicrophoneSource(),
                WindowsVoskBackend.DefaultPcmCapacitySamples);
        }
    }
}
