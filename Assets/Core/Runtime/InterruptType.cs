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
    /// Runtime上で発生した入力を、将来どの種類の割り込みとして扱うかを表す。
    ///
    /// この段階では分類用の型であり、直接停止・状態変更・再処理は行わない。
    /// </summary>
    public enum InterruptType
    {
        None = 0,

        /// <summary>
        /// 安全停止点まで待つ通常割り込み。
        /// </summary>
        Soft = 10,

        /// <summary>
        /// 短い猶予で安全停止へ向かう強い割り込み。
        /// </summary>
        Hard = 20,

        /// <summary>
        /// SafeState到達を待たず、緊急系として扱う割り込み。
        /// Phase 7-Aでは分類ログのみ。
        /// </summary>
        Emergency = 30,

        /// <summary>
        /// 発話中にUserSpeechが入った場合の発話割り込み候補。
        /// </summary>
        SpeechInterrupt = 40,

        /// <summary>
        /// 「止まれ」など、LLM判断に回さない停止命令候補。
        /// </summary>
        StopCommand = 50,

        /// <summary>
        /// 顔検出・顔復帰など、顔追従/探索復帰に関係する割り込み候補。
        /// </summary>
        FaceTrackOverride = 60,

        /// <summary>
        /// バッテリー・温度・Bluetoothなど、資源制限に関係する割り込み候補。
        /// </summary>
        ResourceLimited = 70
    }
}
