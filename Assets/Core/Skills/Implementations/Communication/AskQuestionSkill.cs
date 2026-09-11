// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Threading.Tasks;

namespace SalieriAI.Core.Skills.Communication
{
    public interface IQuestionSpeechRequestSink
    {
        bool TryPublish(string text, out string error);
    }

    /// <summary>
    /// 既存ResponseBus -> VoicePlaybackController経路へ発話要求を公開する。
    /// </summary>
    public sealed class ResponseBusQuestionSpeechRequestSink :
        IQuestionSpeechRequestSink
    {
        public bool TryPublish(string text, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Speech text is empty.";
                return false;
            }

            global::ResponseBus.Raise(text.Trim());
            return true;
        }
    }

    [Serializable]
    public sealed class AskQuestionRequest : ISkillPayload
    {
        public string Text = string.Empty;

        public AskQuestionRequest()
        {
        }

        public AskQuestionRequest(string text)
        {
            Text = text ?? string.Empty;
        }
    }

    [Serializable]
    public sealed class AskQuestionResult : ISkillPayload
    {
        public string RequestedText = string.Empty;
    }

    public sealed class AskQuestionSkill :
        SkillHandler<AskQuestionRequest, AskQuestionResult>
    {
        public const string Id = "ask_question";
        public const int CurrentVersion = 1;

        private readonly IQuestionSpeechRequestSink speechSink;

        public AskQuestionSkill(IQuestionSpeechRequestSink speechSink)
            : base(
                Id,
                CurrentVersion,
                "ask_question_request",
                "ask_question_result")
        {
            this.speechSink = speechSink;
        }

        protected override Task<SkillExecutionResult<AskQuestionResult>>
            ExecuteTypedAsync(
                AskQuestionRequest input,
                SkillExecutionContext context)
        {
            DateTime startedAt = DateTime.UtcNow;
            string text = input.Text == null
                ? string.Empty
                : input.Text.Trim();

            if (text.Length == 0)
            {
                return Task.FromResult(
                    SkillExecutionResult<AskQuestionResult>.InvalidInput(
                        "Question text is empty.",
                        startedAt));
            }

            if (speechSink == null)
            {
                return Task.FromResult(
                    SkillExecutionResult<AskQuestionResult>.Failed(
                        "speech_request_rejected",
                        "Question speech request sink is unavailable.",
                        startedAt));
            }

            if (!speechSink.TryPublish(text, out string error))
            {
                return Task.FromResult(
                    SkillExecutionResult<AskQuestionResult>.Failed(
                        "speech_request_rejected",
                        error,
                        startedAt));
            }

            return Task.FromResult(
                SkillExecutionResult<AskQuestionResult>.Succeeded(
                    new AskQuestionResult
                    {
                        RequestedText = text
                    },
                    "Question speech was requested.",
                    startedAt));
        }
    }
}
