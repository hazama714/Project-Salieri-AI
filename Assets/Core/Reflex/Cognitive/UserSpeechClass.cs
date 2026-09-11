// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Reflex.Cognitive
{
    public enum UserSpeechClass
    {
        Unknown = 0,

        Conversation = 10,

        /*
         * Phase 10-A:
         * 会話ではなく「行動意図」はあるが、
         * 対象または文脈が足りない指示。
         *
         * ExecutionRequest化せず、
         * ConversationReactionService で clarification に流す。
         */
        NeedsTarget = 15,
        NeedsContext = 16,

        ActionIntent = 20,

        StopCommand = 90,
        AdminEmergencyStop = 100
    }
}