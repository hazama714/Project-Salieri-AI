// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Reflex.Cognitive
{
    /// <summary>
    /// Phase 9-R-2 prepare-only mapper.
    ///
    /// ProgrammedActionCandidate を ExecutionRequest へ変換する。
    ///
    /// 注意:
    /// - ここでは実行しない。
    /// - ExecutionController.TryStartExecution() は呼ばない。
    /// - BodyActionExecutor / NeckController / VoiceController は呼ばない。
    /// - NeedsContext / NeedsTarget / NeedsDirection などは ExecutionRequest 化しない。
    /// </summary>
    public static class ProgrammedActionExecutionMapper
    {
        private const string SourceEventTypeUserSpeech = "UserSpeech";

        public static bool TryCreate(
            ProgrammedActionCandidate candidate,
            string sourcePayload,
            out global::ExecutionRequest request,
            out string skipReason
        )
        {
            return TryCreate(
                candidate,
                SourceEventTypeUserSpeech,
                sourcePayload,
                0,
                out request,
                out skipReason
            );
        }

        public static bool TryCreate(
            ProgrammedActionCandidate candidate,
            string sourceEventType,
            string sourcePayload,
            int priority,
            out global::ExecutionRequest request,
            out string skipReason
        )
        {
            request = null;
            skipReason = string.Empty;

            if (candidate == null)
            {
                skipReason = "candidate is null";
                return false;
            }

            if (!candidate.HasCandidate)
            {
                skipReason = "candidate has no primary action";
                return false;
            }

            if (candidate.RequiresAdditionalContext)
            {
                skipReason =
                    "candidate requires additional context. primary=" +
                    candidate.PrimaryAction;
                return false;
            }

            string actionId = MapActionId(candidate.PrimaryAction);
            if (string.IsNullOrWhiteSpace(actionId))
            {
                skipReason =
                    "primary action is not executable yet. primary=" +
                    candidate.PrimaryAction;
                return false;
            }

            request = global::ExecutionRequest.Create(
                actionId: actionId,
                sourceEventType: string.IsNullOrWhiteSpace(sourceEventType)
                    ? SourceEventTypeUserSpeech
                    : sourceEventType,
                sourcePayload: sourcePayload ?? string.Empty,
                reason:
                    "Phase9-R-2 ProgrammedAction prepared-only. " +
                    "primary=" + candidate.PrimaryAction +
                    " secondary=" + candidate.SecondaryAction +
                    " candidateReason=" + (candidate.Reason ?? string.Empty),
                priority: priority
            );

            ApplyResourceFlags(request, candidate.PrimaryAction);

            return true;
        }

        private static string MapActionId(ProgrammedActionType action)
        {
            switch (action)
            {
                case ProgrammedActionType.None:
                    return "none";

                case ProgrammedActionType.LookAtUser:
                    return "lookAtUser";

                case ProgrammedActionType.LookAtUserOrSearch:
                    return "lookAtUserOrSearch";

                case ProgrammedActionType.SearchUser:
                    return "searchUser";

                case ProgrammedActionType.LightSearch:
                    return "lightSearch";

                case ProgrammedActionType.LookAround:
                    return "lookAround";

                case ProgrammedActionType.ReturnCenter:
                    return "returnCenter";

                case ProgrammedActionType.IdleNod:
                    return "idleNod";

                case ProgrammedActionType.Hold:
                    return "hold";

                case ProgrammedActionType.Stop:
                    return "stop";

                default:
                    return string.Empty;
            }
        }

        private static void ApplyResourceFlags(
            global::ExecutionRequest request,
            ProgrammedActionType action
        )
        {
            if (request == null)
            {
                return;
            }

            switch (action)
            {
                case ProgrammedActionType.LookAtUser:
                case ProgrammedActionType.LookAtUserOrSearch:
                case ProgrammedActionType.SearchUser:
                case ProgrammedActionType.LightSearch:
                case ProgrammedActionType.LookAround:
                case ProgrammedActionType.ReturnCenter:
                case ProgrammedActionType.IdleNod:
                    request.RequiresBody = true;
                    request.RequiresSpeech = false;
                    request.RequiresExpression = false;
                    request.RequiresDevice = false;
                    break;

                case ProgrammedActionType.Hold:
                case ProgrammedActionType.Stop:
                    request.RequiresBody = false;
                    request.RequiresSpeech = false;
                    request.RequiresExpression = false;
                    request.RequiresDevice = false;
                    break;

                case ProgrammedActionType.None:
                    request.RequiresBody = false;
                    request.RequiresSpeech = false;
                    request.RequiresExpression = false;
                    request.RequiresDevice = false;
                    break;
            }
        }
    }
}
