// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Body.FreePose;
using SalieriAI.Core.LLM.Common;

namespace SalieriAI.Core.Reflex.Cognitive
{
    public readonly struct BodyExpressionProductionHandoffResult
    {
        public BodyExpressionPoseMappingResult Mapping { get; }
        public bool RequestAttempted { get; }
        public ProductionPoseRequestResult ProductionResult { get; }
        public string Reason { get; }

        internal BodyExpressionProductionHandoffResult(
            BodyExpressionPoseMappingResult mapping,
            bool requestAttempted,
            ProductionPoseRequestResult productionResult,
            string reason)
        {
            Mapping = mapping;
            RequestAttempted = requestAttempted;
            ProductionResult = productionResult;
            Reason = reason ?? string.Empty;
        }
    }

    /// <summary>
    /// Turn-level adapter from normalized Conversation BodyIntent to the
    /// existing Stage 3C-A Production request boundary.
    /// </summary>
    public static class BodyExpressionIntentProductionAdapter
    {
        public const string RequestSource =
            "ConversationService.BodyExpressionIntent";

        public static BodyExpressionProductionHandoffResult Handle(
            ConversationLLMResponse response,
            string turnId,
            Action<string> logSink)
        {
            ConversationLLMResponse safe =
                response ?? ConversationLLMResponse.Empty();
            safe.Normalize();

            BodyExpressionPoseMappingResult mapping =
                BodyExpressionIntentExistingPoseMapper.Map(
                    safe.overallPoseIntent,
                    safe.rightArmIntent,
                    safe.leftArmIntent);

            if (!mapping.IsSelected)
            {
                Log(logSink, safe, turnId, mapping, null,
                    "NOT_REQUESTED", mapping.Reason);
                return new BodyExpressionProductionHandoffResult(
                    mapping, false, null, mapping.Reason);
            }

            if (string.IsNullOrWhiteSpace(turnId))
            {
                const string missingTurn =
                    "body_expression_turn_id_missing";
                Log(logSink, safe, turnId, mapping, null,
                    "NOT_REQUESTED", missingTurn);
                return new BodyExpressionProductionHandoffResult(
                    mapping, false, null, missingTurn);
            }

            var request = new ProductionPoseRequest(
                turnId,
                RequestSource,
                mapping.SemanticIntent);
            bool attempted = ProductionPoseRequestRuntime.TryRequest(
                request,
                out ProductionPoseRequestResult productionResult,
                out string reason);

            Log(
                logSink,
                safe,
                turnId,
                mapping,
                productionResult,
                attempted ? "REQUESTED" : "NOT_REQUESTED",
                reason);
            return new BodyExpressionProductionHandoffResult(
                mapping,
                attempted,
                productionResult,
                reason);
        }

        private static void Log(
            Action<string> logSink,
            ConversationLLMResponse response,
            string turnId,
            BodyExpressionPoseMappingResult mapping,
            ProductionPoseRequestResult productionResult,
            string execution,
            string reason)
        {
            logSink?.Invoke(
                "[BodyPoseMapping]" +
                " TurnId=" + Text(turnId) +
                " Overall=" + response.overallPoseIntent +
                " Right=" + response.rightArmIntent +
                " Left=" + response.leftArmIntent +
                " Head=" + response.headIntent +
                " Target=" + response.targetIntent +
                " Result=" +
                (mapping.IsSelected
                    ? mapping.SemanticIntent.ToString()
                    : "NoSelection") +
                " PoseStatus=" +
                (productionResult != null
                    ? productionResult.Status.ToString()
                    : "NONE") +
                " Reason=" + Text(reason) +
                " Execution=" + execution);
        }

        private static string Text(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "<empty>"
                : value;
        }
    }
}
