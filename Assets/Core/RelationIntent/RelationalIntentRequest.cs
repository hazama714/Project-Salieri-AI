// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.RelationIntent
{
    public sealed class RelationalIntentRequest
    {
        public RelationalIntentType IntentType { get; }
        public string TargetId { get; }
        public float Strength { get; }

        public RelationalIntentRequest(
            RelationalIntentType intentType,
            string targetId,
            float strength = 1.0f)
        {
            IntentType = intentType;
            TargetId = targetId;
            Strength = strength;
        }

        public static RelationalIntentRequest None()
        {
            return new RelationalIntentRequest(
                RelationalIntentType.None,
                string.Empty,
                0f
            );
        }
    }
}