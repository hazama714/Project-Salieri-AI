// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System.Threading;

namespace SalieriAI.Core.Perception.ObjectDetection
{
    internal enum YoloInferenceAdmissionDecision
    {
        Accepted = 0,
        RejectedBusy,
        RejectedShutdown,
        RejectedInvalidRequest
    }

    /// <summary>
    /// YOLO inference専用のpendingなしSingle Flight admission。
    /// Camera frameを待機列へ蓄積せず、active中の新規frameはskipする。
    /// </summary>
    internal sealed class YoloInferenceSingleFlightAdmission
    {
        private long activeRequestId;
        private int shuttingDown;

        internal bool Active =>
            Interlocked.Read(ref activeRequestId) > 0L;

        internal long ActiveRequestId =>
            Interlocked.Read(ref activeRequestId);

        internal int PendingCount => 0;

        internal bool ShuttingDown =>
            Volatile.Read(ref shuttingDown) != 0;

        internal YoloInferenceAdmissionDecision TryAdmit(long requestId)
        {
            if (requestId <= 0L)
                return YoloInferenceAdmissionDecision.RejectedInvalidRequest;

            if (ShuttingDown)
                return YoloInferenceAdmissionDecision.RejectedShutdown;

            if (Interlocked.CompareExchange(
                    ref activeRequestId,
                    requestId,
                    0L) != 0L)
            {
                return YoloInferenceAdmissionDecision.RejectedBusy;
            }

            if (!ShuttingDown)
                return YoloInferenceAdmissionDecision.Accepted;

            Interlocked.CompareExchange(ref activeRequestId, 0L, requestId);
            return YoloInferenceAdmissionDecision.RejectedShutdown;
        }

        internal bool IsActive(long requestId)
        {
            return requestId > 0L &&
                   Interlocked.Read(ref activeRequestId) == requestId;
        }

        internal bool TryComplete(long requestId)
        {
            return requestId > 0L &&
                   Interlocked.CompareExchange(
                       ref activeRequestId,
                       0L,
                       requestId) == requestId;
        }

        internal void BeginShutdown()
        {
            Volatile.Write(ref shuttingDown, 1);
        }
    }

    internal readonly struct YoloInferenceThreadObservation
    {
        internal readonly int MainThreadId;
        internal readonly int WorkerThreadId;
        internal readonly int CompletionThreadId;

        internal YoloInferenceThreadObservation(
            int mainThreadId,
            int workerThreadId,
            int completionThreadId)
        {
            MainThreadId = mainThreadId;
            WorkerThreadId = workerThreadId;
            CompletionThreadId = completionThreadId;
        }

        internal bool WorkerOffMain =>
            MainThreadId > 0 &&
            WorkerThreadId > 0 &&
            WorkerThreadId != MainThreadId;

        internal bool CompletionOnMain =>
            MainThreadId > 0 &&
            CompletionThreadId == MainThreadId;
    }
}
