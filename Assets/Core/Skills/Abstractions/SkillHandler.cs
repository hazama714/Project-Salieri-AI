// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Threading.Tasks;

namespace SalieriAI.Core.Skills
{
    /// <summary>
    /// Type-safe base class for Skill handlers.
    /// Registry and runner access it only through ISkillHandler.
    /// </summary>
    public abstract class SkillHandler<TInput, TOutput> : ISkillHandler
        where TInput : ISkillPayload
        where TOutput : ISkillPayload
    {
        public string SkillId { get; }

        public int Version { get; }

        public string InputPayloadTypeId { get; }

        public string OutputPayloadTypeId { get; }

        public Type InputPayloadType => typeof(TInput);

        public Type OutputPayloadType => typeof(TOutput);

        protected SkillHandler(
            string skillId,
            int version,
            string inputPayloadTypeId,
            string outputPayloadTypeId)
        {
            SkillId = skillId == null ? string.Empty : skillId.Trim();
            Version = version;
            InputPayloadTypeId = inputPayloadTypeId == null
                ? string.Empty
                : inputPayloadTypeId.Trim();
            OutputPayloadTypeId = outputPayloadTypeId == null
                ? string.Empty
                : outputPayloadTypeId.Trim();
        }

        public async Task<SkillExecutionResult> ExecuteUntypedAsync(
            ISkillPayload input,
            SkillExecutionContext context)
        {
            DateTime startedAt = DateTime.UtcNow;

            if (context == null)
            {
                return SkillExecutionResult.InvalidInput(
                    "SkillExecutionContext is null.",
                    startedAt
                );
            }

            if (!(input is TInput typedInput))
            {
                string actualType = input == null
                    ? "null"
                    : input.GetType().FullName;

                return SkillExecutionResult.InvalidInput(
                    "Skill input type mismatch. expected=" +
                    typeof(TInput).FullName +
                    " actual=" + actualType,
                    startedAt
                );
            }

            try
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                SkillExecutionResult<TOutput> result =
                    await ExecuteTypedAsync(typedInput, context);

                context.CancellationToken.ThrowIfCancellationRequested();

                if (result == null)
                {
                    return SkillExecutionResult.Failed(
                        "null_result",
                        "Skill handler returned a null result.",
                        startedAt
                    );
                }

                if (result.Status == SkillExecutionStatus.Succeeded &&
                    result.Payload == null)
                {
                    return SkillExecutionResult.Failed(
                        "null_success_payload",
                        "Succeeded Skill result must contain an output payload.",
                        startedAt
                    );
                }

                result.EnsureTiming(startedAt);
                return result;
            }
            catch (OperationCanceledException)
            {
                return SkillExecutionResult.Cancelled(
                    "Skill execution was cancelled.",
                    startedAt
                );
            }
            catch (Exception ex)
            {
                return SkillExecutionResult.Failed(
                    "handler_exception",
                    ex.GetType().Name + ": " + ex.Message,
                    startedAt
                );
            }
        }

        protected abstract Task<SkillExecutionResult<TOutput>>
            ExecuteTypedAsync(
                TInput input,
                SkillExecutionContext context
            );
    }
}
