// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

namespace SalieriAI.Runtime
{
    public class RuntimeConnectionSettings : MonoBehaviour
    {
        public enum AndroidBodyTransport
        {
            Bluetooth = 0,
            UsbSerial = 1
        }

        public enum RuntimeMode
        {
            WifiCloudOnly,
            WifiCloudAndLocal,
            WifiOffLocalOnly
        }

        [Header("AI Runtime")]
        public RuntimeMode runtimeMode =
            RuntimeMode.WifiCloudAndLocal;

        [Header("LLM")]
        public bool useCloudLLM = true;
        public bool useLocalLLM = true;

        [Header("Device Features")]
        public bool useBluetooth = true;
        public bool useServo = true;
        public bool useOpenCV = true;

        [Header("Android Body Connection")]
        public AndroidBodyTransport androidBodyTransport =
            AndroidBodyTransport.Bluetooth;

        [Header("Voice")]
        public bool useVoice = true;
        public bool useVoicevox = true;
        public bool useAndroidTTS = true;

        [Header("Input")]
        public bool useMicrophone = false;

        [Header("Avatar")]
        public bool useVRM = true;

        [Header("Debug")]
        public bool debugLog = true;

        public bool ShouldUseCloudNow()
        {
            if (!useCloudLLM)
                return false;

            if (runtimeMode == RuntimeMode.WifiOffLocalOnly)
                return false;

            bool online =
                Application.internetReachability
                != NetworkReachability.NotReachable;

            if (runtimeMode == RuntimeMode.WifiCloudOnly)
                return true;

            return online;
        }

        public bool ShouldUseLocalNow()
        {
            if (!useLocalLLM)
                return false;

            if (runtimeMode == RuntimeMode.WifiCloudOnly)
                return false;

            if (runtimeMode == RuntimeMode.WifiOffLocalOnly)
                return true;

            bool online =
                Application.internetReachability
                != NetworkReachability.NotReachable;

            return !online;
        }

        private void Start()
        {
            if (!debugLog)
                return;

            Debug.Log($"[RuntimeConnectionSettings] Mode: {runtimeMode}");

            Debug.Log($"[RuntimeConnectionSettings] CloudLLM: {useCloudLLM}");
            Debug.Log($"[RuntimeConnectionSettings] LocalLLM: {useLocalLLM}");

            Debug.Log($"[RuntimeConnectionSettings] Bluetooth: {useBluetooth}");
            Debug.Log($"[RuntimeConnectionSettings] Servo: {useServo}");
            Debug.Log($"[RuntimeConnectionSettings] OpenCV: {useOpenCV}");
            Debug.Log(
                $"[RuntimeConnectionSettings] AndroidBodyTransport: " +
                $"{androidBodyTransport}");

            Debug.Log($"[RuntimeConnectionSettings] Voice: {useVoice}");
            Debug.Log($"[RuntimeConnectionSettings] Voicevox: {useVoicevox}");
            Debug.Log($"[RuntimeConnectionSettings] AndroidTTS: {useAndroidTTS}");

            Debug.Log($"[RuntimeConnectionSettings] VRM: {useVRM}");
        }
    }
}
