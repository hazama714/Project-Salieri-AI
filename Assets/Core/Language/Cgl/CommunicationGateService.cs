// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Core.Language.Compiler;
using SalieriAI.Core.Language.Compiler.Compatibility;
using SalieriAI.Core.Language.Compiler.Shadow;
using SalieriAI.Core.Language.Compiler.Validation;
using SalieriAI.Core.Language.Contracts;
using SalieriAI.Core.Language.Runtime;

namespace SalieriAI.Core.Language.Cgl
{
    /// <summary>
    /// Phase 2A shadow-only CGL entry point.
    /// Failure is contained here so the existing production route always continues.
    /// </summary>
    public static class CommunicationGateService
    {
        public static bool TryAnalyzeShadow(
            CommunicationInput input,
            out CglInterpretation interpretation)
        {
            interpretation = null;

            if (input == null)
                return false;

            InteractionTraceLogger.LogCglShadowStarted(input);

            try
            {
                interpretation = DeterministicCglParser.Parse(input);
                CglShadowResultStore.Register(interpretation);
                InteractionTraceLogger.LogCglShadowCompleted(
                    input,
                    interpretation
                );

                TryRegisterSccSemanticShadow(input, interpretation);
                return true;
            }
            catch (Exception exception)
            {
                InteractionTraceLogger.LogCglShadowFailed(
                    input,
                    exception
                );
                interpretation = null;
                return false;
            }
        }

        private static void TryRegisterSccSemanticShadow(
            CommunicationInput input,
            CglInterpretation interpretation)
        {
            const SemanticParserSource parserSource =
                SemanticParserSource.DeterministicPattern;

            InteractionTraceLogger.LogSccSemanticAdapterStarted(
                input,
                interpretation,
                parserSource
            );

            SemanticCandidateSet candidateSet;
            try
            {
                candidateSet =
                    CglInterpretationSemanticAdapter.Convert(
                        input,
                        interpretation
                    );

                InteractionTraceLogger.LogSccSemanticAdapterCompleted(
                    input,
                    candidateSet,
                    parserSource
                );
            }
            catch (Exception exception)
            {
                InteractionTraceLogger.LogSccSemanticAdapterFailed(
                    input,
                    interpretation,
                    parserSource,
                    "ADAPTER_CONVERSION_FAILED",
                    exception
                );
                return;
            }

            try
            {
                SccSemanticShadowStoreDiagnosticsSnapshot before =
                    SccSemanticShadowStore.GetDiagnosticsSnapshot();
                InteractionTraceLogger.LogSccSemanticStoreSnapshot(
                    input,
                    candidateSet,
                    before,
                    "before-register"
                );

                SccSemanticShadowRegisterResult result =
                    SccSemanticShadowStore.Register(
                        candidateSet,
                        parserSource
                    );

                InteractionTraceLogger.LogSccSemanticShadowRegistration(
                    input,
                    candidateSet,
                    parserSource,
                    result
                );

                SccSemanticShadowStoreDiagnosticsSnapshot after =
                    SccSemanticShadowStore.GetDiagnosticsSnapshot();
                InteractionTraceLogger.LogSccSemanticStoreSnapshot(
                    input,
                    candidateSet,
                    after,
                    "after-register"
                );
            }
            catch (Exception exception)
            {
                InteractionTraceLogger.LogSccSemanticShadowFailed(
                    input,
                    candidateSet,
                    parserSource,
                    "STORE_EXCEPTION",
                    exception
                );
            }

            TryRegisterSemanticValidationShadow(input, candidateSet);
        }

        private static void TryRegisterSemanticValidationShadow(
            CommunicationInput input,
            SemanticCandidateSet candidateSet)
        {
            try
            {
                InteractionTraceLogger.LogCglSemanticValidationStarted(
                    input,
                    candidateSet,
                    CglSemanticValidator.ValidatorVersion
                );

                CglSemanticValidationStoreDiagnosticsSnapshot before =
                    CglSemanticValidationShadowStore
                        .GetDiagnosticsSnapshot();
                InteractionTraceLogger
                    .LogCglSemanticValidationStoreSnapshot(
                        input,
                        candidateSet,
                        before,
                        "before-register"
                    );

                CglSemanticValidationShadowBatchResult batchResult =
                    CglSemanticValidationShadowPipeline.Run(
                        candidateSet
                    );

                CglSemanticValidationStoreDiagnosticsSnapshot after =
                    CglSemanticValidationShadowStore
                        .GetDiagnosticsSnapshot();

                for (
                    int i = 0;
                    i < batchResult.Snapshots.Count;
                    i++)
                {
                    CglSemanticValidationShadowSnapshot snapshot =
                        batchResult.Snapshots[i];
                    CglSemanticValidationShadowRegisterResult result =
                        batchResult.RegisterResults[i];

                    InteractionTraceLogger
                        .LogCglSemanticValidationCompleted(
                            input,
                            snapshot
                        );
                    InteractionTraceLogger
                        .LogCglSemanticValidationStoreRegistration(
                            input,
                            snapshot,
                            result
                        );
                }

                InteractionTraceLogger
                    .LogCglSemanticValidationStoreSnapshot(
                        input,
                        candidateSet,
                        after,
                        "after-register"
                    );
            }
            catch (Exception exception)
            {
                InteractionTraceLogger.LogCglSemanticValidationFailed(
                    input,
                    candidateSet,
                    exception
                );
            }
        }
    }
}
