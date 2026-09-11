// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Core.Input;

namespace SalieriAI.Core.Language.Contracts
{
    /// <summary>
    /// Immutable Phase 1 envelope for one admitted language input.
    /// It keeps the raw input and the normalized routing text separate.
    /// </summary>
    [Serializable]
    public sealed class CommunicationInput
    {
        public string InputId { get; }

        public string InteractionId { get; }

        public UserInputSource Source { get; }

        public string RawText { get; }

        public string NormalizedText { get; }

        public string SessionId { get; }

        public int LifecycleGeneration { get; }

        public DateTime TimestampUtc { get; }

        public float Confidence { get; }

        public float DurationSeconds { get; }

        public bool ActivationMatched { get; }

        public string CglVersion { get; }

        public CommunicationInput(
            string inputId,
            string interactionId,
            UserInputSource source,
            string rawText,
            string normalizedText,
            string sessionId,
            int lifecycleGeneration,
            DateTime timestampUtc,
            float confidence,
            float durationSeconds,
            bool activationMatched,
            string cglVersion)
        {
            InputId = NormalizeId(inputId, "input");
            InteractionId = NormalizeId(interactionId, "interaction");
            Source = source;
            RawText = rawText ?? string.Empty;
            NormalizedText = normalizedText ?? string.Empty;
            SessionId = sessionId ?? string.Empty;
            LifecycleGeneration = lifecycleGeneration;
            TimestampUtc = NormalizeUtc(timestampUtc);
            Confidence = confidence;
            DurationSeconds = durationSeconds;
            ActivationMatched = activationMatched;
            CglVersion = cglVersion ?? string.Empty;
        }

        public static CommunicationInput CreateAccepted(
            string rawText,
            string normalizedText,
            UserInputSource source,
            string sessionId = "",
            int lifecycleGeneration = 0,
            float confidence = -1.0f,
            float durationSeconds = -1.0f)
        {
            return new CommunicationInput(
                CommunicationIdGenerator.Create("input"),
                CommunicationIdGenerator.Create("interaction"),
                source,
                rawText,
                normalizedText,
                sessionId,
                lifecycleGeneration,
                DateTime.UtcNow,
                confidence,
                durationSeconds,
                false,
                string.Empty
            );
        }

        public CommunicationInput WithActivationMatched(bool matched)
        {
            return new CommunicationInput(
                InputId,
                InteractionId,
                Source,
                RawText,
                NormalizedText,
                SessionId,
                LifecycleGeneration,
                TimestampUtc,
                Confidence,
                DurationSeconds,
                matched,
                CglVersion
            );
        }

        private static string NormalizeId(string value, string prefix)
        {
            return string.IsNullOrWhiteSpace(value)
                ? CommunicationIdGenerator.Create(prefix)
                : value.Trim();
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            DateTime safe = value == default(DateTime)
                ? DateTime.UtcNow
                : value;

            return safe.Kind == DateTimeKind.Utc
                ? safe
                : safe.ToUniversalTime();
        }
    }

    public static class CommunicationIdGenerator
    {
        public static string Create(string prefix)
        {
            string safePrefix = string.IsNullOrWhiteSpace(prefix)
                ? "id"
                : prefix.Trim();

            return safePrefix + "_" + Guid.NewGuid().ToString("N");
        }
    }
}
