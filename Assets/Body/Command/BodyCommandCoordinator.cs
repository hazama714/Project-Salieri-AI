// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;

using SalieriAI.Runtime;

using UnityEngine;

namespace SalieriAI.Body.Command
{
    public enum BodyCommandSenderSelectionMode
    {
        Single,
        Platform
    }

    /// <summary>
    /// 通常運用時の Body Command を順番に送信する Coordinator。
    ///
    /// 対象:
    /// - 首サーボ
    /// - 腕サーボ
    /// - 将来の胴体・脚サーボ
    /// - Crawler Raw Command
    ///
    /// 目的:
    /// Android transportはpending commandを即時処理するため、
    /// 複数命令を短時間に直接流すと上書きされる可能性がある。
    ///
    /// この Coordinator は、命令を Queue に積み、
    /// 一定間隔で1件ずつ選択済みSenderへ渡す。
    ///
    /// CRAWLER:STOP は安全上の優先命令として、
    /// 通常 Queue より先に送信する。
    /// </summary>
    public sealed class BodyCommandCoordinator :
        MonoBehaviour,
        IBodyCommandSender,
        IConnectionStateProvider
    {
        private enum BodyCommandType
        {
            Servo,
            Raw
        }

        private sealed class PendingBodyCommand
        {
            public BodyCommandType Type;
            public int ServoIndex;
            public int Angle;
            public string RawCommand;
            public float AcceptedTime;

            public static PendingBodyCommand CreateServo(
                int servoIndex,
                int angle
            )
            {
                return new PendingBodyCommand
                {
                    Type = BodyCommandType.Servo,
                    ServoIndex = servoIndex,
                    Angle = angle,
                    RawCommand = string.Empty,
                    AcceptedTime = Time.realtimeSinceStartup
                };
            }

            public static PendingBodyCommand CreateRaw(
                string command
            )
            {
                return new PendingBodyCommand
                {
                    Type = BodyCommandType.Raw,
                    ServoIndex = -1,
                    Angle = 0,
                    RawCommand = command,
                    AcceptedTime = Time.realtimeSinceStartup
                };
            }
        }

        [Header("Actual Sender")]

        [Tooltip(
            "Single modeで使用する実Sender。"
        )]
        [SerializeField]
        private MonoBehaviour actualSenderBehaviour;

        [SerializeField]
        private BodyCommandSenderSelectionMode senderSelectionMode =
            BodyCommandSenderSelectionMode.Single;

        [SerializeField]
        private MonoBehaviour windowsSenderBehaviour;

        [SerializeField]
        private MonoBehaviour androidSenderBehaviour;

        [SerializeField]
        private MonoBehaviour androidUsbSerialSenderBehaviour;

        [SerializeField]
        private RuntimeConnectionSettings runtimeConnectionSettings;

        [Header("Queue Timing")]

        [Tooltip(
            "Queue から次の1件を送るまでの最小間隔。"
        )]
        [SerializeField]
        private float sendInterval = 0.08f;

        [Header("Queue Limit")]

        [Tooltip(
            "異常時に Queue が増え続けないようにする上限。"
        )]
        [SerializeField]
        private int maxQueueSize = 128;

        [Header("Diagnostics")]

        [SerializeField]
        private bool logEnqueue = true;

        [SerializeField]
        private bool logDispatch = true;

        private const string CrawlerStopCommand = "CRAWLER:STOP";

        private readonly Queue<PendingBodyCommand> rawCommandQueue =
            new Queue<PendingBodyCommand>();

        private readonly Queue<int> pendingServoOrder =
            new Queue<int>();

        private readonly Dictionary<int, PendingBodyCommand>
            pendingServoCommands =
                new Dictionary<int, PendingBodyCommand>();

        private readonly Dictionary<int, float> lastServoDispatchTimes =
            new Dictionary<int, float>();

        private readonly Dictionary<int, int> lastServoDispatchAngles =
            new Dictionary<int, int>();

        private readonly Dictionary<int, long> lastServoDispatchSequences =
            new Dictionary<int, long>();

        private long servoDispatchSequence;

        private PendingBodyCommand priorityCommand;

        private IBodyCommandSender actualSender;
        private MonoBehaviour selectedSenderBehaviour;
        private IConnectionStateProvider
            actualConnectionStateProvider;
        private Action<bool>
            actualConnectionStateChangedHandler;
        private bool cachedConnectionState;

        private float lastDispatchTime = -999f;

        private double totalDispatchWaitSeconds;
        private long measuredDispatchCount;

        public long AcceptedServoCommandCount { get; private set; }
        public long ServoOverwriteCount { get; private set; }
        public long DispatchedServoCommandCount { get; private set; }
        public long AcceptedRawCommandCount { get; private set; }
        public long DispatchedRawCommandCount { get; private set; }
        public long PriorityCommandCount { get; private set; }
        public float MaxDispatchWaitSeconds { get; private set; }
        public float AverageDispatchWaitSeconds { get; private set; }

        public int PendingServoCount => pendingServoCommands.Count;
        public int PendingRawCommandCount => rawCommandQueue.Count;

        public BodyCommandSenderSelectionMode SenderSelectionMode =>
            senderSelectionMode;

        public MonoBehaviour SelectedSenderBehaviour =>
            selectedSenderBehaviour;

        public bool IsConnected
        {
            get
            {
                if (!cachedConnectionState ||
                    actualConnectionStateProvider == null)
                {
                    return false;
                }

                try
                {
                    return actualConnectionStateProvider.IsConnected;
                }
                catch
                {
                    return false;
                }
            }
        }

        public event Action<bool> ConnectionStateChanged;

        public int PendingCount
        {
            get
            {
                int priorityCount =
                    priorityCommand != null ? 1 : 0;

                return pendingServoCommands.Count +
                    rawCommandQueue.Count + priorityCount;
            }
        }

        private void Awake()
        {
            if (runtimeConnectionSettings == null)
            {
                runtimeConnectionSettings =
                    FindObjectOfType<RuntimeConnectionSettings>();
            }

            ResolveActualSender();
        }

        private void OnEnable()
        {
            if (actualSender != null &&
                actualConnectionStateProvider == null)
            {
                ResolveActualSender();
            }
        }

        private void OnDisable()
        {
            UnsubscribeActualConnectionStateProvider();
            SetConnectionState(false);
        }

        private void OnDestroy()
        {
            UnsubscribeActualConnectionStateProvider();
            SetConnectionState(false);
        }

        private void Update()
        {
            TryDispatchNext();
        }

        /// <summary>
        /// Servo Command を通常 Queue へ積む。
        /// servoIndex は身体全体で重複しない ID を使う。
        /// </summary>
        public void SendServo(int servoIndex, int angle)
        {
            if (servoIndex < 0)
            {
                Debug.LogWarning(
                    $"[BodyCommandCoordinator] " +
                    $"Servo index must be zero or greater. " +
                    $"servoIndex={servoIndex}",
                    this
                );

                return;
            }

            EnqueueLatestServo(
                PendingBodyCommand.CreateServo(
                    servoIndex,
                    angle
                )
            );
        }

        /// <summary>
        /// Crawler などの Raw Command を Queue へ積む。
        ///
        /// CRAWLER:STOP は Priority Command として扱う。
        /// </summary>
        public void SendRawCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                Debug.LogWarning(
                    "[BodyCommandCoordinator] " +
                    "Raw command is empty.",
                    this
                );

                return;
            }

            string normalizedCommand =
                command.TrimEnd('\r', '\n');

            if (normalizedCommand == CrawlerStopCommand)
            {
                SetPriorityStopCommand(normalizedCommand);
                return;
            }

            EnqueueRawCommand(
                PendingBodyCommand.CreateRaw(
                    normalizedCommand
                )
            );
        }

        /// <summary>
        /// Inspector 参照を再取得する。
        /// Sender 差し替え後の Debug 用にも使用できる。
        /// </summary>
        [ContextMenu("Resolve Actual Sender")]
        public void ResolveActualSender()
        {
            UnsubscribeActualConnectionStateProvider();
            SetConnectionState(false);

            actualSender = null;
            selectedSenderBehaviour = null;

            MonoBehaviour candidate =
                SelectSenderCandidate();

            IBodyCommandSender resolvedSender =
                candidate as IBodyCommandSender;

            if (resolvedSender == null)
            {
                Debug.LogWarning(
                    $"[BodyCommandCoordinator] " +
                    $"No valid sender selected. " +
                    $"mode={senderSelectionMode} " +
                    $"platform={Application.platform} " +
                    $"candidate=" +
                    $"{(candidate != null ? candidate.name : "null")}",
                    this
                );

                return;
            }

            actualSender = resolvedSender;
            selectedSenderBehaviour = candidate;

            IConnectionStateProvider provider =
                actualSender as IConnectionStateProvider;

            if (provider == null)
            {
                SetConnectionState(false);
            }
            else
            {
                actualConnectionStateProvider = provider;

                IConnectionStateProvider subscribedProvider =
                    provider;

                actualConnectionStateChangedHandler =
                    connected =>
                        HandleActualConnectionStateChanged(
                            subscribedProvider,
                            connected
                        );

                actualConnectionStateProvider.
                    ConnectionStateChanged +=
                        actualConnectionStateChangedHandler;

                bool providerConnected = false;

                try
                {
                    providerConnected = provider.IsConnected;
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }

                SetConnectionState(providerConnected);
            }

            Debug.Log(
                $"[BodyCommandCoordinator] " +
                $"Sender selected. " +
                $"mode={senderSelectionMode} " +
                $"platform={Application.platform} " +
                $"sender={selectedSenderBehaviour.name} " +
                $"connected={IsConnected}",
                this
            );
        }

        private MonoBehaviour SelectSenderCandidate()
        {
            if (senderSelectionMode ==
                BodyCommandSenderSelectionMode.Single)
            {
                return actualSenderBehaviour;
            }

            switch (Application.platform)
            {
                case RuntimePlatform.Android:
                    if (runtimeConnectionSettings != null &&
                        runtimeConnectionSettings.androidBodyTransport ==
                            RuntimeConnectionSettings.
                                AndroidBodyTransport.UsbSerial)
                    {
                        return androidUsbSerialSenderBehaviour;
                    }

                    return androidSenderBehaviour;

                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer:
                    return windowsSenderBehaviour;

                default:
                    return null;
            }
        }

        /// <summary>
        /// Debug 用。待機中の通常 Command と Priority Command を破棄する。
        /// </summary>
        [ContextMenu("Clear Pending Commands")]
        public void ClearPendingCommands()
        {
            rawCommandQueue.Clear();
            pendingServoOrder.Clear();
            pendingServoCommands.Clear();
            priorityCommand = null;

            Debug.Log(
                "[BodyCommandCoordinator] " +
                "Pending commands cleared.",
                this
            );
        }

        private void HandleActualConnectionStateChanged(
            IConnectionStateProvider sourceProvider,
            bool connected
        )
        {
            if (!ReferenceEquals(
                sourceProvider,
                actualConnectionStateProvider))
            {
                return;
            }

            bool providerConnected = false;

            try
            {
                providerConnected = connected &&
                    sourceProvider.IsConnected;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }

            SetConnectionState(providerConnected);
        }

        private void UnsubscribeActualConnectionStateProvider()
        {
            if (actualConnectionStateProvider != null &&
                actualConnectionStateChangedHandler != null)
            {
                try
                {
                    actualConnectionStateProvider.
                        ConnectionStateChanged -=
                            actualConnectionStateChangedHandler;
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }

            actualConnectionStateChangedHandler = null;
            actualConnectionStateProvider = null;
        }

        private void SetConnectionState(bool connected)
        {
            if (cachedConnectionState == connected)
            {
                return;
            }

            cachedConnectionState = connected;

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
                        cachedConnectionState
                    );
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        /// <summary>
        /// 指定した Servo ID 範囲の未送信 Servo Command だけを破棄する。
        /// Raw Command と Priority Command には影響しない。
        /// </summary>
        public int ClearPendingServoCommands(
            int minimumServoId,
            int maximumServoId
        )
        {
            if (minimumServoId > maximumServoId)
            {
                Debug.LogWarning(
                    $"[BodyCommandCoordinator] " +
                    $"Clear pending servo commands rejected: " +
                    $"invalid range={minimumServoId}-{maximumServoId}.",
                    this
                );

                return 0;
            }

            List<int> servoIdsToRemove =
                new List<int>();

            foreach (KeyValuePair<int, PendingBodyCommand> pair
                in pendingServoCommands)
            {
                PendingBodyCommand command = pair.Value;

                if (command == null ||
                    command.Type != BodyCommandType.Servo ||
                    command.ServoIndex < minimumServoId ||
                    command.ServoIndex > maximumServoId)
                {
                    continue;
                }

                servoIdsToRemove.Add(pair.Key);
            }

            for (int i = 0; i < servoIdsToRemove.Count; i++)
            {
                pendingServoCommands.Remove(
                    servoIdsToRemove[i]
                );
            }

            int pendingOrderCount =
                pendingServoOrder.Count;

            for (int i = 0; i < pendingOrderCount; i++)
            {
                int servoId =
                    pendingServoOrder.Dequeue();

                if (servoId >= minimumServoId &&
                    servoId <= maximumServoId)
                {
                    continue;
                }

                pendingServoOrder.Enqueue(servoId);
            }

            int removedCount =
                servoIdsToRemove.Count;

            Debug.Log(
                $"[BodyCommandCoordinator] " +
                $"Cleared pending servo commands. " +
                $"range={minimumServoId}-{maximumServoId} " +
                $"removed={removedCount} " +
                $"remaining={PendingServoCount}",
                this
            );

            return removedCount;
        }

        private void EnqueueLatestServo(
            PendingBodyCommand command
        )
        {
            if (command == null)
            {
                return;
            }

            AcceptedServoCommandCount++;

            if (pendingServoCommands.ContainsKey(
                command.ServoIndex))
            {
                pendingServoCommands[command.ServoIndex] =
                    command;

                ServoOverwriteCount++;
            }
            else
            {
                EnsureNormalCapacity();

                pendingServoCommands.Add(
                    command.ServoIndex,
                    command
                );

                pendingServoOrder.Enqueue(
                    command.ServoIndex
                );
            }

            LogEnqueue(command);
            TryDispatchNext();
        }

        private void EnqueueRawCommand(
            PendingBodyCommand command
        )
        {
            if (command == null)
            {
                return;
            }

            AcceptedRawCommandCount++;
            EnsureNormalCapacity();
            rawCommandQueue.Enqueue(command);

            LogEnqueue(command);
            TryDispatchNext();
        }

        private void EnsureNormalCapacity()
        {
            int safeLimit = Mathf.Max(1, maxQueueSize);

            if (pendingServoCommands.Count +
                rawCommandQueue.Count < safeLimit)
            {
                return;
            }

            while (pendingServoOrder.Count > 0)
            {
                int servoIndex = pendingServoOrder.Dequeue();

                if (pendingServoCommands.Remove(servoIndex))
                {
                    Debug.LogWarning(
                        $"[BodyCommandCoordinator] " +
                        $"Queue limit reached. Pending servo " +
                        $"command dropped. servoIndex={servoIndex} " +
                        $"limit={safeLimit}",
                        this
                    );

                    return;
                }
            }

            if (rawCommandQueue.Count > 0)
            {
                rawCommandQueue.Dequeue();

                Debug.LogWarning(
                    $"[BodyCommandCoordinator] " +
                    $"Queue limit reached. Oldest raw " +
                    $"command dropped. limit={safeLimit}",
                    this
                );
            }
        }

        private void LogEnqueue(PendingBodyCommand command)
        {
            if (logEnqueue)
            {
                Debug.Log(
                    $"[BodyCommandCoordinator] " +
                    $"Enqueued: {Describe(command)} " +
                    $"pending={PendingCount}",
                    this
                );
            }
        }

        private void SetPriorityStopCommand(
            string normalizedCommand
        )
        {
            PriorityCommandCount++;

            priorityCommand =
                PendingBodyCommand.CreateRaw(
                    normalizedCommand
                );

            if (logEnqueue)
            {
                Debug.Log(
                    $"[BodyCommandCoordinator] " +
                    $"Priority command set: " +
                    $"{normalizedCommand} " +
                    $"pending={PendingCount}",
                    this
                );
            }

            TryDispatchNext();
        }

        private void TryDispatchNext()
        {
            if (actualSender == null)
            {
                return;
            }

            if (Time.realtimeSinceStartup - lastDispatchTime <
                sendInterval)
            {
                return;
            }

            PendingBodyCommand nextCommand =
                DequeueNextCommand();

            if (nextCommand == null)
            {
                return;
            }

            Dispatch(nextCommand);

            lastDispatchTime =
                Time.realtimeSinceStartup;
        }

        private PendingBodyCommand DequeueNextCommand()
        {
            if (priorityCommand != null)
            {
                PendingBodyCommand nextPriority =
                    priorityCommand;

                priorityCommand = null;

                return nextPriority;
            }

            if (rawCommandQueue.Count > 0)
            {
                return rawCommandQueue.Dequeue();
            }

            while (pendingServoOrder.Count > 0)
            {
                int servoIndex = pendingServoOrder.Dequeue();

                if (!pendingServoCommands.TryGetValue(
                    servoIndex,
                    out PendingBodyCommand servoCommand))
                {
                    continue;
                }

                pendingServoCommands.Remove(servoIndex);
                return servoCommand;
            }

            return null;
        }

        private void Dispatch(
            PendingBodyCommand command
        )
        {
            if (command == null)
            {
                return;
            }

            if (logDispatch)
            {
                Debug.Log(
                    $"[BodyCommandCoordinator] " +
                    $"Dispatch: {Describe(command)} " +
                    $"remaining={PendingCount}",
                    this
                );
            }

            switch (command.Type)
            {
                case BodyCommandType.Servo:
                    actualSender.SendServo(
                        command.ServoIndex,
                        command.Angle
                    );

                    DispatchedServoCommandCount++;
                    lastServoDispatchTimes[command.ServoIndex] =
                        Time.realtimeSinceStartup;
                    lastServoDispatchAngles[command.ServoIndex] =
                        command.Angle;
                    servoDispatchSequence++;
                    lastServoDispatchSequences[command.ServoIndex] =
                        servoDispatchSequence;
                    break;

                case BodyCommandType.Raw:
                    actualSender.SendRawCommand(
                        command.RawCommand
                    );

                    DispatchedRawCommandCount++;
                    break;

                default:
                    Debug.LogWarning(
                        "[BodyCommandCoordinator] " +
                        "Unsupported command type.",
                        this
                    );
                    break;
            }

            RecordDispatchWait(command);
        }

        private void RecordDispatchWait(
            PendingBodyCommand command
        )
        {
            float waitSeconds = Mathf.Max(
                0f,
                Time.realtimeSinceStartup - command.AcceptedTime
            );

            measuredDispatchCount++;
            totalDispatchWaitSeconds += waitSeconds;
            MaxDispatchWaitSeconds = Mathf.Max(
                MaxDispatchWaitSeconds,
                waitSeconds
            );
            AverageDispatchWaitSeconds =
                (float)(totalDispatchWaitSeconds /
                    measuredDispatchCount);
        }

        public bool TryGetLastServoDispatch(
            int servoIndex,
            out int angle,
            out float dispatchTime
        )
        {
            bool hasAngle = lastServoDispatchAngles.TryGetValue(
                servoIndex,
                out angle
            );

            bool hasTime = lastServoDispatchTimes.TryGetValue(
                servoIndex,
                out dispatchTime
            );

            return hasAngle && hasTime;
        }

        public bool TryGetLastServoDispatchSequence(
            int servoIndex,
            out long sequence
        )
        {
            return lastServoDispatchSequences.TryGetValue(
                servoIndex,
                out sequence
            );
        }

        [ContextMenu("Reset Diagnostics")]
        public void ResetDiagnostics()
        {
            AcceptedServoCommandCount = 0;
            ServoOverwriteCount = 0;
            DispatchedServoCommandCount = 0;
            AcceptedRawCommandCount = 0;
            DispatchedRawCommandCount = 0;
            PriorityCommandCount = 0;
            MaxDispatchWaitSeconds = 0f;
            AverageDispatchWaitSeconds = 0f;
            totalDispatchWaitSeconds = 0d;
            measuredDispatchCount = 0;
            lastServoDispatchTimes.Clear();
            lastServoDispatchAngles.Clear();
            lastServoDispatchSequences.Clear();
            servoDispatchSequence = 0L;
        }

        private static string Describe(
            PendingBodyCommand command
        )
        {
            if (command == null)
            {
                return "null";
            }

            switch (command.Type)
            {
                case BodyCommandType.Servo:
                    return
                        $"Servo id={command.ServoIndex} " +
                        $"angle={command.Angle}";

                case BodyCommandType.Raw:
                    return
                        $"Raw command={command.RawCommand}";

                default:
                    return "Unknown";
            }
        }
    }
}
