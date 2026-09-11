// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;

using UnityEngine;

using SalieriAI.Core.Language.Compiler;
using SalieriAI.Core.Language.Compiler.Shadow;
using SalieriAI.Core.Language.Compiler.Validation;
using SalieriAI.Core.Language.Contracts;

namespace SalieriAI.Core.Language.Runtime
{
    /// <summary>
    /// Phase 1 append-only diagnostic logging.
    /// It owns no routing state and has no output, Skill, Body, or DB side effects.
    /// </summary>
    public static class InteractionTraceLogger
    {
        public static void LogAdmission(CommunicationInput input)
        {
            if (input == null)
                return;

            Debug.Log(
                "[InteractionTrace] stage=ADMISSION_ACCEPTED" +
                Common(input) +
                " rawText=" +
                Safe(input.RawText) +
                " normalizedText=" +
                Safe(input.NormalizedText)
            );
        }

        public static void LogRuntimeStage(
            string stage,
            CommunicationInput input,
            string detail = "")
        {
            if (input == null)
                return;

            Debug.Log(
                "[InteractionTrace] stage=" +
                Safe(stage) +
                Common(input) +
                " detail=" +
                Safe(detail)
            );
        }

        public static void LogCglShadowStarted(CommunicationInput input)
        {
            if (input == null)
                return;

            Debug.Log(
                "[InteractionTrace] stage=CGL_SHADOW_STARTED" +
                Common(input)
            );
        }

        public static void LogCglShadowCompleted(
            CommunicationInput input,
            CglInterpretation interpretation)
        {
            if (input == null || interpretation == null)
                return;

            Debug.Log(
                "[InteractionTrace] stage=CGL_SHADOW_COMPLETED" +
                Common(input) +
                " analysisId=" +
                Safe(interpretation.AnalysisId) +
                " normalizedText=" +
                Safe(interpretation.NormalizedText) +
                " activationMatched=" +
                interpretation.ActivationMatched +
                " requestType=" +
                Safe(interpretation.RequestType) +
                " actionCandidate=" +
                Safe(interpretation.ActionCandidate) +
                " directionCandidate=" +
                Safe(interpretation.DirectionCandidate) +
                " interpretationStatus=" +
                Safe(interpretation.InterpretationStatus) +
                " parserVersion=" +
                Safe(interpretation.ParserVersion)
            );
        }

        public static void LogCglShadowFailed(
            CommunicationInput input,
            Exception exception)
        {
            if (input == null)
                return;

            Debug.LogWarning(
                "[InteractionTrace] stage=CGL_SHADOW_FAILED" +
                Common(input) +
                " exceptionType=" +
                Safe(exception != null
                    ? exception.GetType().FullName
                    : string.Empty) +
                " message=" +
                Safe(exception != null
                    ? exception.Message
                    : string.Empty)
            );
        }

        public static void LogSccSemanticAdapterStarted(
            CommunicationInput input,
            CglInterpretation interpretation,
            SemanticParserSource parserSource)
        {
            if (input == null || interpretation == null)
                return;

            Debug.Log(
                "[InteractionTrace] stage=SCC_SEMANTIC_ADAPTER_STARTED" +
                Common(input) +
                " analysisId=" +
                Safe(interpretation.AnalysisId) +
                " parserSource=" +
                parserSource
            );
        }

        public static void LogSccSemanticAdapterCompleted(
            CommunicationInput input,
            SemanticCandidateSet candidateSet,
            SemanticParserSource parserSource)
        {
            if (input == null || candidateSet == null)
                return;

            Debug.Log(
                "[InteractionTrace] stage=SCC_SEMANTIC_ADAPTER_COMPLETED" +
                Common(input) +
                SemanticCommon(candidateSet, parserSource)
            );
        }

        public static void LogSccSemanticAdapterFailed(
            CommunicationInput input,
            CglInterpretation interpretation,
            SemanticParserSource parserSource,
            string failureCode,
            Exception exception)
        {
            if (input == null)
                return;

            Debug.LogWarning(
                "[InteractionTrace] stage=SCC_SEMANTIC_ADAPTER_FAILED" +
                Common(input) +
                " analysisId=" +
                Safe(interpretation != null
                    ? interpretation.AnalysisId
                    : string.Empty) +
                " candidateSetId=<empty>" +
                " parserSource=" +
                parserSource +
                " candidateCount=0" +
                " setStatus=<empty>" +
                " failureCode=" +
                Safe(failureCode) +
                " diagnosticMessage=" +
                Safe(exception != null
                    ? exception.Message
                    : string.Empty) +
                " exceptionType=" +
                Safe(exception != null
                    ? exception.GetType().FullName
                    : string.Empty)
            );
        }

        public static void LogSccSemanticShadowRegistration(
            CommunicationInput input,
            SemanticCandidateSet candidateSet,
            SemanticParserSource parserSource,
            SccSemanticShadowRegisterResult result)
        {
            if (input == null || candidateSet == null)
                return;

            string stage;
            switch (result)
            {
                case SccSemanticShadowRegisterResult.Stored:
                    stage = "SCC_SEMANTIC_SHADOW_STORED";
                    break;
                case SccSemanticShadowRegisterResult.Duplicate:
                    stage = "SCC_SEMANTIC_SHADOW_DUPLICATE";
                    break;
                default:
                    stage = "SCC_SEMANTIC_SHADOW_FAILED";
                    break;
            }

            Debug.Log(
                "[InteractionTrace] stage=" +
                stage +
                Common(input) +
                SemanticCommon(candidateSet, parserSource) +
                " failureCode=" +
                Safe(result == SccSemanticShadowRegisterResult.Invalid
                    ? "STORE_INVALID"
                    : result ==
                      SccSemanticShadowRegisterResult.CapacityRejected
                        ? "STORE_CAPACITY_REJECTED"
                        : string.Empty) +
                " diagnosticMessage=" +
                Safe(candidateSet.DiagnosticMessage)
            );
        }

        public static void LogSccSemanticStoreSnapshot(
            CommunicationInput input,
            SemanticCandidateSet candidateSet,
            SccSemanticShadowStoreDiagnosticsSnapshot snapshot,
            string boundary)
        {
            if (input == null || snapshot == null)
                return;

            Debug.Log(
                "[InteractionTrace] " +
                "stage=CGL_SEMANTIC_STORE_SNAPSHOT" +
                Common(input) +
                " analysisId=" +
                Safe(candidateSet != null
                    ? candidateSet.AnalysisId
                    : string.Empty) +
                " candidateSetId=" +
                Safe(candidateSet != null
                    ? candidateSet.CandidateSetId
                    : string.Empty) +
                " boundary=" +
                Safe(boundary) +
                StoreDiagnosticsCommon(
                    snapshot.ObservedAtUtc,
                    snapshot.StoredCount,
                    snapshot.EntryLifetime,
                    snapshot.MaximumCapacity,
                    snapshot.ExpiredEntryRemovedCount
                )
            );
        }

        public static void LogSccSemanticShadowFailed(
            CommunicationInput input,
            SemanticCandidateSet candidateSet,
            SemanticParserSource parserSource,
            string failureCode,
            Exception exception)
        {
            if (input == null)
                return;

            Debug.LogWarning(
                "[InteractionTrace] stage=SCC_SEMANTIC_SHADOW_FAILED" +
                Common(input) +
                SemanticCommon(candidateSet, parserSource) +
                " failureCode=" +
                Safe(failureCode) +
                " diagnosticMessage=" +
                Safe(exception != null
                    ? exception.Message
                    : string.Empty) +
                " exceptionType=" +
                Safe(exception != null
                    ? exception.GetType().FullName
                    : string.Empty)
            );
        }

        public static void LogCglSemanticValidationStarted(
            CommunicationInput input,
            SemanticCandidateSet candidateSet,
            string validatorVersion)
        {
            if (input == null || candidateSet == null)
                return;

            Debug.Log(
                "[InteractionTrace] " +
                "stage=CGL_SEMANTIC_VALIDATION_STARTED" +
                Common(input) +
                ValidationSetCommon(candidateSet) +
                " validatorVersion=" +
                Safe(validatorVersion)
            );
        }

        public static void LogCglSemanticValidationCompleted(
            CommunicationInput input,
            CglSemanticValidationShadowSnapshot snapshot)
        {
            if (input == null || snapshot == null)
                return;

            Debug.Log(
                "[InteractionTrace] " +
                "stage=CGL_SEMANTIC_VALIDATION_COMPLETED" +
                Common(input) +
                ValidationCommon(snapshot)
            );
        }

        public static void LogCglSemanticValidationStoreRegistration(
            CommunicationInput input,
            CglSemanticValidationShadowSnapshot snapshot,
            CglSemanticValidationShadowRegisterResult result)
        {
            if (input == null || snapshot == null)
                return;

            string stage;
            switch (result)
            {
                case CglSemanticValidationShadowRegisterResult.Stored:
                    stage = "CGL_SEMANTIC_VALIDATION_STORED";
                    break;
                case CglSemanticValidationShadowRegisterResult.Duplicate:
                    stage = "CGL_SEMANTIC_VALIDATION_DUPLICATE";
                    break;
                default:
                    stage =
                        "CGL_SEMANTIC_VALIDATION_STORE_FAILED";
                    break;
            }

            Debug.Log(
                "[InteractionTrace] stage=" +
                stage +
                Common(input) +
                ValidationCommon(snapshot) +
                " storeResult=" +
                result
            );
        }

        public static void LogCglSemanticValidationStoreSnapshot(
            CommunicationInput input,
            SemanticCandidateSet candidateSet,
            CglSemanticValidationStoreDiagnosticsSnapshot snapshot,
            string boundary)
        {
            if (input == null || snapshot == null)
                return;

            Debug.Log(
                "[InteractionTrace] " +
                "stage=CGL_SEMANTIC_VALIDATION_STORE_SNAPSHOT" +
                Common(input) +
                ValidationSetCommon(candidateSet) +
                " boundary=" +
                Safe(boundary) +
                StoreDiagnosticsCommon(
                    snapshot.ObservedAtUtc,
                    snapshot.StoredCount,
                    snapshot.EntryLifetime,
                    snapshot.MaximumCapacity,
                    snapshot.ExpiredEntryRemovedCount
                )
            );
        }

        public static void LogCglSemanticValidationFailed(
            CommunicationInput input,
            SemanticCandidateSet candidateSet,
            Exception exception)
        {
            if (input == null)
                return;

            Debug.LogWarning(
                "[InteractionTrace] " +
                "stage=CGL_SEMANTIC_VALIDATION_FAILED" +
                Common(input) +
                ValidationSetCommon(candidateSet) +
                " exceptionType=" +
                Safe(exception != null
                    ? exception.GetType().FullName
                    : string.Empty) +
                " diagnosticMessage=" +
                Safe(exception != null
                    ? exception.Message
                    : string.Empty)
            );
        }

        public static void LogDecision(ReactionDecision decision)
        {
            if (decision == null)
                return;

            Debug.Log(
                "[InteractionTrace] stage=REACTION_DECIDED" +
                " inputId=" +
                Safe(decision.InputId) +
                " interactionId=" +
                Safe(decision.InteractionId) +
                " reactionId=" +
                Safe(decision.ReactionId) +
                " route=" +
                Safe(decision.SelectedRoute) +
                " skillId=" +
                Safe(decision.SelectedSkillId) +
                " actionId=" +
                Safe(decision.ActionId) +
                " direction=" +
                Safe(decision.DirectionCandidate) +
                " target=" +
                Safe(decision.TargetCandidate) +
                " reason=" +
                Safe(decision.Reason) +
                " policyVersion=" +
                Safe(decision.PolicyVersion)
            );
        }

        public static void LogCglLegacyComparisonStarted(
            ReactionDecision decision)
        {
            if (decision == null)
                return;

            Debug.Log(
                "[InteractionTrace] stage=CGL_LEGACY_COMPARISON_STARTED" +
                Common(decision)
            );
        }

        public static void LogCglLegacyComparison(
            CglLegacyComparisonRecord record)
        {
            if (record == null)
                return;

            string stage =
                record.ComparisonResult == CglLegacyComparisonResults.Match
                    ? "CGL_LEGACY_MATCH"
                    : record.ComparisonResult ==
                      CglLegacyComparisonResults.ComparisonUnavailable
                        ? "CGL_LEGACY_COMPARISON_UNAVAILABLE"
                        : "CGL_LEGACY_MISMATCH";

            Debug.Log(
                "[InteractionTrace] stage=" +
                stage +
                Common(record) +
                " comparisonResult=" +
                Safe(record.ComparisonResult) +
                " mismatchReason=" +
                Safe(record.MismatchReason)
            );
        }

        public static void LogCglLegacyComparisonUnavailable(
            ReactionDecision decision,
            string reason)
        {
            if (decision == null)
                return;

            Debug.LogWarning(
                "[InteractionTrace] stage=CGL_LEGACY_COMPARISON_UNAVAILABLE" +
                Common(decision) +
                " reason=" +
                Safe(reason)
            );
        }

        public static void LogCglLegacyComparisonFailed(
            ReactionDecision decision,
            Exception exception)
        {
            if (decision == null)
                return;

            Debug.LogWarning(
                "[InteractionTrace] stage=CGL_LEGACY_COMPARISON_FAILED" +
                Common(decision) +
                " exceptionType=" +
                Safe(exception != null
                    ? exception.GetType().FullName
                    : string.Empty) +
                " message=" +
                Safe(exception != null
                    ? exception.Message
                    : string.Empty)
            );
        }

        public static void LogOutcome(ReactionOutcome outcome)
        {
            if (outcome == null)
                return;

            Debug.Log(
                "[InteractionTrace] stage=OUTPUT_REQUESTED" +
                " inputId=" +
                Safe(outcome.InputId) +
                " interactionId=" +
                Safe(outcome.InteractionId) +
                " reactionId=" +
                Safe(outcome.ReactionId) +
                " outputId=" +
                Safe(outcome.OutputId) +
                " responseType=" +
                Safe(outcome.ResponseType) +
                " skillId=" +
                Safe(outcome.SelectedSkillId) +
                " skillStatus=" +
                Safe(outcome.SkillStatus) +
                " speechRequested=" +
                outcome.SpeechRequested +
                " caller=" +
                Safe(outcome.Caller) +
                " text=" +
                Safe(outcome.Text)
            );
        }

        private static string Common(CommunicationInput input)
        {
            return
                " inputId=" +
                Safe(input.InputId) +
                " interactionId=" +
                Safe(input.InteractionId) +
                " source=" +
                input.Source +
                " sessionId=" +
                Safe(input.SessionId) +
                " generation=" +
                input.LifecycleGeneration;
        }

        private static string Common(ReactionDecision decision)
        {
            return
                " inputId=" +
                Safe(decision.InputId) +
                " interactionId=" +
                Safe(decision.InteractionId) +
                " legacyRoute=" +
                Safe(decision.SelectedRoute) +
                " legacyActionId=" +
                Safe(decision.ActionId);
        }

        private static string Common(CglLegacyComparisonRecord record)
        {
            return
                " inputId=" +
                Safe(record.InputId) +
                " interactionId=" +
                Safe(record.InteractionId) +
                " normalizedText=" +
                Safe(record.NormalizedText) +
                " cglRoute=" +
                Safe(record.CglRequestType) +
                " cglAction=" +
                Safe(record.CglActionCandidate) +
                " cglDirection=" +
                Safe(record.CglDirectionCandidate) +
                " legacyRoute=" +
                Safe(record.LegacyRoute) +
                " legacyActionId=" +
                Safe(record.LegacyActionId) +
                " legacyDirection=" +
                Safe(record.LegacyDirection) +
                " parserVersion=" +
                Safe(record.ParserVersion);
        }

        private static string SemanticCommon(
            SemanticCandidateSet candidateSet,
            SemanticParserSource parserSource)
        {
            return
                " analysisId=" +
                Safe(candidateSet != null
                    ? candidateSet.AnalysisId
                    : string.Empty) +
                " candidateSetId=" +
                Safe(candidateSet != null
                    ? candidateSet.CandidateSetId
                    : string.Empty) +
                " parserSource=" +
                parserSource +
                " candidateCount=" +
                (candidateSet != null
                    ? candidateSet.Candidates.Count
                    : 0) +
                SemanticIrIdsCommon(candidateSet) +
                " setStatus=" +
                (candidateSet != null
                    ? candidateSet.SetStatus.ToString()
                    : "<empty>");
        }

        private static string ValidationSetCommon(
            SemanticCandidateSet candidateSet)
        {
            return
                " analysisId=" +
                Safe(candidateSet != null
                    ? candidateSet.AnalysisId
                    : string.Empty) +
                " candidateSetId=" +
                Safe(candidateSet != null
                    ? candidateSet.CandidateSetId
                    : string.Empty) +
                " candidateCount=" +
                (candidateSet != null
                    ? candidateSet.Candidates.Count
                    : 0) +
                SemanticIrIdsCommon(candidateSet);
        }

        private static string ValidationCommon(
            CglSemanticValidationShadowSnapshot snapshot)
        {
            return
                " analysisId=" +
                Safe(snapshot.AnalysisId) +
                " candidateSetId=" +
                Safe(snapshot.CandidateSetId) +
                " candidateId=" +
                Safe(snapshot.CandidateId) +
                ValidationSemanticIrCommon(snapshot) +
                " validationId=" +
                Safe(snapshot.ValidationId) +
                " validationScope=" +
                snapshot.Scope +
                " validationStatus=" +
                snapshot.Status +
                " failureCodeCount=" +
                snapshot.FailureCodes.Count +
                " requiresClarification=" +
                snapshot.RequiresClarification +
                " validatorVersion=" +
                Safe(snapshot.ValidatorVersion) +
                " diagnosticMessage=" +
                Safe(snapshot.DiagnosticMessage);
        }

        private static string SemanticIrIdsCommon(
            SemanticCandidateSet candidateSet)
        {
            if (candidateSet == null)
            {
                return
                    " semanticIrIds=<empty>" +
                    " semanticIrIdCount=0";
            }

            List<string> semanticIrIds = new List<string>();
            for (int i = 0; i < candidateSet.Candidates.Count; i++)
            {
                SemanticIrCandidate candidate =
                    candidateSet.Candidates[i];

                if (candidate != null)
                    semanticIrIds.Add(candidate.SemanticIrId);
            }

            return
                " semanticIrIds=" +
                Safe(string.Join(",", semanticIrIds)) +
                " semanticIrIdCount=" +
                semanticIrIds.Count;
        }

        private static string ValidationSemanticIrCommon(
            CglSemanticValidationShadowSnapshot snapshot)
        {
            if (snapshot.Scope ==
                CglSemanticValidationShadowScope.Candidate)
            {
                return
                    " semanticIrId=" +
                    Safe(snapshot.SemanticIrId);
            }

            return
                " semanticIrIds=" +
                Safe(string.Join(",", snapshot.SemanticIrIds)) +
                " semanticIrIdCount=" +
                snapshot.SemanticIrIds.Count;
        }

        private static string StoreDiagnosticsCommon(
            DateTime observedAtUtc,
            int storedCount,
            TimeSpan entryLifetime,
            int maximumCapacity,
            int expiredEntryRemovedCount)
        {
            return
                " observedAtUtc=" +
                observedAtUtc.ToString("o") +
                " storedCount=" +
                storedCount +
                " ttlSeconds=" +
                entryLifetime.TotalSeconds +
                " maxCapacity=" +
                maximumCapacity +
                " expiredEntryRemovedCount=" +
                expiredEntryRemovedCount;
        }

        private static string Safe(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "<empty>";

            return value
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }
    }
}
