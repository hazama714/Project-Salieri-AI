// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;
using SalieriAI.Core.Input;

namespace SalieriAI.Sensors.Audio
{
    public class AndroidSTTService : STTService
    {
        private enum Backend
        {
            Vosk,
            AndroidSpeechRecognizer
        }

        private const Backend ActiveBackend = Backend.Vosk;

        private const string BridgeClass =
            "com.studiohazama.salieri.stt.SalieriSpeechRecognizerBridge";

        private AndroidJavaClass _bridge;
        private bool _shutdown;

        public STTProviderKind ProviderKind => STTProviderKind.Android;

        public UserInputSource ResultSource =>
            ActiveBackend == Backend.Vosk
                ? UserInputSource.LocalSTT
                : UserInputSource.AndroidSTT;

        public void Initialize(AndroidSTTReceiver receiver)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (receiver == null)
            {
                Debug.LogError("[AndroidSTTService] Initialize failed: receiver is null.");
                return;
            }

            _bridge = new AndroidJavaClass(BridgeClass);
            _shutdown = false;

            string initializeMethod =
                ActiveBackend == Backend.Vosk
                    ? "init"
                    : "initAndroidSpeechRecognizer";

            _bridge.CallStatic(
                initializeMethod,
                receiver.gameObject.name
            );

            Debug.Log("[AndroidSTTService] Initialized");
#else
            Debug.Log("[AndroidSTTService] Editor mode");
#endif
        }

        public STTStartResult StartListening()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_shutdown)
            {
                return STTStartResult.Rejected(
                    ProviderKind,
                    "Android STT service is shut down.");
            }

            if (_bridge == null)
            {
                return STTStartResult.Rejected(
                    ProviderKind,
                    "Android STT bridge is not initialized.");
            }

            try
            {
                _bridge.CallStatic("startListening");

                // Android Vosk may still be loading its model. The Java owner
                // accepts and serializes the pending start, then reports the
                // actual microphone start through OnSttReady.
                return STTStartResult.ControlAccepted(ProviderKind);
            }
            catch (System.Exception ex)
            {
                return STTStartResult.Rejected(
                    ProviderKind,
                    ex.GetType().Name + ": " + ex.Message);
            }
#else
            return STTStartResult.Rejected(
                ProviderKind,
                "Android STT is unavailable on this platform.");
#endif
        }

        public void UpdateLifecycle()
        {
            // Android owns its asynchronous start lifecycle in the existing
            // Java/Vosk bridge and reports readiness through OnSttReady.
        }

        public void StopListening()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!_shutdown)
            {
                _bridge?.CallStatic("stopListening");
            }
#endif
        }

        public void Shutdown()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_shutdown)
                return;

            _shutdown = true;

            try
            {
                _bridge?.CallStatic("shutdown");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning(
                    "[AndroidSTTService] Shutdown failed: " +
                    ex.GetType().Name +
                    " " +
                    ex.Message
                );
            }
            finally
            {
                _bridge?.Dispose();
                _bridge = null;
            }
#endif
        }
    }
}
