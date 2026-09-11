// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Input
{
    /// <summary>
    /// Phase 10-D:
    /// ユーザー入力の発生元。
    ///
    /// AndroidSTT / TextInput / DebugInput / 将来LocalSTT / ExternalSTT を
    /// 同じ UserSpeech Runtime 本流へ集約するための source 定義。
    /// </summary>
    public enum UserInputSource
    {
        Unknown = 0,

        AndroidSTT = 10,
        TextInput = 20,
        DebugInput = 30,

        LocalSTT = 40,
        ExternalSTT = 50,
        RaspberryPiSTT = 60,

        EditorManual = 70
    }
}
