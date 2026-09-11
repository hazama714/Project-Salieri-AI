// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using UnityEngine;

[Serializable]
public sealed class LLMActionDecision
{
    public string action = "none";
    public string speech = "";
    public string reason = "";
    public float confidence = 0f;

    public bool IsNone()
    {
        return string.IsNullOrWhiteSpace(action) ||
               action == "none";
    }

    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(action))
            action = "none";

        if (speech == null)
            speech = "";

        if (reason == null)
            reason = "";

        confidence = Mathf.Clamp01(confidence);
    }

    public static LLMActionDecision SafeDefault(string reason = "fallback")
    {
        return new LLMActionDecision
        {
            action = "none",
            speech = "",
            reason = reason,
            confidence = 0f
        };
    }
}