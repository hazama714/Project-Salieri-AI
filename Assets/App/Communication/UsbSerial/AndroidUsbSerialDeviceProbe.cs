// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using UnityEngine;

namespace SalieriAI.App.Communication.UsbSerial
{
    public enum AndroidUsbSerialProbeState
    {
        Unknown = 0,
        DeviceDisconnected = 1,
        DeviceFound = 2,
        PermissionPending = 3,
        PermissionGranted = 4,
        PermissionDenied = 5,
        Connected = 6,
        Error = 7
    }

    /// <summary>
    /// Phase 1 Android USB Host probe. It discovers USB devices, requests
    /// permission for a single attached device, and observes detach events.
    /// It intentionally does not select a serial driver or write commands.
    /// </summary>
    public sealed class AndroidUsbSerialDeviceProbe : MonoBehaviour
    {
        private const string BridgeClassName =
            "com.studiohazama.salieri.usbserial.SalieriUsbSerialProbe";

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaClass bridgeClass;
        private AndroidJavaObject activity;
#endif

        public AndroidUsbSerialProbeState State { get; private set; }
        public string LastEvent { get; private set; } = string.Empty;
        public string LastDetail { get; private set; } = string.Empty;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateRuntimeProbe()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (FindObjectOfType<AndroidUsbSerialSender>() != null)
                return;

            if (FindObjectOfType<AndroidUsbSerialDeviceProbe>() != null)
                return;

            var probeObject = new GameObject(
                "AndroidUsbSerialDeviceProbe");
            DontDestroyOnLoad(probeObject);
            probeObject.AddComponent<AndroidUsbSerialDeviceProbe>();
#endif
        }

        private void Start()
        {
            ProbeNow();
        }

        [ContextMenu("Probe Android USB Devices")]
        public void ProbeNow()
        {
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
                    exception.GetType().Name + ": " +
                    exception.Message,
                    this);
            }
#endif
        }

        /// <summary>
        /// Receives bounded discovery/permission/detach state from the Android
        /// bridge. The payload is event-name, newline, then diagnostic detail.
        /// </summary>
        public void OnAndroidUsbSerialEvent(string payload)
        {
            string value = payload ?? string.Empty;
            int separator = value.IndexOf('\n');
            string eventName = separator >= 0
                ? value.Substring(0, separator)
                : value;
            string detail = separator >= 0
                ? value.Substring(separator + 1)
                : string.Empty;

            LastEvent = eventName;
            LastDetail = detail;
            State = ToState(eventName);
        }

        private static AndroidUsbSerialProbeState ToState(string eventName)
        {
            switch (eventName)
            {
                case "DEVICE_FOUND":
                case "SERIAL_DEVICE_FOUND":
                    return AndroidUsbSerialProbeState.DeviceFound;
                case "PERMISSION_REQUEST":
                case "PERMISSION_PENDING":
                    return AndroidUsbSerialProbeState.PermissionPending;
                case "PERMISSION_GRANTED":
                    return AndroidUsbSerialProbeState.PermissionGranted;
                case "PERMISSION_DENIED":
                    return AndroidUsbSerialProbeState.PermissionDenied;
                case "CONNECTED":
                    return AndroidUsbSerialProbeState.Connected;
                case "DISCONNECTED":
                case "DETACHED":
                    return AndroidUsbSerialProbeState.DeviceDisconnected;
                case "PROBE_ERROR":
                case "SHUTDOWN_ERROR":
                    return AndroidUsbSerialProbeState.Error;
                default:
                    return AndroidUsbSerialProbeState.Unknown;
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void EnsureBridge()
        {
            if (bridgeClass == null)
            {
                bridgeClass = new AndroidJavaClass(
                    BridgeClassName);
            }

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

        private void OnDestroy()
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
                    exception.GetType().Name + ": " +
                    exception.Message,
                    this);
            }
            finally
            {
                activity?.Dispose();
                activity = null;
                bridgeClass?.Dispose();
                bridgeClass = null;
            }
#endif
        }
    }
}
