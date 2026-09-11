// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Core.Behavior.FindPointAsk;
using SalieriAI.Core.Input;

namespace SalieriAI.Core.Experience.AnswerBinding
{
    public enum CorrelationStatus
    {
        Matched = 0,
        NoActiveQuestion = 1,
        QuestionExpired = 2,
        QuestionCancelled = 3,
        AlreadyAnswered = 4,
        TargetCorrelationFailed = 5,
        InvalidAnswer = 6
    }

    /// <summary>
    /// QuestionContextで凍結されたTarget evidenceのコピー。
    /// TrackId/TargetKeyを永続Object IDとして解釈しない。
    /// </summary>
    public sealed class TargetContext
    {
        public string TargetKey { get; }
        public int TrackId { get; }
        public string ObservationReference { get; }
        public QuestionType QuestionType { get; }
        public string RecallKeyCandidate { get; }

        public TargetContext(QuestionContext question)
        {
            TargetKey = question != null ? question.TargetKey : string.Empty;
            TrackId = question != null ? question.TrackId : -1;
            ObservationReference = question != null
                ? question.ObservationReference
                : string.Empty;
            QuestionType = question != null
                ? question.QuestionType
                : QuestionType.Unknown;
            RecallKeyCandidate = question != null
                ? question.RecallKeyCandidate
                : string.Empty;
        }

        public bool HasSufficientEvidence =>
            !string.IsNullOrWhiteSpace(TargetKey) &&
            TrackId >= 0 &&
            !string.IsNullOrWhiteSpace(ObservationReference) &&
            QuestionType != QuestionType.Unknown;
    }

    public sealed class AnswerContext
    {
        public string AnswerId { get; }
        public string QuestionId { get; }
        public string BehaviorRunId { get; }
        public TargetContext TargetContext { get; }
        public string RawTranscript { get; }
        public string NormalizedAnswer { get; }
        public UserInputSource InputSource { get; }
        public DateTime ReceivedAt { get; }
        public CorrelationStatus CorrelationStatus { get; }

        public AnswerContext(
            string answerId,
            QuestionContext question,
            TargetContext targetContext,
            string rawTranscript,
            string normalizedAnswer,
            UserInputSource inputSource,
            DateTime receivedAt)
        {
            AnswerId = Normalize(answerId);
            QuestionId = question != null
                ? question.QuestionId
                : string.Empty;
            BehaviorRunId = question != null
                ? question.BehaviorRunId
                : string.Empty;
            TargetContext = targetContext;
            RawTranscript = rawTranscript ?? string.Empty;
            NormalizedAnswer = normalizedAnswer ?? string.Empty;
            InputSource = inputSource;
            ReceivedAt = receivedAt.Kind == DateTimeKind.Utc
                ? receivedAt
                : receivedAt.ToUniversalTime();
            CorrelationStatus = CorrelationStatus.Matched;
        }

        public bool IsMatched =>
            CorrelationStatus == CorrelationStatus.Matched &&
            AnswerId.Length > 0 &&
            QuestionId.Length > 0 &&
            BehaviorRunId.Length > 0 &&
            TargetContext != null &&
            TargetContext.HasSufficientEvidence &&
            NormalizedAnswer.Length > 0;

        private static string Normalize(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }
    }

    public sealed class AnswerBindingResult
    {
        public CorrelationStatus Status { get; }
        public AnswerContext AnswerContext { get; }
        public string Reason { get; }
        public bool CanProceedToExperience =>
            Status == CorrelationStatus.Matched &&
            AnswerContext != null &&
            AnswerContext.IsMatched;

        private AnswerBindingResult(
            CorrelationStatus status,
            AnswerContext answerContext,
            string reason)
        {
            Status = status;
            AnswerContext = answerContext;
            Reason = reason ?? string.Empty;
        }

        public static AnswerBindingResult Matched(AnswerContext context)
        {
            return new AnswerBindingResult(
                CorrelationStatus.Matched,
                context,
                string.Empty);
        }

        public static AnswerBindingResult Rejected(
            CorrelationStatus status,
            string reason)
        {
            return new AnswerBindingResult(status, null, reason);
        }
    }
}
