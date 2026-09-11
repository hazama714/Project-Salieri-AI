// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using UnityEngine;

namespace SalieriAI.Autonomy
{
    public sealed class SelfStateCollector : MonoBehaviour
    {
        [Header("Debug State")]
        [SerializeField] private bool faceDetected = false;
        [SerializeField] private string currentMode = "idle";
        [SerializeField] private string lastAction = "none";
        [SerializeField] private string batteryState = "normal";

        private DateTime startTime;
        private DateTime lastActionTime;
        private DateTime lastExternalReactionTime;

        private void Awake()
        {
            startTime = DateTime.UtcNow;
            lastActionTime = startTime;
            lastExternalReactionTime = startTime;
        }

        public SelfStateSnapshot Collect()
        {
            DateTime now = DateTime.UtcNow;

            return new SelfStateSnapshot
            {
                faceDetected = faceDetected,
                currentMode = currentMode,
                lastAction = lastAction,
                batteryState = batteryState,
                secondsFromStart = (int)(now - startTime).TotalSeconds,
                secondsSinceLastAction = (int)(now - lastActionTime).TotalSeconds,
                secondsSinceExternalReaction = (int)(now - lastExternalReactionTime).TotalSeconds
            };
        }

        public void MarkAction(string action)
        {
            lastAction = string.IsNullOrWhiteSpace(action) ? "none" : action;
            lastActionTime = DateTime.UtcNow;
        }

        public void MarkExternalReaction()
        {
            lastExternalReactionTime = DateTime.UtcNow;
        }
    }

    [Serializable]
    public struct SelfStateSnapshot
    {
        public bool faceDetected;
        public string currentMode;
        public string lastAction;
        public string batteryState;
        public int secondsFromStart;
        public int secondsSinceLastAction;
        public int secondsSinceExternalReaction;
    }
}