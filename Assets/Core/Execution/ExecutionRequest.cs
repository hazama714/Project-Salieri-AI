// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

[Serializable]
public sealed class ExecutionRequest
{
    public string RequestId;
    public string ActionId;

    public string SourceEventType;
    public string SourcePayload;

    public string Reason;
    public int Priority;

    public bool RequiresBody;
    public bool RequiresSpeech;
    public bool RequiresExpression;
    public bool RequiresDevice;

    public bool AllowInterrupt;

    public static ExecutionRequest Create(
        string actionId,
        string sourceEventType,
        string sourcePayload = "",
        string reason = "",
        int priority = 0)
    {
        return new ExecutionRequest
        {
            RequestId = Guid.NewGuid().ToString("N"),
            ActionId = actionId ?? string.Empty,
            SourceEventType = sourceEventType ?? string.Empty,
            SourcePayload = sourcePayload ?? string.Empty,
            Reason = reason ?? string.Empty,
            Priority = priority,
            AllowInterrupt = true
        };
    }

    public override string ToString()
    {
        return $"requestId={RequestId} action={ActionId} source={SourceEventType} payload={SourcePayload} priority={Priority}";
    }
}