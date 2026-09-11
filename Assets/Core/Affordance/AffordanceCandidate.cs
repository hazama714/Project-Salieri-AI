// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Affordance
{
    public sealed class AffordanceCandidate
    {
        public AffordanceType Type { get; }
        public string ActionId { get; }
        public string Reason { get; }

        public AffordanceCandidate(
            AffordanceType type,
            string actionId,
            string reason)
        {
            Type = type;
            ActionId = actionId;
            Reason = reason;
        }

        public static AffordanceCandidate None(string reason)
        {
            return new AffordanceCandidate(
                AffordanceType.None,
                "none",
                reason
            );
        }
    }
}