// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using UnityEngine;
using UnityEngine.Android;

using SalieriAI.Core.Execution;
using SalieriAI.Core.Input;

namespace SalieriAI.Sensors.Audio
{
    public class AndroidSTTReceiver : MonoBehaviour
    {
        [Header("Speech Route")]
        [SerializeField]
        private UserSpeechRouter userSpeechRouter;

        [Header("Runtime")]
        [SerializeField]
        private AutonomousClock autonomousClock;

        [Header("Think Suppression")]
        [SerializeField]
        private float suppressOnStartListeningSeconds = 5f;

        [SerializeField]
        private float suppressOnReadySeconds = 5f;

        [SerializeField]
        private float suppressOnBeginSeconds = 5f;

        [SerializeField]
        private float suppressOnPartialSeconds = 4f;

        [SerializeField]
        private float suppressOnResultSeconds = 3f;

        [SerializeField]
        private float suppressOnErrorSeconds = 2f;

        public event Action<string> OnFinalResultReceived;
        public event Action<string> OnErrorReceived;
        public event Action OnListeningEnded;
        public event Action<STTStartResult> OnRecognizerStarted;

        private STTService _sttService;
        private bool isListeningRequested;
        private bool hasStarted;

        public bool IsListeningRequested => isListeningRequested;
        public STTProviderKind ProviderKind =>
            _sttService != null
                ? _sttService.ProviderKind
                : STTProviderKind.Unavailable;
        public STTStartResult LastStartResult { get; private set; }

        private void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                Permission.RequestUserPermission(Permission.Microphone);
            }
#endif

            hasStarted = true;
            InitializeService();
        }

        private void OnEnable()
        {
            if (hasStarted && _sttService == null)
            {
                InitializeService();
            }
        }

        private void OnDisable()
        {
            ShutdownService();
        }

        private void OnDestroy()
        {
            ShutdownService();
        }

        private void OnApplicationQuit()
        {
            ShutdownService();
        }

        private void Update()
        {
            _sttService?.UpdateLifecycle();
        }

        public STTStartResult StartListening()
        {
            Debug.Log("[STT] StartListening");

            SuppressThink(
                suppressOnStartListeningSeconds,
                "STT StartListening"
            );

            if (_sttService == null)
                InitializeService();

            STTStartResult result = _sttService != null
                ? _sttService.StartListening()
                : STTStartResult.Rejected(
                    STTProviderKind.Unavailable,
                    "No STT provider is available for this platform.");

            LastStartResult = result;
            isListeningRequested = result.Accepted;

            Debug.Log(
                "[STT] Start result. provider=" + result.ProviderKind +
                " disposition=" + result.Disposition +
                " recognizerStarted=" + result.RecognizerActuallyStarted);

            if (!result.Accepted)
                OnSttError(result.Error);

            return result;
        }

        public void StopListening()
        {
            Debug.Log("[STT] StopListening");

            isListeningRequested = false;
            _sttService?.StopListening();
        }

        public void OnSttReady(string msg)
        {
            Debug.Log("[STT] Ready");

            SuppressThink(
                suppressOnReadySeconds,
                "STT Ready"
            );
        }

        internal void OnSttRecognizerStarted(STTProviderKind providerKind)
        {
            STTStartResult result =
                STTStartResult.RecognizerStarted(providerKind);
            LastStartResult = result;
            isListeningRequested = true;

            Debug.Log(
                "[STT] Recognizer started. provider=" + providerKind);

            OnSttReady(string.Empty);
            OnRecognizerStarted?.Invoke(result);
        }

        public void OnSttBegin(string msg)
        {
            Debug.Log("[STT] Begin");

            SuppressThink(
                suppressOnBeginSeconds,
                "STT Begin"
            );
        }

        public void OnSttEnd(string msg)
        {
            Debug.Log("[STT] End");

            isListeningRequested = false;
            OnListeningEnded?.Invoke();
        }

        public void OnSttPartial(string text)
        {
            Debug.Log("[STT] Partial: " + text);

            SuppressThink(
                suppressOnPartialSeconds,
                "STT Partial"
            );
        }

        public void OnSttResult(string text)
        {
            Debug.Log("[STT] Result: " + text);

            isListeningRequested = false;

            SuppressThink(
                suppressOnResultSeconds,
                "STT Result"
            );

            if (userSpeechRouter == null)
            {
                Debug.LogWarning(
                    "[STT] Result dropped: UserSpeechRouter is not assigned. text=" +
                    text
                );

                OnFinalResultReceived?.Invoke(text);
                return;
            }

            UserInputSource inputSource =
                _sttService != null
                    ? _sttService.ResultSource
                    : UserInputSource.Unknown;

            userSpeechRouter.OnUserInputReceived(
                text,
                inputSource
            );

            OnFinalResultReceived?.Invoke(text);
        }

        public void OnSttError(string error)
        {
            Debug.LogWarning("[STT] Error: " + error);

            isListeningRequested = false;

            SuppressThink(
                suppressOnErrorSeconds,
                "STT Error"
            );

            OnErrorReceived?.Invoke(error);
        }

        private void SuppressThink(float seconds, string reason)
        {
            if (autonomousClock == null)
                return;

            autonomousClock.SuppressThinkForSeconds(
                seconds,
                reason
            );
        }

        private void InitializeService()
        {
            if (_sttService != null)
                return;

            _sttService = STTProviderResolver.CreateForCurrentPlatform();
            if (_sttService == null)
            {
                Debug.LogWarning(
                    "[STT] No provider is available for platform=" +
                    STTProviderResolver.CurrentPlatform);
                return;
            }

            _sttService.Initialize(this);

            Debug.Log(
                "[STT] Provider initialized. platform=" +
                STTProviderResolver.CurrentPlatform +
                " provider=" + _sttService.ProviderKind);
        }

        internal void SetServiceForTesting(STTService service)
        {
            ShutdownService();
            _sttService = service;
            _sttService?.Initialize(this);
        }

        private void ShutdownService()
        {
            isListeningRequested = false;

            STTService service = _sttService;
            _sttService = null;
            service?.Shutdown();
        }
    }
}
