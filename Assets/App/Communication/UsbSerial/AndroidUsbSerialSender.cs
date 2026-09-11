// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Runtime;

using UnityEngine;

namespace SalieriAI.App.Communication.UsbSerial
{
    /// <summary>
    /// Android-only Body command transport for a supported USB serial device.
    /// The current bridge accepts FTDI and CH34x drivers.
    /// Servo safety, mapping, ordering, and pacing remain upstream.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AndroidUsbSerialSender :
        MonoBehaviour,
        IBodyCommandSender,
        IConnectionStateProvider
    {
        private const string BridgeClassName =
            "com.studiohazama.salieri.usbserial.SalieriUsbSerialProbe";

        [SerializeField]
        private RuntimeConnectionSettings runtimeSettings;

        [Header("Connection")]
        [SerializeField]
        private bool autoConnectOnStart = true;

        [SerializeField]
        [Min(1f)]
        private float reconnectIntervalSeconds = 5f;

        private bool connected;
        private bool started;
        private bool applicationPaused;
        private bool applicationQuitting;
        private bool permissionDenied;
        private float nextReconnectTime;

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaClass bridgeClass;
        private AndroidJavaObject activity;
#endif

        public bool IsConnected => connected;
        public string LastEvent { get; private set; } = string.Empty;
        public string LastDetail { get; private set; } = string.Empty;

        public event Action<bool> ConnectionStateChanged;

        private void Awake()
        {
            if (runtimeSettings == null)
                runtimeSettings = FindObjectOfType<RuntimeConnectionSettings>();
        }

        private void Start()
        {
            started = true;
            if (autoConnectOnStart && IsUsbSerialSelected())
                Connect();
        }

        private void OnEnable()
        {
            if (started && !applicationPaused && !applicationQuitting &&
                autoConnectOnStart && IsUsbSerialSelected())
            {
                RequestReconnectNow();
            }
        }

        private void Update()
        {
            if (!autoConnectOnStart || connected || permissionDenied ||
                applicationPaused || applicationQuitting ||
                !IsUsbSerialSelected() ||
                Time.realtimeSinceStartup < nextReconnectTime)
            {
                return;
            }

            Connect();
        }

        public void SendServo(int servoId, int angle)
        {
            if (servoId < 0)
            {
                Debug.LogWarning(
                    "[AndroidUsbSerial][WRITE_SKIP] " +
                    $"Invalid ServoId={servoId}",
                    this);
                return;
            }

            SendPayload($"#{servoId} P{angle}\n");
        }

        public void SendRawCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                Debug.LogWarning(
                    "[AndroidUsbSerial][WRITE_SKIP] Empty raw command.",
                    this);
                return;
            }

            SendPayload(command.TrimEnd('\r', '\n') + "\n");
        }

        [ContextMenu("Connect Android USB Serial")]
        public void Connect()
        {
            if (!IsUsbSerialSelected() || applicationPaused ||
                applicationQuitting || !isActiveAndEnabled)
            {
                return;
            }

            nextReconnectTime = Time.realtimeSinceStartup +
                Mathf.Max(1f, reconnectIntervalSeconds);

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                EnsureBridge();
                bridgeClass.CallStatic(
                    "initializeAndProbe",
                    activity,
                    gameObject.name);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[AndroidUsbSerial][PROBE_ERROR] " +
                    exception.GetType().Name + ": " + exception.Message,
                    this);
                SetConnectionState(false);
            }
#endif
        }

        [ContextMenu("Reconnect Android USB Serial")]
        public void RequestReconnectNow()
        {
            permissionDenied = false;
            nextReconnectTime = 0f;
            Connect();
        }

        [ContextMenu("Disconnect Android USB Serial")]
        public void Disconnect()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (bridgeClass != null && activity != null)
                    bridgeClass.CallStatic("shutdown", activity);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[AndroidUsbSerial][SHUTDOWN_ERROR] " +
                    exception.GetType().Name + ": " + exception.Message,
                    this);
            }
#endif
            SetConnectionState(false);
        }

        /// <summary>
        /// Called by SalieriUsbSerialProbe through UnitySendMessage.
        /// Important state transitions are repeated into Unity's log so the
        /// existing LogToFile component persists them on Android.
        /// </summary>
        public void OnAndroidUsbSerialEvent(string payload)
        {
            ParseEvent(payload, out string eventName, out string detail);
            LastEvent = eventName;
            LastDetail = detail;

            switch (eventName)
            {
                case "SERIAL_DEVICE_FOUND":
                case "CH34X_FOUND":
                    permissionDenied = false;
                    break;
                case "CONNECTED":
                    permissionDenied = false;
                    SetConnectionState(true);
                    break;
                case "PERMISSION_DENIED":
                    permissionDenied = true;
                    SetConnectionState(false);
                    break;
                case "DETACHED":
                case "DISCONNECTED":
                case "OPEN_ERROR":
                case "WRITE_ERROR":
                case "PROBE_ERROR":
                case "UNSUPPORTED_DEVICE":
                    SetConnectionState(false);
                    break;
            }

            Debug.Log(
                $"[AndroidUsbSerial][{eventName}] {detail}",
                this);
        }

        private void SendPayload(string payload)
        {
            if (!IsUsbSerialSelected() || !connected)
            {
                Debug.LogWarning(
                    "[AndroidUsbSerial][WRITE_SKIP] Not connected.",
                    this);
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                EnsureBridge();
                bool written = bridgeClass.CallStatic<bool>("write", payload);
                if (!written)
                    SetConnectionState(false);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[AndroidUsbSerial][WRITE_ERROR] " +
                    exception.GetType().Name + ": " + exception.Message,
                    this);
                SetConnectionState(false);
            }
#endif
        }

        private bool IsUsbSerialSelected()
        {
            return runtimeSettings != null && runtimeSettings.useServo &&
                runtimeSettings.androidBodyTransport ==
                    RuntimeConnectionSettings.AndroidBodyTransport.UsbSerial;
        }

        private void SetConnectionState(bool value)
        {
            if (connected == value)
                return;

            connected = value;
            Delegate[] listeners =
                ConnectionStateChanged?.GetInvocationList();
            if (listeners == null)
                return;

            for (int i = 0; i < listeners.Length; i++)
            {
                try
                {
                    ((Action<bool>)listeners[i]).Invoke(connected);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        private static void ParseEvent(
            string payload,
            out string eventName,
            out string detail)
        {
            string value = payload ?? string.Empty;
            int separator = value.IndexOf('\n');
            eventName = separator >= 0
                ? value.Substring(0, separator)
                : value;
            detail = separator >= 0
                ? value.Substring(separator + 1)
                : string.Empty;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void EnsureBridge()
        {
            if (bridgeClass == null)
                bridgeClass = new AndroidJavaClass(BridgeClassName);

            if (activity == null)
            {
                using var unityPlayer = new AndroidJavaClass(
                    "com.unity3d.player.UnityPlayer");
                activity = unityPlayer.GetStatic<AndroidJavaObject>(
                    "currentActivity");
            }

            if (activity == null)
                throw new InvalidOperationException(
                    "UnityPlayer.currentActivity is null.");
        }
#endif

        private void OnDisable()
        {
            Disconnect();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            applicationPaused = pauseStatus;
            if (pauseStatus)
            {
                Disconnect();
            }
            else if (!applicationQuitting && IsUsbSerialSelected())
            {
                RequestReconnectNow();
            }
        }

        private void OnApplicationQuit()
        {
            applicationQuitting = true;
            Disconnect();
        }

        private void OnDestroy()
        {
            Disconnect();
#if UNITY_ANDROID && !UNITY_EDITOR
            activity?.Dispose();
            activity = null;
            bridgeClass?.Dispose();
            bridgeClass = null;
#endif
        }
    }
}
