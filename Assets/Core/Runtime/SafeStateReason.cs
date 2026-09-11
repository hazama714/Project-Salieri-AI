// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Runtime
{
    /// <summary>
    /// Phase 7-A:
    /// SafeStateRequestを作る理由。
    ///
    /// この段階では理由付けとログ分類に使うだけで、実停止は行わない。
    /// </summary>
    public enum SafeStateReason
    {
        None = 0,
        UserSpeech = 10,
        StopCommand = 20,
        Emergency = 30,
        FaceDetected = 40,
        FaceLost = 50,
        DeviceStateChanged = 60,
        ResourceLimited = 70,
        Touch = 80,
        EnvironmentChanged = 90
    }
}
