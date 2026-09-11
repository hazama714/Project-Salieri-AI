// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Input
{
    public enum ExternalInputType
    {
        None = 0,
        Emergency = 10,
        UserSpeech = 20,
        Touch = 30,
        DeviceStateChange = 40,
        FaceEvent = 50,
        Environment = 60,
        IdleTick = 100
    }
}