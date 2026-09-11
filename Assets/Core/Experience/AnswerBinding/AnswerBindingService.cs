// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;

using SalieriAI.Core.Behavior.FindPointAsk;

namespace SalieriAI.Core.Experience.AnswerBinding
{
    /// <summary>
    /// M1 AnswerReceivedをQuestion snapshotへ決定論的にbindする。
    /// DB、Recall、AttentionTarget、Unity runtime状態は参照しない。
    /// </summary>
    public sealed class AnswerBindingService
    {
        private readonly HashSet<string> matchedQuestionIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> issuedAnswerIds =
            new HashSet<string>(StringComparer.Ordinal);

        public AnswerBindingResult Bind(QuestionAnswerReceipt receipt)
        {
            if (receipt == null || receipt.QuestionContextSnapshot == null)
            {
                return AnswerBindingResult.Rejected(
                    CorrelationStatus.NoActiveQuestion,
                    "AnswerReceived has no QuestionContext snapshot.");
            }

            QuestionContext question = receipt.QuestionContextSnapshot;
            if (question.Status == QuestionContextStatus.Expired)
            {
                return AnswerBindingResult.Rejected(
                    CorrelationStatus.QuestionExpired,
                    "QuestionContext is expired.");
            }

            if (question.Status == QuestionContextStatus.Cancelled)
            {
                return AnswerBindingResult.Rejected(
                    CorrelationStatus.QuestionCancelled,
                    "QuestionContext is cancelled.");
            }

            if (question.Status == QuestionContextStatus.Answered ||
                matchedQuestionIds.Contains(question.QuestionId))
            {
                return AnswerBindingResult.Rejected(
                    CorrelationStatus.AlreadyAnswered,
                    "QuestionContext already has a matched answer.");
            }

            if (question.Status != QuestionContextStatus.Active)
            {
                return AnswerBindingResult.Rejected(
                    CorrelationStatus.NoActiveQuestion,
                    "QuestionContext is not Active.");
            }

            string normalized = NormalizeAnswer(receipt.RawText);
            if (normalized.Length == 0)
            {
                return AnswerBindingResult.Rejected(
                    CorrelationStatus.InvalidAnswer,
                    "Answer transcript is empty after normalization.");
            }

            if (string.IsNullOrWhiteSpace(question.QuestionId) ||
                string.IsNullOrWhiteSpace(question.BehaviorRunId) ||
                !string.Equals(
                    question.QuestionId,
                    receipt.QuestionId,
                    StringComparison.Ordinal))
            {
                return AnswerBindingResult.Rejected(
                    CorrelationStatus.TargetCorrelationFailed,
                    "Question identity does not match the receipt.");
            }

            var target = new TargetContext(question);
            if (!target.HasSufficientEvidence)
            {
                return AnswerBindingResult.Rejected(
                    CorrelationStatus.TargetCorrelationFailed,
                    "Frozen target evidence is insufficient.");
            }

            string answerId = CreateUniqueAnswerId();
            var context = new AnswerContext(
                answerId,
                question,
                target,
                receipt.RawText,
                normalized,
                receipt.InputSource,
                receipt.ReceivedAt);

            if (!context.IsMatched)
            {
                return AnswerBindingResult.Rejected(
                    CorrelationStatus.TargetCorrelationFailed,
                    "AnswerContext failed structural validation.");
            }

            matchedQuestionIds.Add(question.QuestionId);
            return AnswerBindingResult.Matched(context);
        }

        public static string NormalizeAnswer(string rawTranscript)
        {
            return rawTranscript == null
                ? string.Empty
                : rawTranscript.Trim();
        }

        private string CreateUniqueAnswerId()
        {
            for (int attempt = 0; attempt < 4; attempt++)
            {
                string answerId = Guid.NewGuid().ToString("N");
                if (issuedAnswerIds.Add(answerId))
                    return answerId;
            }

            throw new InvalidOperationException(
                "Unable to allocate a unique AnswerId.");
        }
    }
}
