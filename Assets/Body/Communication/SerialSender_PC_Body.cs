// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using System.IO.Ports;
#endif
using UnityEngine;
using SalieriAI.Body.Command;

/// <summary>
/// BODYLOBO / PC Editor 用の Body Command 実送信クラス。
///
/// BodyCommandCoordinator が Queue と送信間隔を管理するため、
/// このクラスは COM ポートへ実際に書き込むことだけを担当する。
///
/// 対応:
/// - Servo Command: #6 P90
/// - Raw Command:   CRAWLER:STOP
/// </summary>
public sealed class SerialSender_PC_Body :
    MonoBehaviour,
    IBodyCommandSender,
    IConnectionStateProvider
{
    [Header("Serial Settings")]

    [Tooltip("Arduino が接続されている COM ポート")]
    [SerializeField]
    private string portName = "COM4";

    [Tooltip("Arduino 側 SERIAL_BAUD と一致させる")]
    [SerializeField]
    private int baudRate = 9600;

    [Header("Timeout Settings")]

    [SerializeField]
    private int writeTimeout = 100;

    [SerializeField]
    private int readTimeout = 100;

    [Header("Write Guard")]

    [Tooltip("タイムアウト時に送信バッファを破棄する")]
    [SerializeField]
    private bool discardOutBufferOnTimeout = true;

    [Header("Diagnostics")]

    [SerializeField]
    private bool verboseLog = true;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    private SerialPort serialPort;
#endif
    private bool cachedConnected;

    public bool IsConnected
    {
        get
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if (!cachedConnected || serialPort == null)
            {
                return false;
            }

            try
            {
                return serialPort.IsOpen;
            }
            catch
            {
                return false;
            }
#else
            return false;
#endif
        }
    }

    public event Action<bool> ConnectionStateChanged;

    public string ConfiguredPortName => portName;
    public int ConfiguredBaudRate => baudRate;

    private void Start()
    {
        OpenPort();
    }

    /// <summary>
    /// Servo ID と角度を Arduino へ送信する。
    /// BodyCommandCoordinator から1件ずつ呼ばれる。
    /// </summary>
    public void SendServo(int servoIndex, int angle)
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        WriteLine($"#{servoIndex} P{angle}");
#endif
    }

    /// <summary>
    /// Crawler などの Raw Command を Arduino へ送信する。
    /// </summary>
    public void SendRawCommand(string command)
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if (string.IsNullOrWhiteSpace(command))
        {
            Debug.LogWarning(
                "[SerialSender_PC_Body] Raw command is empty.",
                this
            );

            return;
        }

        WriteLine(command.TrimEnd('\r', '\n'));
#endif
    }

    [ContextMenu("Debug Reopen Port")]
    public void DebugReopenPort()
    {
        ClosePort();
        OpenPort();
    }

    [ContextMenu("Debug Close Port")]
    public void DebugClosePort()
    {
        ClosePort();
    }

    private void OpenPort()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        try
        {
            ClosePort();

            serialPort = new SerialPort(portName, baudRate)
            {
                NewLine = "\n",
                WriteTimeout = writeTimeout,
                ReadTimeout = readTimeout
            };

            serialPort.Open();
            SetConnectionState(true);

            Debug.Log(
                $"[SerialSender_PC_Body] Opened {portName} at {baudRate} baud.",
                this
            );
        }
        catch (Exception e)
        {
            SetConnectionState(false);
            ClosePort();

            Debug.LogError(
                $"[SerialSender_PC_Body] Failed to open port: {e.Message}",
                this
            );
        }
#else
        SetConnectionState(false);
#endif
    }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    private void WriteLine(string command)
    {
        if (!IsConnected)
        {
            SetConnectionState(false);

            Debug.LogWarning(
                "[SerialSender_PC_Body] Port is not open. Command skipped.",
                this
            );

            return;
        }

        string normalizedCommand =
            command.TrimEnd('\r', '\n');

        string serialText =
            normalizedCommand + "\n";

        if (verboseLog)
        {
            Debug.Log(
                $"[SerialSender_PC_Body][WRITE_BEFORE] " +
                $"[{serialText.Replace("\n", "\\n")}]",
                this
            );
        }

        try
        {
            serialPort.Write(serialText);

            if (verboseLog)
            {
                Debug.Log(
                    "[SerialSender_PC_Body][WRITE_AFTER]",
                    this
                );
            }
        }
        catch (TimeoutException e)
        {
            if (discardOutBufferOnTimeout)
            {
                try
                {
                    serialPort.DiscardOutBuffer();
                }
                catch (Exception discardError)
                {
                    Debug.LogWarning(
                        $"[SerialSender_PC_Body][DISCARD_OUT_ERROR] " +
                        $"{discardError.Message}",
                        this
                    );
                }
            }

            Debug.LogWarning(
                $"[SerialSender_PC_Body][WRITE_TIMEOUT] {e.Message}",
                this
            );

            SetConnectionState(false);
            ClosePort();
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"[SerialSender_PC_Body][WRITE_ERROR] {e.Message}",
                this
            );

            SetConnectionState(false);
            ClosePort();
        }
    }
#endif

    private void OnDisable()
    {
        ClosePort();
    }

    private void OnDestroy()
    {
        ClosePort();
    }

    private void OnApplicationQuit()
    {
        ClosePort();
    }

    private void ClosePort()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        SerialPort portToClose = serialPort;
        serialPort = null;

        if (portToClose == null)
        {
            SetConnectionState(false);
            return;
        }

        try
        {
            if (portToClose.IsOpen)
            {
                portToClose.Close();

                Debug.Log(
                    "[SerialSender_PC_Body] Closed.",
                    this
                );
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning(
                $"[SerialSender_PC_Body] Close failed: {e.Message}",
                this
            );
        }
        finally
        {
            try
            {
                portToClose.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    $"[SerialSender_PC_Body] Dispose failed: {e.Message}",
                    this
                );
            }

            SetConnectionState(false);
        }
#else
        SetConnectionState(false);
#endif
    }

    private void SetConnectionState(bool connected)
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        bool nextState = connected;

        if (nextState)
        {
            try
            {
                nextState = serialPort != null &&
                    serialPort.IsOpen;
            }
            catch
            {
                nextState = false;
            }
        }
#else
        bool nextState = false;
#endif

        if (cachedConnected == nextState)
        {
            return;
        }

        cachedConnected = nextState;

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
                    cachedConnected
                );
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }
    }
}
