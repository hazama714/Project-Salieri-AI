// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

/* Project Salieri AI - Apache-2.0 */
using System;

using SalieriAI.Core.Execution.Orchestration.Contracts;
using SalieriAI.Core.Execution.Orchestration.Reduction;
using SalieriAI.Core.Execution.Orchestration.Resources;
using SalieriAI.Core.Execution.Orchestration.Runtime;
using SalieriAI.Core.Execution.Orchestration.Validation;

namespace SalieriAI.Core.Execution.Orchestration.Integration
{
    public enum ExecutionProductionAdmissionStatus
    {
        Started = 0,
        ValidationFailed = 1,
        HostStartFailed = 2,
        AdmissionFailed = 3
    }

    /// <summary>
    /// Explicit result of the one-time Production admission boundary.
    /// Validation failure never starts or replaces the runtime wake-up host.
    /// </summary>
    public sealed class ExecutionProductionAdmissionResult
    {
        public ExecutionProductionAdmissionStatus Status { get; }
        public ExecutionPlanValidationBundle Validation { get; }
        public ValidatedExecutionPlan ValidatedPlan { get; }
        public ExecutionCoordinatorReductionResult InitialReduction
        {
            get;
        }
        public LogicalResourceLeaseState InitialLeaseState { get; }
        public string Diagnostic { get; }
        public bool IsStarted =>
            Status == ExecutionProductionAdmissionStatus.Started;

        internal ExecutionProductionAdmissionResult(
            ExecutionProductionAdmissionStatus status,
            ExecutionPlanValidationBundle validation,
            ValidatedExecutionPlan validatedPlan,
            ExecutionCoordinatorReductionResult initialReduction,
            LogicalResourceLeaseState initialLeaseState,
            string diagnostic)
        {
            Status = status;
            Validation = validation;
            ValidatedPlan = validatedPlan;
            InitialReduction = initialReduction;
            InitialLeaseState = initialLeaseState;
            Diagnostic = diagnostic ?? string.Empty;
        }
    }

    /// <summary>
    /// Production boundary for an already-built ReactionExecutionPlan.
    /// It validates exactly once per admission attempt, creates the initial
    /// pure coordinator and lease states, and starts the existing durable
    /// runtime wake-up host. Plan construction remains outside this class.
    /// </summary>
    public sealed class ExecutionProductionAdmissionService
    {
        public const string ServiceVersion =
            "execution-production-admission-3d4b.1";

        private readonly ExecutionResourceWaitRuntimeWakeupHost host;

        public int AdmissionAttemptCount { get; private set; }
        public int ValidationInvocationCount { get; private set; }
        public ExecutionProductionAdmissionResult LastResult { get; private set; }

        public ExecutionProductionAdmissionService(
            ExecutionResourceWaitRuntimeWakeupHost host)
        {
            this.host = host;
        }

        /// <summary>
        /// The caller owns all non-deterministic admission values. Both
        /// validation contexts receive only their explicit ID and UTC time.
        /// No Resource/Profile/Timeout wake-up can re-enter this method.
        /// </summary>
        public ExecutionProductionAdmissionResult AdmitAndStart(
            ReactionExecutionPlan plan,
            string structureValidationId,
            string policyValidationId,
            DateTime evaluatedAtUtc)
        {
            AdmissionAttemptCount++;
            DateTime now = NormalizeUtc(evaluatedAtUtc);
            ExecutionPlanValidationBundle validation = null;

            try
            {
                ValidationInvocationCount++;
                validation = ExecutionPlanValidationService.Validate(
                    plan,
                    new ExecutionPlanValidationContext(
                        structureValidationId, now),
                    new ExecutionPlanValidationContext(
                        policyValidationId, now),
                    now);

                if (validation == null || !validation.IsValid)
                {
                    return Store(new ExecutionProductionAdmissionResult(
                        ExecutionProductionAdmissionStatus.ValidationFailed,
                        validation,
                        null,
                        null,
                        null,
                        "EXECUTION_PLAN_VALIDATION_FAILED"));
                }

                ValidatedExecutionPlan validated =
                    validation.ValidatedPlan;
                int generation = validated.SourcePlan.PlanGeneration;
                ExecutionCoordinatorRuntimeState initialState =
                    ExecutionCoordinatorReducer.CreateInitialState(
                        validated,
                        new ExecutionCoordinatorReducerContext(
                            now,
                            string.Empty,
                            string.Empty,
                            new string[0],
                            generation));
                var initialReduction =
                    new ExecutionCoordinatorReductionResult(
                        initialState,
                        new ExecutionCoordinatorEffectIntent[0],
                        new string[0],
                        ExecutionCoordinatorEventDisposition.Applied);
                LogicalResourceLeaseState initialLeaseState =
                    LogicalResourceLeaseState.Empty(generation);

                if (host == null || !host.Start(
                        validated,
                        initialReduction,
                        initialLeaseState))
                {
                    return Store(new ExecutionProductionAdmissionResult(
                        ExecutionProductionAdmissionStatus.HostStartFailed,
                        validation,
                        validated,
                        initialReduction,
                        initialLeaseState,
                        "RUNTIME_WAKEUP_HOST_START_FAILED"));
                }

                return Store(new ExecutionProductionAdmissionResult(
                    ExecutionProductionAdmissionStatus.Started,
                    validation,
                    validated,
                    initialReduction,
                    initialLeaseState,
                    "EXECUTION_ADMISSION_STARTED"));
            }
            catch (Exception exception)
            {
                return Store(new ExecutionProductionAdmissionResult(
                    ExecutionProductionAdmissionStatus.AdmissionFailed,
                    validation,
                    validation != null ? validation.ValidatedPlan : null,
                    null,
                    null,
                    exception.GetType().Name + ": " + exception.Message));
            }
        }

        private ExecutionProductionAdmissionResult Store(
            ExecutionProductionAdmissionResult result)
        {
            LastResult = result;
            return result;
        }

        private static DateTime NormalizeUtc(DateTime value)
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
    }
}
