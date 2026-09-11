// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

// v1.2 / 2026-05-03 21:35 JST / Keep latest command only and guard Bluetooth COM write clog

#if UNITY_EDITOR || UNITY_STANDALONE_WIN

using UnityEngine;
using System.IO.Ports;

public class SerialSender_PC : MonoBehaviour, ICommandSender
{
    [Header("Serial Settings")]
    public string portName = "COM4";   // COM4 or COM5 に変更
    public int baudRate = 9600;

    [Header("Timeout Settings")]
    public int writeTimeout = 100;
    public int readTimeout = 100;

    [Header("Send Guard")]
    public float minSendInterval = 0.05f;
    public float timeoutCooldown = 0.15f;
    public bool discardOutBufferOnTimeout = true;

    private SerialPort serialPort;

    private bool hasPendingCommand = false;
    private int pendingServoId = 0;
    private int pendingAngle = 90;

    private bool isWriting = false;
    private float lastWriteTime = -999f;
    private float lastTimeoutTime = -999f;

    void Start()
    {
        try
        {
            serialPort = new SerialPort(portName, baudRate);
            serialPort.NewLine = "\n";
            serialPort.WriteTimeout = writeTimeout;
            serialPort.ReadTimeout = readTimeout;
            serialPort.Open();

            Debug.Log($"[SerialSender_PC] Opened {portName}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SerialSender_PC] Failed: {e.Message}");
        }
    }

    void Update()
    {
        TrySendPendingCommand();
    }

    public void SendServo(int servoId, int angle)
    {
        // ① 呼び出し確認
        Debug.Log($"[SerialSender_PC][CALL] id:{servoId} angle:{angle}");

        if (serialPort == null)
        {
            Debug.LogWarning("[SerialSender_PC][ERROR] serialPort is null");
            return;
        }

        if (!serialPort.IsOpen)
        {
            Debug.LogWarning("[SerialSender_PC][ERROR] port not open");
            return;
        }

        // 古い命令を溜めず、常に最新命令だけ保持する
        pendingServoId = servoId;
        pendingAngle = angle;
        hasPendingCommand = true;

        TrySendPendingCommand();
    }

    private void TrySendPendingCommand()
    {
        if (!hasPendingCommand)
            return;

        if (isWriting)
            return;

        if (serialPort == null)
            return;

        if (!serialPort.IsOpen)
            return;

        if (Time.realtimeSinceStartup - lastTimeoutTime < timeoutCooldown)
            return;

        if (Time.realtimeSinceStartup - lastWriteTime < minSendInterval)
            return;

        string command = $"#{pendingServoId} P{pendingAngle}\n";

        // ② ★ここが最重要（送信直前）
        Debug.Log($"[SerialSender_PC][WRITE_BEFORE] [{command.Replace("\n", "\\n")}]");

        isWriting = true;

        try
        {
            serialPort.Write(command);

            lastWriteTime = Time.realtimeSinceStartup;
            hasPendingCommand = false;

            // ③ 送信完了
            Debug.Log("[SerialSender_PC][WRITE_AFTER]");
        }
        catch (System.TimeoutException e)
        {
            lastTimeoutTime = Time.realtimeSinceStartup;
            hasPendingCommand = false;

            if (discardOutBufferOnTimeout)
            {
                try
                {
                    serialPort.DiscardOutBuffer();
                }
                catch (System.Exception discardError)
                {
                    Debug.LogWarning($"[SerialSender_PC][DISCARD_OUT_ERROR] {discardError.Message}");
                }
            }

            Debug.LogWarning($"[SerialSender_PC][WRITE_TIMEOUT] {e.Message}");
        }
        catch (System.Exception e)
        {
            hasPendingCommand = false;
            Debug.LogError($"[SerialSender_PC][WRITE_ERROR] {e.Message}");
        }
        finally
        {
            isWriting = false;
        }
    }

    void OnDestroy()
    {
        if (serialPort != null && serialPort.IsOpen)
        {
            serialPort.Close();
            Debug.Log("[SerialSender_PC] Closed");
        }
    }
}

#endif