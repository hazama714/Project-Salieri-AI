// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Core.Language.Contracts;

namespace SalieriAI.Core.Input
{
    /// <summary>
    /// Runtime に流す外部入力イベント。
    ///
    /// Phase 10-D:
    /// UserSpeech の発生元を追跡するため Source を追加。
    /// 既存互換のため、旧コンストラクタも維持する。
    /// </summary>
    [Serializable]
    public class ExternalInputEvent
    {
        public ExternalInputType Type;
        public ExternalInputPriority Priority;
        public string Payload;
        public float Time;

        /// <summary>
        /// 入力元。
        /// 例:
        /// AndroidSTT / TextInput / DebugInput / LocalSTT / ExternalSTT
        ///
        /// UserSpeech以外では Unknown のままでよい。
        /// </summary>
        public string Source;

        /// <summary>
        /// Phase 1 typed language input. Null for non-language events and
        /// for legacy callers that use the string-only constructors.
        /// </summary>
        public CommunicationInput CommunicationInput;

        public ExternalInputEvent(
            ExternalInputType type,
            ExternalInputPriority priority,
            string payload,
            float time
        )
            : this(
                type,
                priority,
                payload,
                time,
                "Unknown"
            )
        {
        }

        public ExternalInputEvent(
            ExternalInputType type,
            ExternalInputPriority priority,
            string payload,
            float time,
            string source
        )
        {
            Type = type;
            Priority = priority;
            Payload = payload;
            Time = time;
            Source = string.IsNullOrWhiteSpace(source)
                ? "Unknown"
                : source;
            CommunicationInput = null;
        }

        public ExternalInputEvent(
            ExternalInputType type,
            ExternalInputPriority priority,
            CommunicationInput communicationInput,
            float time
        )
        {
            Type = type;
            Priority = priority;
            CommunicationInput = communicationInput;
            Payload = communicationInput != null
                ? communicationInput.NormalizedText
                : string.Empty;
            Time = time;
            Source = communicationInput != null
                ? communicationInput.Source.ToString()
                : "Unknown";
        }
    }
}
