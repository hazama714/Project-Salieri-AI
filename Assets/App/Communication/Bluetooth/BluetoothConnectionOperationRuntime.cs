// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SalieriAI.App.Communication.Bluetooth
{
    public enum BluetoothConnectionAdmissionResult
    {
        Started = 0,
        CoalescedActive = 1,
        RejectedShutdown = 2
    }

    public enum BluetoothConnectionTerminalStatus
    {
        Succeeded = 0,
        Failed = 1,
        Cancelled = 2
    }

    public enum BluetoothConnectionFailureKind
    {
        None = 0,
        AdapterUnavailable = 1,
        BluetoothDisabled = 2,
        BondedDevicesUnavailable = 3,
        DeviceNotFound = 4,
        SocketCreationFailed = 5,
        OutputStreamUnavailable = 6,
        AndroidJavaException = 7,
        IOException = 8,
        UnexpectedException = 9,
        Cancelled = 10,
        Shutdown = 11,
        PlatformUnavailable = 12
    }

    public sealed class BluetoothConnectionRequest
    {
        public BluetoothConnectionRequest(
            string operationId,
            string deviceName,
            string sppUuid,
            DateTime requestedAtUtc)
        {
            OperationId = operationId ?? string.Empty;
            DeviceName = deviceName ?? string.Empty;
            SppUuid = sppUuid ?? string.Empty;
            RequestedAtUtc = requestedAtUtc;
        }

        public string OperationId { get; }
        public string DeviceName { get; }
        public string SppUuid { get; }
        public DateTime RequestedAtUtc { get; }
    }

    public sealed class BluetoothConnectionResources
    {
        public BluetoothConnectionResources(
            object socketHandle,
            object outputStreamHandle)
        {
            SocketHandle = socketHandle;
            OutputStreamHandle = outputStreamHandle;
        }

        public object SocketHandle { get; }
        public object OutputStreamHandle { get; }
    }

    public sealed class BluetoothConnectionOperationResult
    {
        private BluetoothConnectionOperationResult(
            BluetoothConnectionRequest request,
            BluetoothConnectionTerminalStatus terminalStatus,
            BluetoothConnectionFailureKind failureKind,
            string failureType,
            string failureMessage,
            BluetoothConnectionResources resources,
            double operationDurationMilliseconds,
            int workerThreadId,
            int jniThreadId,
            int completionThreadId,
            bool jniAttached)
        {
            Request = request;
            TerminalStatus = terminalStatus;
            FailureKind = failureKind;
            FailureType = failureType ?? string.Empty;
            FailureMessage = failureMessage ?? string.Empty;
            Resources = resources;
            OperationDurationMilliseconds = operationDurationMilliseconds;
            WorkerThreadId = workerThreadId;
            JniThreadId = jniThreadId;
            CompletionThreadId = completionThreadId;
            JniAttached = jniAttached;
        }

        public BluetoothConnectionRequest Request { get; }
        public BluetoothConnectionTerminalStatus TerminalStatus { get; }
        public BluetoothConnectionFailureKind FailureKind { get; }
        public string FailureType { get; }
        public string FailureMessage { get; }
        public BluetoothConnectionResources Resources { get; }
        public double OperationDurationMilliseconds { get; }
        public int WorkerThreadId { get; }
        public int JniThreadId { get; }
        public int CompletionThreadId { get; }
        public bool JniAttached { get; }
        public bool Succeeded =>
            TerminalStatus == BluetoothConnectionTerminalStatus.Succeeded;

        public static BluetoothConnectionOperationResult Success(
            BluetoothConnectionRequest request,
            BluetoothConnectionResources resources,
            int jniThreadId,
            bool jniAttached)
        {
            return new BluetoothConnectionOperationResult(
                request,
                BluetoothConnectionTerminalStatus.Succeeded,
                BluetoothConnectionFailureKind.None,
                string.Empty,
                string.Empty,
                resources,
                0.0,
                0,
                jniThreadId,
                0,
                jniAttached);
        }

        public static BluetoothConnectionOperationResult Failure(
            BluetoothConnectionRequest request,
            BluetoothConnectionFailureKind failureKind,
            string failureType,
            string failureMessage,
            int jniThreadId = 0,
            bool jniAttached = false)
        {
            return new BluetoothConnectionOperationResult(
                request,
                BluetoothConnectionTerminalStatus.Failed,
                failureKind,
                failureType,
                failureMessage,
                null,
                0.0,
                0,
                jniThreadId,
                0,
                jniAttached);
        }

        internal BluetoothConnectionOperationResult WithRuntime(
            double durationMilliseconds,
            int workerThreadId)
        {
            return new BluetoothConnectionOperationResult(
                Request,
                TerminalStatus,
                FailureKind,
                FailureType,
                FailureMessage,
                Resources,
                durationMilliseconds,
                workerThreadId,
                JniThreadId,
                0,
                JniAttached);
        }

        internal BluetoothConnectionOperationResult WithCompletionThread(
            int completionThreadId)
        {
            return new BluetoothConnectionOperationResult(
                Request,
                TerminalStatus,
                FailureKind,
                FailureType,
                FailureMessage,
                Resources,
                OperationDurationMilliseconds,
                WorkerThreadId,
                JniThreadId,
                completionThreadId,
                JniAttached);
        }

        internal BluetoothConnectionOperationResult AsCancelled(
            BluetoothConnectionFailureKind failureKind)
        {
            return new BluetoothConnectionOperationResult(
                Request,
                BluetoothConnectionTerminalStatus.Cancelled,
                failureKind,
                string.Empty,
                string.Empty,
                null,
                OperationDurationMilliseconds,
                WorkerThreadId,
                JniThreadId,
                0,
                JniAttached);
        }

        internal static BluetoothConnectionOperationResult UnexpectedFailure(
            BluetoothConnectionRequest request,
            Exception exception)
        {
            return Failure(
                request,
                BluetoothConnectionFailureKind.UnexpectedException,
                exception == null ? string.Empty : exception.GetType().Name,
                exception == null ? string.Empty : exception.Message);
        }
    }

    public interface IBluetoothConnectionOperationBackend
    {
        BluetoothConnectionOperationResult Connect(
            BluetoothConnectionRequest request);

        void Release(BluetoothConnectionResources resources);
    }

    /// <summary>
    /// Bluetooth-specific active=1 / pending=0 execution boundary.
    /// A duplicate request is coalesced and never queued.
    /// </summary>
    public sealed class BluetoothConnectionOperationRuntime
    {
        private readonly IBluetoothConnectionOperationBackend backend;
        private readonly int designatedCompletionThreadId;
        private readonly ConcurrentQueue<BluetoothConnectionOperationResult>
            completions =
                new ConcurrentQueue<BluetoothConnectionOperationResult>();

        private int active;
        private int accepting = 1;
        private int lifecycleVersion;
        private int maximumConcurrentBackendCalls;
        private int currentBackendCalls;
        private long startedCount;
        private long coalescedCount;
        private long completionCount;

        public BluetoothConnectionOperationRuntime(
            IBluetoothConnectionOperationBackend backend,
            int designatedCompletionThreadId)
        {
            this.backend = backend ??
                throw new ArgumentNullException(nameof(backend));
            this.designatedCompletionThreadId =
                designatedCompletionThreadId;
        }

        public bool IsActive => Volatile.Read(ref active) != 0;
        public bool IsAccepting => Volatile.Read(ref accepting) != 0;
        public long StartedCount => Interlocked.Read(ref startedCount);
        public long CoalescedCount => Interlocked.Read(ref coalescedCount);
        public long CompletionCount => Interlocked.Read(ref completionCount);
        public int MaximumConcurrentBackendCalls =>
            Volatile.Read(ref maximumConcurrentBackendCalls);

        public BluetoothConnectionAdmissionResult TryStart(
            BluetoothConnectionRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (!IsAccepting)
                return BluetoothConnectionAdmissionResult.RejectedShutdown;

            if (Interlocked.CompareExchange(ref active, 1, 0) != 0)
            {
                Interlocked.Increment(ref coalescedCount);
                return BluetoothConnectionAdmissionResult.CoalescedActive;
            }

            if (!IsAccepting)
            {
                Volatile.Write(ref active, 0);
                return BluetoothConnectionAdmissionResult.RejectedShutdown;
            }

            int version = Volatile.Read(ref lifecycleVersion);
            Interlocked.Increment(ref startedCount);
            Task.Run(() => Execute(request, version));
            return BluetoothConnectionAdmissionResult.Started;
        }

        public bool TryTakeCompletion(
            out BluetoothConnectionOperationResult result)
        {
            result = null;
            if (Thread.CurrentThread.ManagedThreadId !=
                designatedCompletionThreadId)
            {
                return false;
            }

            if (!completions.TryDequeue(out result))
                return false;

            result = result.WithCompletionThread(
                Thread.CurrentThread.ManagedThreadId);
            Interlocked.Increment(ref completionCount);
            Volatile.Write(ref active, 0);
            return true;
        }

        public void CancelActive()
        {
            Interlocked.Increment(ref lifecycleVersion);
            ReplaceQueuedCompletionWithCancellation(
                BluetoothConnectionFailureKind.Cancelled);
        }

        public void BeginShutdown()
        {
            Volatile.Write(ref accepting, 0);
            Interlocked.Increment(ref lifecycleVersion);
            ReplaceQueuedCompletionWithCancellation(
                BluetoothConnectionFailureKind.Shutdown);
        }

        public void ReleaseResourcesAsync(
            BluetoothConnectionResources resources)
        {
            if (resources == null)
                return;
            Task.Run(() => SafeRelease(resources));
        }

        private void Execute(
            BluetoothConnectionRequest request,
            int requestLifecycleVersion)
        {
            int workerThreadId = Thread.CurrentThread.ManagedThreadId;
            long started = Stopwatch.GetTimestamp();
            BluetoothConnectionOperationResult result;

            if (requestLifecycleVersion != Volatile.Read(ref lifecycleVersion))
            {
                result = BluetoothConnectionOperationResult.Failure(
                    request,
                    BluetoothConnectionFailureKind.Cancelled,
                    string.Empty,
                    string.Empty).AsCancelled(
                        BluetoothConnectionFailureKind.Cancelled);
            }
            else
            {
                int concurrent = Interlocked.Increment(
                    ref currentBackendCalls);
                UpdateMaximum(ref maximumConcurrentBackendCalls, concurrent);
                try
                {
                    result = backend.Connect(request) ??
                        BluetoothConnectionOperationResult.Failure(
                            request,
                            BluetoothConnectionFailureKind.UnexpectedException,
                            "NullResult",
                            "Bluetooth backend returned null.");
                }
                catch (Exception exception)
                {
                    result =
                        BluetoothConnectionOperationResult.UnexpectedFailure(
                            request,
                            exception);
                }
                finally
                {
                    Interlocked.Decrement(ref currentBackendCalls);
                }
            }

            double durationMilliseconds =
                (Stopwatch.GetTimestamp() - started) * 1000.0 /
                Stopwatch.Frequency;
            result = result.WithRuntime(
                durationMilliseconds,
                workerThreadId);

            bool invalidated =
                requestLifecycleVersion != Volatile.Read(ref lifecycleVersion);
            if (invalidated)
            {
                if (result.Resources != null)
                    SafeRelease(result.Resources);
                BluetoothConnectionFailureKind kind = IsAccepting
                    ? BluetoothConnectionFailureKind.Cancelled
                    : BluetoothConnectionFailureKind.Shutdown;
                result = result.AsCancelled(kind);
            }

            completions.Enqueue(result);
        }

        private void ReplaceQueuedCompletionWithCancellation(
            BluetoothConnectionFailureKind failureKind)
        {
            BluetoothConnectionOperationResult queued;
            if (!completions.TryDequeue(out queued))
                return;

            if (queued.Resources != null)
                ReleaseResourcesAsync(queued.Resources);
            completions.Enqueue(queued.AsCancelled(failureKind));
        }

        private void SafeRelease(BluetoothConnectionResources resources)
        {
            try
            {
                backend.Release(resources);
            }
            catch
            {
                // Resource release is best-effort after cancellation/shutdown.
            }
        }

        private static void UpdateMaximum(ref int target, int candidate)
        {
            while (true)
            {
                int current = Volatile.Read(ref target);
                if (candidate <= current)
                    return;
                if (Interlocked.CompareExchange(
                        ref target,
                        candidate,
                        current) == current)
                {
                    return;
                }
            }
        }
    }
}
