// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Core.Input;

namespace SalieriAI.Core.Behavior.FindPointAsk
{
    /// <summary>
    /// 1つのQuestionContext、対応Speech instance、最初のEditor回答だけを所有する。
    /// Speech実行、Conversation、Experience保存は行わない。
    /// </summary>
    public sealed class QuestionInteractionSession
    {
        public QuestionContext CurrentQuestion { get; private set; }
        public QuestionSpeechLifecycleState SpeechState { get; private set; }
        public string SpeechInstanceId { get; private set; }
        public QuestionAnswerReceipt AcceptedAnswer { get; private set; }

        public bool IsWaitingForAnswer =>
            CurrentQuestion != null &&
            CurrentQuestion.Status == QuestionContextStatus.Active &&
            SpeechState == QuestionSpeechLifecycleState.Completed;

        public bool TryCreateQuestion(
            QuestionContext context,
            out string error)
        {
            error = string.Empty;
            if (CurrentQuestion != null)
            {
                error = "A QuestionContext already exists for this session.";
                return false;
            }

            if (context == null ||
                !context.IsStructurallyValid ||
                context.Status != QuestionContextStatus.Created)
            {
                error = "QuestionContext is invalid or is not Created.";
                return false;
            }

            CurrentQuestion = context;
            SpeechState = QuestionSpeechLifecycleState.None;
            SpeechInstanceId = string.Empty;
            AcceptedAnswer = null;
            return true;
        }

        public bool TryObserveSpeech(
            string speechInstanceId,
            QuestionSpeechLifecycleState next,
            out string error)
        {
            error = string.Empty;
            string normalizedId = Normalize(speechInstanceId);
            if (CurrentQuestion == null)
            {
                error = "QuestionContext has not been created.";
                return false;
            }

            if (normalizedId.Length == 0)
            {
                error = "Speech instance id is empty.";
                return false;
            }

            if (IsTerminalQuestion(CurrentQuestion.Status))
            {
                error = "QuestionContext is already terminal.";
                return false;
            }

            if (next == QuestionSpeechLifecycleState.Requested)
            {
                if (SpeechState != QuestionSpeechLifecycleState.None)
                {
                    error = "SpeechRequested is duplicate or out of order.";
                    return false;
                }

                SpeechInstanceId = normalizedId;
                SpeechState = next;
                return true;
            }

            if (!string.Equals(
                    SpeechInstanceId,
                    normalizedId,
                    StringComparison.Ordinal))
            {
                error = "Speech instance does not match the active question.";
                return false;
            }

            if (next == QuestionSpeechLifecycleState.Started)
            {
                if (SpeechState != QuestionSpeechLifecycleState.Requested)
                {
                    error = "SpeechStarted is duplicate or out of order.";
                    return false;
                }

                SpeechState = next;
                return true;
            }

            if (next == QuestionSpeechLifecycleState.Completed)
            {
                if (SpeechState != QuestionSpeechLifecycleState.Started)
                {
                    error = "SpeechCompleted is duplicate or out of order.";
                    return false;
                }

                SpeechState = next;
                CurrentQuestion = CurrentQuestion.WithStatus(
                    QuestionContextStatus.Active);
                return true;
            }

            if (next == QuestionSpeechLifecycleState.Failed ||
                next == QuestionSpeechLifecycleState.Cancelled)
            {
                if (SpeechState != QuestionSpeechLifecycleState.Requested &&
                    SpeechState != QuestionSpeechLifecycleState.Started)
                {
                    error = "Speech terminal is duplicate or out of order.";
                    return false;
                }

                SpeechState = next;
                CurrentQuestion = CurrentQuestion.WithStatus(
                    next == QuestionSpeechLifecycleState.Cancelled
                        ? QuestionContextStatus.Cancelled
                        : QuestionContextStatus.Failed);
                return true;
            }

            error = "Unsupported speech lifecycle state.";
            return false;
        }

        public bool TryAcceptEditorManualAnswer(
            string text,
            DateTime receivedAtUtc,
            out EditorManualAnswerReceipt receipt,
            out string error)
        {
            receipt = null;
            if (!TryValidateAnswer(text, out error))
                return false;

            receipt = new EditorManualAnswerReceipt(
                CurrentQuestion.QuestionId,
                text,
                receivedAtUtc,
                CurrentQuestion);
            CommitAcceptedAnswer(receipt);
            return true;
        }

        public bool TryAcceptAnswer(
            string text,
            UserInputSource inputSource,
            string inputId,
            DateTime receivedAtUtc,
            out QuestionAnswerReceipt receipt,
            out string error)
        {
            receipt = null;
            if (!TryValidateAnswer(text, out error))
                return false;

            receipt = new QuestionAnswerReceipt(
                inputId,
                CurrentQuestion.QuestionId,
                text,
                inputSource,
                receivedAtUtc,
                CurrentQuestion);
            CommitAcceptedAnswer(receipt);
            return true;
        }

        public bool TryCancel(out string error)
        {
            error = string.Empty;
            if (CurrentQuestion == null)
                return false;
            if (IsTerminalQuestion(CurrentQuestion.Status))
            {
                error = "QuestionContext is already terminal.";
                return false;
            }

            CurrentQuestion = CurrentQuestion.WithStatus(
                QuestionContextStatus.Cancelled);
            if (SpeechState == QuestionSpeechLifecycleState.Requested ||
                SpeechState == QuestionSpeechLifecycleState.Started)
            {
                SpeechState = QuestionSpeechLifecycleState.Cancelled;
            }
            return true;
        }

        public bool TryExpire(out string error)
        {
            error = string.Empty;
            if (CurrentQuestion == null)
                return false;
            if (CurrentQuestion.Status != QuestionContextStatus.Active ||
                SpeechState != QuestionSpeechLifecycleState.Completed)
            {
                error = "Only an Active completed question can expire.";
                return false;
            }

            CurrentQuestion = CurrentQuestion.WithStatus(
                QuestionContextStatus.Expired);
            return true;
        }

        public bool TryFail(out string error)
        {
            error = string.Empty;
            if (CurrentQuestion == null)
                return false;
            if (IsTerminalQuestion(CurrentQuestion.Status))
            {
                error = "QuestionContext is already terminal.";
                return false;
            }

            CurrentQuestion = CurrentQuestion.WithStatus(
                QuestionContextStatus.Failed);
            if (SpeechState == QuestionSpeechLifecycleState.Requested ||
                SpeechState == QuestionSpeechLifecycleState.Started)
            {
                SpeechState = QuestionSpeechLifecycleState.Failed;
            }
            return true;
        }

        private static bool IsTerminalQuestion(QuestionContextStatus status)
        {
            return status == QuestionContextStatus.Answered ||
                status == QuestionContextStatus.Expired ||
                status == QuestionContextStatus.Cancelled ||
                status == QuestionContextStatus.Failed;
        }

        private bool TryValidateAnswer(string text, out string error)
        {
            error = string.Empty;
            if (!IsWaitingForAnswer)
            {
                error = "No Active QuestionContext is waiting for an answer.";
                return false;
            }

            if (AcceptedAnswer != null)
            {
                error = "An answer was already accepted for this question.";
                return false;
            }

            if (Normalize(text).Length == 0)
            {
                error = "Answer is empty.";
                return false;
            }

            return true;
        }

        private void CommitAcceptedAnswer(QuestionAnswerReceipt receipt)
        {
            AcceptedAnswer = receipt;
            CurrentQuestion = CurrentQuestion.WithStatus(
                QuestionContextStatus.Answered);
        }

        private static string Normalize(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }
    }
}
