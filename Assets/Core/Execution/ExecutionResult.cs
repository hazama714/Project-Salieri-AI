// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

[Serializable]
public sealed class ExecutionResult
{
    public string RequestId;
    public string ActionId;
    public ExecutionStatus Status;
    public string Message;

    public static ExecutionResult Completed(ExecutionRequest request, string message = "")
    {
        return From(request, ExecutionStatus.Completed, message);
    }

    public static ExecutionResult Failed(ExecutionRequest request, string message)
    {
        return From(request, ExecutionStatus.Failed, message);
    }

    public static ExecutionResult Rejected(ExecutionRequest request, string message)
    {
        return From(request, ExecutionStatus.Rejected, message);
    }

    public static ExecutionResult Cancelled(ExecutionRequest request, string message = "")
    {
        return From(request, ExecutionStatus.Cancelled, message);
    }

    private static ExecutionResult From(ExecutionRequest request, ExecutionStatus status, string message)
    {
        return new ExecutionResult
        {
            RequestId = request != null ? request.RequestId : string.Empty,
            ActionId = request != null ? request.ActionId : string.Empty,
            Status = status,
            Message = message ?? string.Empty
        };
    }

    public override string ToString()
    {
        return $"requestId={RequestId} action={ActionId} status={Status} message={Message}";
    }
}