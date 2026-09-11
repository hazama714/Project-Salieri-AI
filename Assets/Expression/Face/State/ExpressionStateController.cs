// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

public sealed class ExpressionStateController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ExpressionReactionHub reactionHub;

    [Header("Settings")]
    [SerializeField] private float fullyLostNeutralDelay = 2.0f;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    private bool hasFace;
    private float fullyLostStartedAt = -1f;

    private void Update()
    {
        if (reactionHub == null)
            return;

        if (fullyLostStartedAt < 0f)
            return;

        if (Time.time - fullyLostStartedAt >= fullyLostNeutralDelay)
        {
            fullyLostStartedAt = -1f;

            if (debugLog)
                Debug.Log("[ExpressionStateController] FullyLost delay passed. Set Neutral.");

            reactionHub.OnFaceLost();
        }
    }

    public void OnFaceStableFound()
    {
        hasFace = true;
        fullyLostStartedAt = -1f;

        if (debugLog)
            Debug.Log("[ExpressionStateController] Face StableFound");

        reactionHub.OnFaceFound();
    }

    public void OnFaceTemporaryLost()
    {
        if (debugLog)
            Debug.Log("[ExpressionStateController] Face TemporaryLost. Keep expression.");

        // 何もしない。
        // センサー瞬断では表情を変えない。
    }

    public void OnFaceFullyLost()
    {
        hasFace = false;
        fullyLostStartedAt = Time.time;

        if (debugLog)
            Debug.Log("[ExpressionStateController] Face FullyLost. Start neutral delay.");

        // すぐNeutralへ落とさない。
        // 一定時間後に落ち着かせる。
    }
}