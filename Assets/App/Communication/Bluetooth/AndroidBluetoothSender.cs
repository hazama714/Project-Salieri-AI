// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

// v1.4 / RuntimeConnectionSettings support

using UnityEngine;
using System;
using System.Text;
using System.Threading;
using SalieriAI.Runtime;
using SalieriAI.Core.Diagnostics.Performance;
using SalieriAI.App.Communication.Bluetooth;

public class AndroidBluetoothSender :
    MonoBehaviour,
    IBodyCommandSender,
    IConnectionStateProvider
{
    [Header("Runtime Settings")]
    [SerializeField] private RuntimeConnectionSettings runtimeSettings;

    [Header("Bluetooth Target")]
    public string deviceName = "JDY-31-SPP";
    public string sppUuid = "00001101-0000-1000-8000-00805F9B34FB";

    [Header("Connection")]
    public bool autoConnectOnStart = false;
    public bool autoReconnect = true;
    public float reconnectCooldownSeconds = 5.0f;

    [Header("Send Guard")]
    public float minSendInterval = 0.05f;

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject bluetoothSocket;
    private AndroidJavaObject outputStream;
#endif

    private AndroidBluetoothConnectionBackend connectionBackend;
    private BluetoothConnectionOperationRuntime connectionRuntime;
    private int unityMainThreadId;
    private long connectionOperationSequence;
    private long connectProbeStarted;

    private bool isConnected = false;
    private bool isConnecting = false;
    private bool applicationPaused = false;
    private bool applicationQuitting = false;

    public bool IsConnected => isConnected;

    public event Action<bool> ConnectionStateChanged;

    private bool hasPendingCommand = false;
    private int pendingServoId = 0;
    private int pendingAngle = 90;

    private bool hasPendingRawCommand = false;
    private string pendingRawCommand = string.Empty;

    private float lastSendTime = -999f;
    private float nextReconnectTime = 0f;

    void Awake()
    {
        unityMainThreadId = Thread.CurrentThread.ManagedThreadId;
        connectionBackend = new AndroidBluetoothConnectionBackend();
        connectionRuntime = new BluetoothConnectionOperationRuntime(
            connectionBackend,
            unityMainThreadId);

        if (runtimeSettings == null)
            runtimeSettings = FindObjectOfType<RuntimeConnectionSettings>();

        Debug.Log("[AndroidBluetoothSender][AWAKE]");

        if (!IsBluetoothEnabled())
        {
            Debug.Log("[AndroidBluetoothSender][DISABLED] Bluetooth disabled by RuntimeConnectionSettings.");
            ClearPendingCommands();
        }
    }

    void Start()
    {
        if (!IsBluetoothEnabled())
        {
            Debug.Log("[AndroidBluetoothSender][START_SKIP] Bluetooth disabled.");
            return;
        }

        Debug.Log(
            $"[AndroidBluetoothSender][START] " +
            $"autoConnectOnStart:{autoConnectOnStart} " +
            $"autoReconnect:{autoReconnect} " +
            $"deviceName:{deviceName}"
        );

        if (autoConnectOnStart)
            RequestReconnectNow();
    }

    void Update()
    {
        PublishConnectionCompletion();

        if (!IsBluetoothEnabled())
        {
            connectionRuntime?.CancelActive();
            if (isConnected || isConnecting)
            {
                Close();
            }
            else
            {
                SetConnectionState(false);
                ClearPendingCommands();
            }

            return;
        }

        TryAutoReconnect();
        TrySendPendingCommand();
    }

    public void SendServo(int servoId, int angle)
    {
        if (!IsBluetoothEnabled())
        {
            Debug.Log($"[AndroidBluetoothSender][SEND_SKIP] Bluetooth disabled. id:{servoId} angle:{angle}");
            return;
        }

        Debug.Log($"[AndroidBluetoothSender][CALL] id:{servoId} angle:{angle}");

        pendingServoId = servoId;
        pendingAngle = angle;
        hasPendingCommand = true;

        hasPendingRawCommand = false;
        pendingRawCommand = string.Empty;

        TrySendPendingCommand();
    }

    public void SendRawCommand(string command)
    {
        if (!IsBluetoothEnabled())
        {
            Debug.Log($"[AndroidBluetoothSender][RAW_SEND_SKIP] Bluetooth disabled. command:{command}");
            return;
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            Debug.LogWarning("[AndroidBluetoothSender][RAW_SEND_SKIP] command is empty.");
            return;
        }

        string normalizedCommand = command.TrimEnd('\r', '\n') + "\n";
        Debug.Log($"[AndroidBluetoothSender][RAW_CALL] command:{normalizedCommand.Replace("\n", "\\n")}");

        pendingRawCommand = normalizedCommand;
        hasPendingRawCommand = true;

        hasPendingCommand = false;

        TrySendPendingCommand();
    }

    public void RequestReconnectNow()
    {
        if (!IsBluetoothEnabled())
        {
            Debug.Log("[AndroidBluetoothSender][RECONNECT_SKIP] Bluetooth disabled.");
            return;
        }

        nextReconnectTime = 0f;
        TryAutoReconnect();
    }

    private bool IsBluetoothEnabled()
    {
        if (runtimeSettings == null)
            return true;

        return runtimeSettings.useBluetooth &&
            runtimeSettings.androidBodyTransport ==
                RuntimeConnectionSettings.AndroidBodyTransport.Bluetooth;
    }

    private void TryAutoReconnect()
    {
        if (!IsBluetoothEnabled())
            return;

        if (applicationPaused ||
            applicationQuitting ||
            !isActiveAndEnabled)
        {
            return;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        if (!autoReconnect)
            return;

        if (isConnected || isConnecting)
            return;

        if (Time.realtimeSinceStartup < nextReconnectTime)
            return;

        Debug.Log("[AndroidBluetoothSender][AUTO_RECONNECT]");
        Connect();
#endif
    }

    public void Connect()
    {
        if (applicationPaused ||
            applicationQuitting ||
            !isActiveAndEnabled)
        {
            Debug.Log(
                "[AndroidBluetoothSender][CONNECT_SKIP] " +
                "Sender is paused, quitting, or disabled."
            );
            return;
        }

        if (!IsBluetoothEnabled())
        {
            Debug.Log("[AndroidBluetoothSender][CONNECT_SKIP] Bluetooth disabled.");
            return;
        }

        Debug.Log(
            $"[AndroidBluetoothSender][CONNECT_ENTER] " +
            $"isConnected:{isConnected} " +
            $"isConnecting:{isConnecting} " +
            $"deviceName:{deviceName}"
        );

#if UNITY_ANDROID && !UNITY_EDITOR
        if (isConnected)
        {
            Debug.Log("[AndroidBluetoothSender][CONNECT_SKIP] already connected");
            return;
        }

        if (isConnecting)
        {
            Debug.Log("[AndroidBluetoothSender][CONNECT_SKIP] already connecting");
            return;
        }

        var request = new BluetoothConnectionRequest(
            "bluetooth-connect:" +
                Interlocked.Increment(ref connectionOperationSequence),
            deviceName,
            sppUuid,
            DateTime.UtcNow);
        BluetoothConnectionAdmissionResult admission =
            connectionRuntime.TryStart(request);

        if (admission == BluetoothConnectionAdmissionResult.Started)
        {
            isConnecting = true;
            connectProbeStarted =
                SalieriRuntimePerformanceProbe.Timestamp();
            Debug.Log(
                $"[AndroidBluetoothSender][CONNECT_BEGIN] " +
                $"operationId:{request.OperationId} " +
                $"mainThread:{unityMainThreadId} " +
                "active:1 pending:0");
        }
        else if (admission ==
            BluetoothConnectionAdmissionResult.CoalescedActive)
        {
            isConnecting = true;
            Debug.Log(
                "[AndroidBluetoothSender][CONNECT_COALESCED] " +
                "active connect already owns the slot");
        }
        else
        {
            Debug.LogWarning(
                "[AndroidBluetoothSender][CONNECT_REJECTED] shutdown");
        }
#else
        Debug.Log("[AndroidBluetoothSender][EDITOR_DUMMY] Connect skipped in Editor");
#endif
    }

    private void PublishConnectionCompletion()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (connectionRuntime == null ||
            !connectionRuntime.TryTakeCompletion(out
                BluetoothConnectionOperationResult result))
        {
            return;
        }

        if (connectProbeStarted > 0L)
        {
            SalieriRuntimePerformanceProbe.RecordDuration(
                SalieriRuntimePerformanceMetric.BluetoothConnect,
                connectProbeStarted);
            connectProbeStarted = 0L;
        }

        isConnecting = false;
        bool canPublish =
            IsBluetoothEnabled() &&
            !applicationPaused &&
            !applicationQuitting &&
            isActiveAndEnabled;

        if (result.Succeeded && canPublish)
        {
            bluetoothSocket =
                result.Resources.SocketHandle as AndroidJavaObject;
            outputStream =
                result.Resources.OutputStreamHandle as AndroidJavaObject;

            if (bluetoothSocket != null && outputStream != null)
            {
                SetConnectionState(true);
                Debug.Log(
                    $"[AndroidBluetoothSender][CONNECT_COMPLETED] " +
                    $"operationId:{result.Request.OperationId} " +
                    $"durationMs:{result.OperationDurationMilliseconds:F3} " +
                    $"mainThread:{unityMainThreadId} " +
                    $"workerThread:{result.WorkerThreadId} " +
                    $"jniThread:{result.JniThreadId} " +
                    $"completionThread:{result.CompletionThreadId} " +
                    $"jniAttached:{result.JniAttached} " +
                    "terminal:Succeeded");
                return;
            }

            connectionRuntime.ReleaseResourcesAsync(result.Resources);
            MarkConnectFailed();
            return;
        }

        if (result.Resources != null)
            connectionRuntime.ReleaseResourcesAsync(result.Resources);

        if (!canPublish ||
            result.TerminalStatus ==
                BluetoothConnectionTerminalStatus.Cancelled)
        {
            SetConnectionState(false);
            Debug.Log(
                $"[AndroidBluetoothSender][CONNECT_COMPLETION_SUPPRESSED] " +
                $"operationId:{result.Request.OperationId} " +
                $"terminal:{result.TerminalStatus} " +
                $"failure:{result.FailureKind}");
            return;
        }

        Debug.LogError(
            $"[AndroidBluetoothSender][CONNECT_FAILED_RESULT] " +
            $"operationId:{result.Request.OperationId} " +
            $"durationMs:{result.OperationDurationMilliseconds:F3} " +
            $"mainThread:{unityMainThreadId} " +
            $"workerThread:{result.WorkerThreadId} " +
            $"jniThread:{result.JniThreadId} " +
            $"completionThread:{result.CompletionThreadId} " +
            $"jniAttached:{result.JniAttached} " +
            $"failure:{result.FailureKind} " +
            $"type:{result.FailureType} " +
            $"message:{result.FailureMessage}");
        MarkConnectFailed();
#endif
    }

    private void TrySendPendingCommand()
    {
        if (!IsBluetoothEnabled())
        {
            ClearPendingCommands();
            return;
        }

        if (!hasPendingCommand && !hasPendingRawCommand)
            return;

        string pendingDescription = hasPendingRawCommand
            ? $"raw:{pendingRawCommand.Replace("\n", "\\n")}"
            : $"id:{pendingServoId} angle:{pendingAngle}";

        Debug.Log(
            $"[AndroidBluetoothSender][SEND_PENDING] " +
            $"isConnected:{isConnected} " +
            $"isConnecting:{isConnecting} " +
            $"deviceName:{deviceName} " +
            pendingDescription
        );

#if UNITY_ANDROID && !UNITY_EDITOR
        if (!isConnected)
        {
            Debug.LogWarning("[AndroidBluetoothSender][SEND_SKIP] not connected. Command dropped.");
            ClearPendingCommands();
            return;
        }

        if (outputStream == null)
        {
            Debug.LogWarning("[AndroidBluetoothSender][SEND_SKIP] outputStream is null. Command dropped.");
            ClearPendingCommands();
            MarkDisconnected();
            Close();
            return;
        }

        if (Time.realtimeSinceStartup - lastSendTime < minSendInterval)
            return;

        string command = hasPendingRawCommand
            ? pendingRawCommand
            : $"#{pendingServoId} P{pendingAngle}\n";

        Debug.Log($"[AndroidBluetoothSender][WRITE_BEFORE] [{command.Replace("\n", "\\n")}]");

        try
        {
            byte[] bytes = Encoding.ASCII.GetBytes(command);
            sbyte[] signedBytes = new sbyte[bytes.Length];

            for (int i = 0; i < bytes.Length; i++)
                signedBytes[i] = unchecked((sbyte)bytes[i]);

            long writeStarted =
                SalieriRuntimePerformanceProbe.Timestamp();
            outputStream.Call("write", signedBytes);
            SalieriRuntimePerformanceProbe.RecordDuration(
                SalieriRuntimePerformanceMetric.BluetoothWrite,
                writeStarted,
                signedBytes.Length);

            long flushStarted =
                SalieriRuntimePerformanceProbe.Timestamp();
            outputStream.Call("flush");
            SalieriRuntimePerformanceProbe.RecordDuration(
                SalieriRuntimePerformanceMetric.BluetoothFlush,
                flushStarted,
                signedBytes.Length);

            lastSendTime = Time.realtimeSinceStartup;
            ClearPendingCommands();

            Debug.Log("[AndroidBluetoothSender][WRITE_AFTER]");
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"[AndroidBluetoothSender][WRITE_ERROR] " +
                $"{e.GetType().Name}: {e.Message}\n{e.StackTrace}"
            );

            ClearPendingCommands();
            MarkDisconnected();
            Close();
        }
#else
        if (Time.realtimeSinceStartup - lastSendTime < minSendInterval)
            return;

        string command = hasPendingRawCommand
            ? pendingRawCommand
            : $"#{pendingServoId} P{pendingAngle}\n";

        Debug.Log($"[AndroidBluetoothSender][EDITOR_DUMMY] command:[{command.Replace("\n", "\\n")}]");

        lastSendTime = Time.realtimeSinceStartup;
        ClearPendingCommands();
#endif
    }

    private void MarkConnectFailed()
    {
        isConnecting = false;
        SetConnectionState(false);
        nextReconnectTime = Time.realtimeSinceStartup + reconnectCooldownSeconds;

        Debug.Log(
            $"[AndroidBluetoothSender][CONNECT_FAILED] " +
            $"nextReconnectIn:{reconnectCooldownSeconds}s"
        );
    }

    private void MarkDisconnected()
    {
        isConnecting = false;
        SetConnectionState(false);
        nextReconnectTime = Time.realtimeSinceStartup + reconnectCooldownSeconds;

        Debug.Log(
            $"[AndroidBluetoothSender][DISCONNECTED] " +
            $"nextReconnectIn:{reconnectCooldownSeconds}s"
        );
    }

    public void Close()
    {
        Debug.Log("[AndroidBluetoothSender][CLOSE]");

#if UNITY_ANDROID && !UNITY_EDITOR
        connectionRuntime?.CancelActive();

        AndroidJavaObject socket = bluetoothSocket;
        AndroidJavaObject stream = outputStream;
        bluetoothSocket = null;
        outputStream = null;

        if (socket != null || stream != null)
        {
            connectionRuntime?.ReleaseResourcesAsync(
                new BluetoothConnectionResources(socket, stream));
            Debug.Log(
                "[AndroidBluetoothSender][CLOSE_REQUESTED] " +
                "resource release runs off Main Thread");
        }
#endif

        isConnecting = false;
        SetConnectionState(false);
        ClearPendingCommands();
    }

    private void SetConnectionState(bool connected)
    {
        if (isConnected == connected)
        {
            return;
        }

        isConnected = connected;

        Delegate[] listeners =
            ConnectionStateChanged?.GetInvocationList();

        if (listeners == null)
        {
            return;
        }

        for (int i = 0; i < listeners.Length; i++)
        {
            try
            {
                ((Action<bool>)listeners[i]).Invoke(
                    isConnected
                );
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }
    }

    private void ClearPendingCommands()
    {
        hasPendingCommand = false;
        hasPendingRawCommand = false;
        pendingRawCommand = string.Empty;
    }

    private void OnDisable()
    {
        Close();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        applicationPaused = pauseStatus;

        if (pauseStatus)
        {
            Close();
        }
    }

    private void OnApplicationQuit()
    {
        applicationQuitting = true;
        connectionRuntime?.BeginShutdown();
        Close();
    }

    void OnDestroy()
    {
        connectionRuntime?.BeginShutdown();
        Close();
    }
}
