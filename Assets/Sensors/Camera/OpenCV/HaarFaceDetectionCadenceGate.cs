// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

namespace SalieriAI.Sensors.Camera.OpenCV
{
    public enum HaarFaceDetectionCadenceDecision
    {
        Run = 0,
        SkipNoNewFrame,
        SkipInterval
    }

    /// <summary>
    /// Pure cadence and camera-frame admission for synchronous Haar detection.
    /// A skipped decision does not imply that the last detection became invalid.
    /// </summary>
    public sealed class HaarFaceDetectionCadenceGate
    {
        public const double DefaultIntervalSeconds = 0.1d;

        private bool hasRun;
        private double lastRunAtSeconds;

        public bool HasRun => hasRun;
        public double LastRunAtSeconds => lastRunAtSeconds;

        public static double NormalizeIntervalSeconds(double configuredIntervalSeconds)
        {
            if (double.IsNaN(configuredIntervalSeconds) ||
                double.IsInfinity(configuredIntervalSeconds) ||
                configuredIntervalSeconds <= 0d)
            {
                return DefaultIntervalSeconds;
            }

            return configuredIntervalSeconds;
        }

        public HaarFaceDetectionCadenceDecision Evaluate(
            double nowSeconds,
            bool newFrameAvailable,
            double configuredIntervalSeconds)
        {
            if (!newFrameAvailable)
                return HaarFaceDetectionCadenceDecision.SkipNoNewFrame;

            if (double.IsNaN(nowSeconds) || double.IsInfinity(nowSeconds))
                return HaarFaceDetectionCadenceDecision.SkipInterval;

            double interval =
                NormalizeIntervalSeconds(configuredIntervalSeconds);

            if (!hasRun || nowSeconds < lastRunAtSeconds)
            {
                hasRun = true;
                lastRunAtSeconds = nowSeconds;
                return HaarFaceDetectionCadenceDecision.Run;
            }

            if (nowSeconds - lastRunAtSeconds < interval)
                return HaarFaceDetectionCadenceDecision.SkipInterval;

            lastRunAtSeconds = nowSeconds;
            return HaarFaceDetectionCadenceDecision.Run;
        }

        public void Reset()
        {
            hasRun = false;
            lastRunAtSeconds = 0d;
        }
    }
}
