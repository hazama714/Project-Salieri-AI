// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Command.BodyCommand
{
    /// <summary>
    /// 入力テキストと BodyActionId の対応を表す固定BodyCommand定義。
    ///
    /// 現段階ではC#固定配列で管理する。
    /// 将来的には JSON / DB / UsageMode / BodyCapability 条件へ拡張する。
    /// </summary>
    public readonly struct BodyCommandRule
    {
        public BodyCommandRule(
            string actionId,
            string reason,
            int priority,
            bool safetyStop,
            bool useContainsMatch,
            string[] phrases
        )
        {
            ActionId = actionId;
            Reason = reason;
            Priority = priority;
            SafetyStop = safetyStop;
            UseContainsMatch = useContainsMatch;
            Phrases = phrases;
        }

        public string ActionId { get; }
        public string Reason { get; }
        public int Priority { get; }
        public bool SafetyStop { get; }
        public bool UseContainsMatch { get; }
        public string[] Phrases { get; }
    }
}
