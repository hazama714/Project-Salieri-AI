// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

public class AndroidBluetoothPermission : MonoBehaviour
{
    [Header("Target")]
    public AndroidBluetoothSender bluetoothSender;

    private const string BLUETOOTH_CONNECT =
        "android.permission.BLUETOOTH_CONNECT";

    private const string BLUETOOTH_SCAN =
        "android.permission.BLUETOOTH_SCAN";

    private void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        Debug.Log("[AndroidBluetoothPermission][START]");

        RequestBluetoothPermissions();
#else
        Debug.Log("[AndroidBluetoothPermission][EDITOR]");
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void RequestBluetoothPermissions()
    {
        bool hasConnect = Permission.HasUserAuthorizedPermission(BLUETOOTH_CONNECT);
        bool hasScan = Permission.HasUserAuthorizedPermission(BLUETOOTH_SCAN);

        Debug.Log($"[AndroidBluetoothPermission][CHECK] CONNECT:{hasConnect} SCAN:{hasScan}");

        if (hasConnect && hasScan)
        {
            Debug.Log("[AndroidBluetoothPermission][ALREADY_GRANTED]");
            return;
        }

        PermissionCallbacks callbacks = new PermissionCallbacks();

        callbacks.PermissionGranted += OnPermissionGranted;
        callbacks.PermissionDenied += OnPermissionDenied;
        callbacks.PermissionDeniedAndDontAskAgain += OnPermissionDeniedAndDontAskAgain;

        Debug.Log("[AndroidBluetoothPermission][REQUEST]");

        Permission.RequestUserPermissions(
            new string[]
            {
                BLUETOOTH_CONNECT,
                BLUETOOTH_SCAN
            },
            callbacks
        );
    }

    private void OnPermissionGranted(string permission)
    {
        Debug.Log($"[AndroidBluetoothPermission][GRANTED] {permission}");

        bool hasConnect = Permission.HasUserAuthorizedPermission(BLUETOOTH_CONNECT);
        bool hasScan = Permission.HasUserAuthorizedPermission(BLUETOOTH_SCAN);

        Debug.Log($"[AndroidBluetoothPermission][RECHECK] CONNECT:{hasConnect} SCAN:{hasScan}");

        if (hasConnect && hasScan)
        {
            ConnectBluetooth();
        }
    }

    private void OnPermissionDenied(string permission)
    {
        Debug.LogWarning($"[AndroidBluetoothPermission][DENIED] {permission}");
    }

    private void OnPermissionDeniedAndDontAskAgain(string permission)
    {
        Debug.LogError($"[AndroidBluetoothPermission][DENIED_DONT_ASK_AGAIN] {permission}");
    }

    private void ConnectBluetooth()
    {
        Debug.Log("[AndroidBluetoothPermission][CONNECT_CALL]");

        if (bluetoothSender == null)
        {
            Debug.LogError("[AndroidBluetoothPermission][ERROR] bluetoothSender is null");
            return;
        }

        bluetoothSender.Connect();
    }
#endif
}