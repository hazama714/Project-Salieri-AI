// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Threading.Tasks;

using SalieriAI.Core.Perception.ObjectTargeting;

namespace SalieriAI.Core.Skills.Perception.ObjectTargeting
{
    /// <summary>
    /// ObservationTargetSelectionServiceが継続評価している現在対象を、
    /// 1回のSkill実行結果として取得する。
    ///
    /// 選択ロジックは再実装しない。
    /// UI、首、眼球、腕、発話、DBは操作しない。
    /// </summary>
    public sealed class SelectObservationTargetSkill :
        SkillHandler<
            SelectObservationTargetRequest,
            SelectObservationTargetResult>
    {
        public const string Id =
            "select_observation_target";

        public const int CurrentVersion = 1;

        public const string InputTypeId =
            "select_observation_target_request";

        public const string OutputTypeId =
            "select_observation_target_result";

        private readonly ObservationTargetSelectionService
            selectionService;

        public SelectObservationTargetSkill(
            ObservationTargetSelectionService selectionService)
            : base(
                Id,
                CurrentVersion,
                InputTypeId,
                OutputTypeId)
        {
            this.selectionService = selectionService;
        }

        protected override Task<
            SkillExecutionResult<
                SelectObservationTargetResult>>
            ExecuteTypedAsync(
                SelectObservationTargetRequest input,
                SkillExecutionContext context)
        {
            DateTime startedAt = DateTime.UtcNow;

            context.CancellationToken
                .ThrowIfCancellationRequested();

            if (selectionService == null ||
                !selectionService.isActiveAndEnabled)
            {
                return Task.FromResult(
                    SkillExecutionResult<
                        SelectObservationTargetResult>
                    .Create(
                        SkillExecutionStatus
                            .ResourceUnavailable,
                        failureReason:
                            "observation_target_selection_service_unavailable",
                        message:
                            "ObservationTargetSelectionService is unavailable.",
                        startedAt:
                            startedAt,
                        retryable:
                            true
                    )
                );
            }

            ObservationTarget target =
                selectionService.CurrentTarget;

            if (target == null ||
                target.TrackSnapshot == null)
            {
                return Task.FromResult(
                    SkillExecutionResult<
                        SelectObservationTargetResult>
                    .Create(
                        SkillExecutionStatus.TargetLost,
                        failureReason:
                            "observation_target_not_available",
                        message:
                            "No stable observation target is currently available.",
                        startedAt:
                            startedAt,
                        retryable:
                            true
                    )
                );
            }

            string expectedTargetKey =
                input.ExpectedTargetKey == null
                    ? string.Empty
                    : input.ExpectedTargetKey.Trim();

            if (expectedTargetKey.Length > 0 &&
                !string.Equals(
                    expectedTargetKey,
                    target.TargetKey,
                    StringComparison.Ordinal))
            {
                return Task.FromResult(
                    SkillExecutionResult<
                        SelectObservationTargetResult>
                    .Create(
                        SkillExecutionStatus.TargetLost,
                        failureReason:
                            "expected_observation_target_not_current",
                        message:
                            "The expected observation target is no longer current.",
                        startedAt:
                            startedAt,
                        retryable:
                            true
                    )
                );
            }

            SelectObservationTargetResult payload =
                SelectObservationTargetResult
                    .FromTarget(target);

            if (payload == null)
            {
                return Task.FromResult(
                    SkillExecutionResult<
                        SelectObservationTargetResult>
                    .Failed(
                        "observation_target_snapshot_invalid",
                        "The current observation target could not be copied.",
                        startedAt
                    )
                );
            }

            return Task.FromResult(
                SkillExecutionResult<
                    SelectObservationTargetResult>
                .Succeeded(
                    payload,
                    "Current observation target captured.",
                    startedAt
                )
            );
        }
    }
}
