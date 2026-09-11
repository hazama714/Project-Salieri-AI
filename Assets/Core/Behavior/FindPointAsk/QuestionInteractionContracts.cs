// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Core.Input;
using SalieriAI.Core.Perception.VisualSnapshots;

namespace SalieriAI.Core.Behavior.FindPointAsk
{
    public enum QuestionContextStatus
    {
        Created = 0,
        Active = 1,
        Answered = 2,
        Expired = 3,
        Cancelled = 4,
        Failed = 5
    }

    public enum QuestionType
    {
        Unknown = 0,
        IdentifyObject = 1
    }

    public enum QuestionSpeechLifecycleState
    {
        None = 0,
        Requested = 1,
        Started = 2,
        Completed = 3,
        Failed = 4,
        Cancelled = 5
    }

    /// <summary>
    /// ASK前に確定する、特定質問のimmutable context。
    /// ObservationReferenceは既存ObservationTarget snapshotへの参照文字列であり、
    /// 現在のAttentionTargetを後から参照しない。
    /// </summary>
    public sealed class QuestionContext
    {
        public string QuestionId { get; }
        public string BehaviorRunId { get; }
        public string TargetKey { get; }
        public int TrackId { get; }
        public string ObservationReference { get; }
        public QuestionType QuestionType { get; }
        public DateTime AskedAt { get; }
        public string RecallKeyCandidate { get; }
        public QuestionContextStatus Status { get; }
        public VisualCropSnapshot VisualCropSnapshot { get; }

        public QuestionContext(
            string questionId,
            string behaviorRunId,
            string targetKey,
            int trackId,
            string observationReference,
            QuestionType questionType,
            DateTime askedAt,
            string recallKeyCandidate,
            QuestionContextStatus status = QuestionContextStatus.Created,
            VisualCropSnapshot visualCropSnapshot = null)
        {
            QuestionId = Normalize(questionId);
            BehaviorRunId = Normalize(behaviorRunId);
            TargetKey = Normalize(targetKey);
            TrackId = trackId;
            ObservationReference = Normalize(observationReference);
            QuestionType = questionType;
            AskedAt = NormalizeUtc(askedAt);
            RecallKeyCandidate = Normalize(recallKeyCandidate);
            Status = status;
            VisualCropSnapshot = visualCropSnapshot;
        }

        public bool IsStructurallyValid =>
            QuestionId.Length > 0 &&
            BehaviorRunId.Length > 0 &&
            TargetKey.Length > 0 &&
            TrackId >= 0 &&
            ObservationReference.Length > 0 &&
            QuestionType != QuestionType.Unknown &&
            AskedAt != default(DateTime);

        internal QuestionContext WithStatus(QuestionContextStatus status)
        {
            return new QuestionContext(
                QuestionId,
                BehaviorRunId,
                TargetKey,
                TrackId,
                ObservationReference,
                QuestionType,
                AskedAt,
                RecallKeyCandidate,
                status,
                VisualCropSnapshot);
        }

        /// <summary>
        /// M4-P0 production boundary。exact SourceFrame cropを凍結するが、
        /// RecallKey/VisualSignatureはまだ生成しない。
        /// </summary>
        public static QuestionContext CreateWithVisualEvidence(
            string questionId,
            string behaviorRunId,
            string targetKey,
            int trackId,
            string observationReference,
            QuestionType questionType,
            DateTime askedAt,
            VisualCropSnapshot visualCropSnapshot)
        {
            return CreateWithVisualEvidence(
                questionId,
                behaviorRunId,
                targetKey,
                trackId,
                observationReference,
                questionType,
                askedAt,
                visualCropSnapshot,
                string.Empty);
        }

        public static QuestionContext CreateWithVisualEvidence(
            string questionId,
            string behaviorRunId,
            string targetKey,
            int trackId,
            string observationReference,
            QuestionType questionType,
            DateTime askedAt,
            VisualCropSnapshot visualCropSnapshot,
            string recallKeyCandidate)
        {
            return new QuestionContext(
                questionId,
                behaviorRunId,
                targetKey,
                trackId,
                observationReference,
                questionType,
                askedAt,
                recallKeyCandidate,
                QuestionContextStatus.Created,
                visualCropSnapshot);
        }

        private static string Normalize(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            if (value == default(DateTime))
                return value;
            return value.Kind == DateTimeKind.Utc
                ? value
                : value.ToUniversalTime();
        }
    }

    /// <summary>
    /// QuestionContextに対して1件の入力が回答として受理された事実。
    /// InputSourceはprovenanceであり、回答可否の意味判定には使用しない。
    /// Experience binding/storageそのものではない。
    /// </summary>
    public class QuestionAnswerReceipt
    {
        public string InputId { get; }
        public string QuestionId { get; }
        public string RawText { get; }
        public string Text { get; }
        public UserInputSource InputSource { get; }
        public DateTime ReceivedAt { get; }
        public QuestionContext QuestionContextSnapshot { get; }

        public QuestionAnswerReceipt(
            string inputId,
            string questionId,
            string rawText,
            UserInputSource inputSource,
            DateTime receivedAt,
            QuestionContext questionContextSnapshot)
        {
            InputId = inputId == null
                ? string.Empty
                : inputId.Trim();
            QuestionId = questionId == null
                ? string.Empty
                : questionId.Trim();
            RawText = rawText ?? string.Empty;
            Text = RawText.Trim();
            InputSource = inputSource;
            ReceivedAt = receivedAt.Kind == DateTimeKind.Utc
                ? receivedAt
                : receivedAt.ToUniversalTime();
            QuestionContextSnapshot = questionContextSnapshot;
        }
    }

    /// <summary>
    /// M1 EditorManual injectionの互換receipt。
    /// Production入力も共通QuestionAnswerReceiptへ収束するが、
    /// この型と決定論的Editor入口はSelf Test用に維持する。
    /// </summary>
    public sealed class EditorManualAnswerReceipt : QuestionAnswerReceipt
    {
        public EditorManualAnswerReceipt(
            string questionId,
            string text,
            DateTime receivedAt)
            : this(questionId, text, receivedAt, null)
        {
        }

        public EditorManualAnswerReceipt(
            string questionId,
            string rawText,
            DateTime receivedAt,
            QuestionContext questionContextSnapshot)
            : base(
                string.Empty,
                questionId,
                rawText,
                UserInputSource.EditorManual,
                receivedAt,
                questionContextSnapshot)
        {
        }
    }
}
