// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SalieriAI.Core.Execution.Orchestration.Contracts
{
    internal static class ExecutionContractUtility
    {
        public static string Text(string value)
        {
            return value ?? string.Empty;
        }

        public static DateTime Utc(DateTime value)
        {
            if (value == default(DateTime) ||
                value.Kind == DateTimeKind.Utc)
            {
                return value;
            }

            return value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        public static DateTime? Utc(DateTime? value)
        {
            return value.HasValue ? Utc(value.Value) : (DateTime?)null;
        }

        public static IReadOnlyList<T> ReadOnlyCopy<T>(
            IEnumerable<T> source)
        {
            return new ReadOnlyCollection<T>(
                source != null
                    ? new List<T>(source)
                    : new List<T>()
            );
        }
    }

    public static class ExecutionResourceIds
    {
        public const string SpeechOutput = "SpeechOutput";
        public const string LlmGeneration = "LlmGeneration";
        public const string VisionLight = "VisionLight";
        public const string VisionHeavy = "VisionHeavy";
        public const string HeadMotion = "HeadMotion";
        public const string ArmMotion = "ArmMotion";
        public const string CrawlerMotion = "CrawlerMotion";
        public const string VirtualBody = "VirtualBody";
        public const string PhysicalTransport = "PhysicalTransport";
    }

    public static class ExecutionDomainStageIds
    {
        public const string RequestAccepted = "REQUEST_ACCEPTED";
        public const string ExecutionStarted = "EXECUTION_STARTED";
        public const string PlaybackStarted = "PLAYBACK_STARTED";
        public const string PlaybackCompleted = "PLAYBACK_COMPLETED";
        public const string VrmTargetApplied = "VRM_TARGET_APPLIED";
        public const string VirtualBodyResolved = "VIRTUAL_BODY_RESOLVED";
        public const string PhysicalDispatched = "PHYSICAL_DISPATCHED";
        public const string PhysicalCompletionVerified =
            "PHYSICAL_COMPLETION_VERIFIED";
        public const string GoalConditionSatisfied =
            "GOAL_CONDITION_SATISFIED";
        public const string ContactFeedbackReceived =
            "CONTACT_FEEDBACK_RECEIVED";
        public const string LogicalNoOpCompleted =
            "LOGICAL_NO_OP_COMPLETED";
    }

    public sealed class ExecutionDomainStage
    {
        public string StageId { get; }

        public ExecutionDomainStage(string stageId)
        {
            StageId = ExecutionContractUtility.Text(stageId);
        }
    }
}
