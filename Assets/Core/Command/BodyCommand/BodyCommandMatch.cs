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
    /// BodyCommandRegistry による入力テキスト照合結果。
    /// </summary>
    public readonly struct BodyCommandMatch
    {
        public BodyCommandMatch(
            string actionId,
            string reason,
            int priority,
            bool safetyStop,
            string matchedPhrase
        )
        {
            ActionId = actionId;
            Reason = reason;
            Priority = priority;
            SafetyStop = safetyStop;
            MatchedPhrase = matchedPhrase;
        }

        public string ActionId { get; }
        public string Reason { get; }
        public int Priority { get; }
        public bool SafetyStop { get; }
        public string MatchedPhrase { get; }
    }
}
