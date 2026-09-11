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

using SalieriAI.Core.Execution;
using SalieriAI.Core.Execution.Orchestration.Adapters;
using SalieriAI.Core.Execution.Orchestration.Authority;
using SalieriAI.Core.Execution.Orchestration.Contracts;
using SalieriAI.Core.Execution.Orchestration.Reduction;
using SalieriAI.Core.Execution.Orchestration.Resources;
using SalieriAI.Core.Execution.Orchestration.Runtime;
using SalieriAI.Core.Execution.Orchestration.Validation;
using SalieriAI.Core.Runtime;
using SalieriAI.Core.State;

namespace SalieriAI.Core.Execution.Orchestration.Integration
{
    public enum AutonomousLookAroundShadowPermissionResult
    {
        NotEvaluated = 0,
        GrantedByLegacyPolicy = 1,
        RejectedByLegacyPolicy = 2
    }

    /// <summary>
    /// One immutable Legacy/Shadow comparison record for an autonomous
    /// lookAround request. No field represents verified physical completion.
    /// </summary>
    public sealed class AutonomousLookAroundShadowObservation
    {
        public string RequestId { get; }
        public bool LegacyAccepted { get; }
        public bool SubmissionAccepted { get; }
        public ExecutionProductionSubmissionStatus SubmissionStatus { get; }
        public AutonomousLookAroundShadowPermissionResult PermissionResult
            { get; }
        public ResourceShadowAvailabilityStatus Availability { get; }
        public ExecutionLogicalLeaseShadowDisposition LeaseDisposition
            { get; }
        public int GeneratedBodyIntentCount { get; }
        public ExecutionTerminalOutcome ShadowTerminalOutcome { get; }
        public string ShadowDomainStageId { get; }
        public int LegacyBodyFactCount { get; }
        public int LiveLeaseMutationCount { get; }
        public int LiveBodyDispatchCount { get; }
        public string Diagnostic { get; }
        public bool ShadowCompleted { get; }

        public AutonomousLookAroundShadowObservation(
            string requestId,
            bool legacyAccepted,
            bool submissionAccepted,
            ExecutionProductionSubmissionStatus submissionStatus,
            AutonomousLookAroundShadowPermissionResult permissionResult,
            ResourceShadowAvailabilityStatus availability,
            ExecutionLogicalLeaseShadowDisposition leaseDisposition,
            int generatedBodyIntentCount,
            ExecutionTerminalOutcome shadowTerminalOutcome,
            string shadowDomainStageId,
            int legacyBodyFactCount,
            int liveLeaseMutationCount,
            int liveBodyDispatchCount,
            string diagnostic,
            bool shadowCompleted)
        {
            RequestId = requestId ?? string.Empty;
            LegacyAccepted = legacyAccepted;
            SubmissionAccepted = submissionAccepted;
            SubmissionStatus = submissionStatus;
            PermissionResult = permissionResult;
            Availability = availability;
            LeaseDisposition = leaseDisposition;
            GeneratedBodyIntentCount = generatedBodyIntentCount;
            ShadowTerminalOutcome = shadowTerminalOutcome;
            ShadowDomainStageId = shadowDomainStageId ?? string.Empty;
            LegacyBodyFactCount = legacyBodyFactCount;
            LiveLeaseMutationCount = liveLeaseMutationCount;
            LiveBodyDispatchCount = liveBodyDispatchCount;
            Diagnostic = diagnostic ?? string.Empty;
            ShadowCompleted = shadowCompleted;
        }

        public override string ToString()
        {
            return "requestId=" + RequestId +
                " legacyAccepted=" + LegacyAccepted +
                " submission=" + SubmissionStatus +
                " permission=" + PermissionResult +
                " availability=" + Availability +
                " lease=" + LeaseDisposition +
                " bodyIntent=" + GeneratedBodyIntentCount +
                " terminal=" + ShadowTerminalOutcome +
                " stage=" + ShadowDomainStageId +
                " legacyFacts=" + LegacyBodyFactCount +
                " liveLeaseMutation=" + LiveLeaseMutationCount +
                " liveBodyDispatch=" + LiveBodyDispatchCount +
                " completed=" + ShadowCompleted +
                " diagnostic=" + Diagnostic;
        }
    }

    /// <summary>
    /// lookAround-specific synchronous Shadow observer. It uses Production
    /// Submission/Admission, but bypasses the Live logical lease loop and
    /// evaluates ownership with the read-only Shadow evaluator. The temporary
    /// wake-up host is always disposed before returning.
    /// </summary>
    public sealed class AutonomousLookAroundShadowOrchestrationObserver
    {
        public const string EventSource =
            "AutonomousLookAroundShadowOrchestrationObserver";
        public const string ObserverVersion =
            "autonomous-look-around-shadow-orchestration-v0.1";

        private const int ReducerIntentCapacity = 32;

        private readonly InteractionStateController stateController;
        private readonly ExecutionResourceManager resourceManager;
        private readonly ExecutionLogicalResourceLeaseShadowEvaluator
            leaseEvaluator =
                new ExecutionLogicalResourceLeaseShadowEvaluator();
        private readonly ExecutionBodyShadowDriver bodyShadowDriver =
            new ExecutionBodyShadowDriver(
                new ExecutionBodyRuntimeAdapter(),
                new BodyRuntimeCorrelationTracker());

        public AutonomousLookAroundShadowOrchestrationObserver(
            InteractionStateController stateController,
            ExecutionResourceManager resourceManager)
        {
            this.stateController = stateController;
            this.resourceManager = resourceManager;
        }

        public AutonomousLookAroundShadowObservation Observe(
            global::ExecutionRequest request,
            DateTime createdAtUtc,
            bool legacyAccepted,
            IEnumerable<BodyExecutionRuntimeFact> legacyBodyFacts)
        {
            List<BodyExecutionRuntimeFact> facts = legacyBodyFacts != null
                ? new List<BodyExecutionRuntimeFact>(legacyBodyFacts)
                : new List<BodyExecutionRuntimeFact>();
            if (request == null ||
                request.ActionId != ExecutionLookAroundContract.ActionId ||
                string.IsNullOrWhiteSpace(request.RequestId) ||
                !legacyAccepted || stateController == null ||
                resourceManager == null ||
                createdAtUtc == default(DateTime))
            {
                return Failure(request, legacyAccepted, facts.Count,
                    "LOOK_AROUND_SHADOW_INPUT_INVALID");
            }

            DateTime now = Utc(createdAtUtc);
            var host = new ExecutionResourceWaitRuntimeWakeupHost(
                stateController, resourceManager);
            try
            {
                ExecutionActionDescriptorRegistry registry =
                    ExecutionProductionActionDescriptorRegistry.Create();
                var admission =
                    new ExecutionProductionAdmissionService(host);
                var submission = new ExecutionProductionSubmissionService(
                    registry,
                    new ExecutionRequestProductionHandoff(admission),
                    new GuidExecutionProductionSubmissionIdSource());
                ExecutionSubmissionResult submitted = submission.Submit(
                    request,
                    ExecutionRequestProvenance.Autonomous(),
                    ExecutionProductionActionDescriptorRegistry
                        .CreateLookAroundCompositionPolicy(),
                    now);
                if (submitted == null || !submitted.IsStarted)
                {
                    return Rejected(request, legacyAccepted, facts.Count,
                        submitted,
                        "LOOK_AROUND_SHADOW_SUBMISSION_REJECTED");
                }

                ValidatedExecutionPlan plan =
                    submitted.Handoff.Admission.ValidatedPlan;
                if (!host.ProcessPlanStartUtc(now))
                {
                    return Result(request, legacyAccepted, submitted,
                        AutonomousLookAroundShadowPermissionResult
                            .NotEvaluated,
                        ResourceShadowAvailabilityStatus.Unknown,
                        ExecutionLogicalLeaseShadowDisposition.Invalid,
                        0, ExecutionTerminalOutcome.None, string.Empty,
                        facts.Count, 0, 0,
                        "LOOK_AROUND_SHADOW_PLAN_START_FAILED", false);
                }

                int sequence = 0;
                ExecutionCoordinatorReductionResult current =
                    host.CurrentReduction;
                LogicalResourceLeaseState observedLiveLeaseState =
                    host.CurrentLeaseState;
                ExecutionCoordinatorEffectIntent permissionIntent = Find(
                    current,
                    ExecutionCoordinatorEffectIntentType.CheckPermission);
                if (permissionIntent == null)
                {
                    return Result(request, legacyAccepted, submitted,
                        AutonomousLookAroundShadowPermissionResult
                            .NotEvaluated,
                        ResourceShadowAvailabilityStatus.Unknown,
                        ExecutionLogicalLeaseShadowDisposition.Invalid,
                        0, ExecutionTerminalOutcome.None, string.Empty,
                        facts.Count, 0, 0,
                        "LOOK_AROUND_SHADOW_PERMISSION_INTENT_MISSING", false);
                }

                AutonomousLookAroundShadowPermissionResult permission =
                    AutonomousLookAroundShadowPermissionResult
                        .GrantedByLegacyPolicy;
                current = Apply(plan, current,
                    CoordinatorEvent(plan,
                        ExecutionCoordinatorEventType.PermissionGranted,
                        NextId("permission", ref sequence),
                        string.Empty, now.AddTicks(sequence)),
                    ReducerContext(plan, now.AddTicks(sequence),
                        ref sequence));

                ExecutionCoordinatorEffectIntent acquireIntent = Find(
                    current,
                    ExecutionCoordinatorEffectIntentType
                        .AcquireResourceLease);
                if (acquireIntent == null ||
                    acquireIntent.ResourceLease == null)
                {
                    return Result(request, legacyAccepted, submitted,
                        permission,
                        ResourceShadowAvailabilityStatus.Unknown,
                        ExecutionLogicalLeaseShadowDisposition.Invalid,
                        0, ExecutionTerminalOutcome.None, string.Empty,
                        facts.Count, 0, 0,
                        "LOOK_AROUND_SHADOW_ACQUIRE_INTENT_MISSING", false);
                }

                string resourceRequestId = NextId(
                    "resource-request", ref sequence);
                string shadowLeaseId = NextId(
                    "shadow-lease", ref sequence);
                ResourceShadowAvailabilityResult availability =
                    new ExecutionResourceRuntimeAdapter(resourceManager)
                        .QueryAvailability(
                            new ResourceAcquireRequest(
                                resourceRequestId,
                                acquireIntent.IntentId,
                                acquireIntent.PlanId,
                                acquireIntent.StepId,
                                acquireIntent.ExecutionAttemptId,
                                acquireIntent.ResourceLease.ResourceId,
                                acquireIntent.ResourceLease.ResourceType,
                                acquireIntent.ResourceLease.AccessMode,
                                now.AddTicks(sequence)),
                            now.AddTicks(sequence));
                ResourceShadowAvailabilityStatus availabilityStatus =
                    availability != null
                        ? availability.Status
                        : ResourceShadowAvailabilityStatus.Failed;
                ExecutionLogicalLeaseShadowEvaluationResult lease =
                    leaseEvaluator.Evaluate(
                        acquireIntent,
                        observedLiveLeaseState,
                        new ExecutionLogicalLeaseShadowEvaluationContext(
                            resourceRequestId,
                            shadowLeaseId,
                            NextId("lease-event", ref sequence),
                            EventSource,
                            availabilityStatus,
                            false,
                            true,
                            string.Empty,
                            plan.SourcePlan.PlanGeneration));
                if (lease == null ||
                    lease.ProjectedCoordinatorEvent == null)
                {
                    return Result(request, legacyAccepted, submitted,
                        permission, availabilityStatus,
                        lease != null ? lease.Disposition :
                            ExecutionLogicalLeaseShadowDisposition.Invalid,
                        0, ExecutionTerminalOutcome.None, string.Empty,
                        facts.Count, 0, 0,
                        "LOOK_AROUND_SHADOW_LEASE_EVALUATION_FAILED", false);
                }

                current = Apply(plan, current,
                    lease.ProjectedCoordinatorEvent,
                    ReducerContext(plan, now.AddTicks(sequence),
                        ref sequence));
                LogicalResourceLeaseState shadowLeaseState =
                    lease.ProjectedShadowState;
                if (lease.Disposition ==
                    ExecutionLogicalLeaseShadowDisposition.WouldAcquire)
                {
                    ExecutionCoordinatorStepState permissionStep =
                        current.State.FindStep(
                            plan.SourcePlan.Steps[0].StepId);
                    current = Apply(plan, current,
                        CoordinatorEvent(plan,
                            ExecutionCoordinatorEventType.PermissionGranted,
                            NextId("permission-after-lease", ref sequence),
                            permissionStep != null
                                ? permissionStep.CurrentAttemptId
                                : string.Empty,
                            now.AddTicks(sequence)),
                        ReducerContext(plan, now.AddTicks(sequence),
                            ref sequence));
                }

                ExecutionCoordinatorEffectIntent bodyIntent = Find(
                    current,
                    ExecutionCoordinatorEffectIntentType.StartExecutor);
                int bodyIntentCount = Count(current,
                    ExecutionCoordinatorEffectIntentType.StartExecutor);
                int liveBodyDispatchCount = 0;
                if (bodyIntent != null && BodyAuthorityIsShadowOnly(
                        plan, bodyIntent, now.AddTicks(sequence)))
                {
                    current = ObserveLegacyBodyFacts(
                        plan, current, bodyIntent, request, facts,
                        now, ref sequence);
                }

                ExecutionCoordinatorEffectIntent releaseIntent = Find(
                    current,
                    ExecutionCoordinatorEffectIntentType
                        .ReleaseResourceLease);
                if (releaseIntent != null && shadowLeaseState != null)
                {
                    ExecutionLogicalLeaseShadowEvaluationResult release =
                        leaseEvaluator.Evaluate(
                            releaseIntent,
                            shadowLeaseState,
                            new ExecutionLogicalLeaseShadowEvaluationContext(
                                NextId("release-request", ref sequence),
                                shadowLeaseId,
                                NextId("release-event", ref sequence),
                                EventSource,
                                ResourceShadowAvailabilityStatus.Available,
                                false,
                                true,
                                string.Empty,
                                plan.SourcePlan.PlanGeneration));
                    if (release != null &&
                        release.ProjectedCoordinatorEvent != null)
                    {
                        current = Apply(plan, current,
                            release.ProjectedCoordinatorEvent,
                            ReducerContext(plan, now.AddTicks(sequence),
                                ref sequence));
                    }
                }

                ExecutionCoordinatorStepState finalStep =
                    current.State.FindStep(
                        plan.SourcePlan.Steps[0].StepId);
                ExecutionTerminalOutcome terminal = finalStep != null
                    ? (finalStep.TerminalOutcome !=
                        ExecutionTerminalOutcome.None
                        ? finalStep.TerminalOutcome
                        : finalStep.PendingTerminalOutcome)
                    : ExecutionTerminalOutcome.None;
                string stage = finalStep != null &&
                    finalStep.DomainStage != null
                    ? finalStep.DomainStage.StageId : string.Empty;
                bool complete = terminal !=
                    ExecutionTerminalOutcome.None;
                return Result(request, legacyAccepted, submitted,
                    permission, availabilityStatus, lease.Disposition,
                    bodyIntentCount, terminal, stage, facts.Count,
                    lease.LiveOwnershipMutationCount,
                    liveBodyDispatchCount,
                    complete
                        ? "LOOK_AROUND_SHADOW_COMPLETED"
                        : "LOOK_AROUND_SHADOW_OBSERVED",
                    complete);
            }
            catch (Exception exception)
            {
                return Failure(request, legacyAccepted, facts.Count,
                    "LOOK_AROUND_SHADOW_EXCEPTION:" +
                    exception.GetType().Name + ":" + exception.Message);
            }
            finally
            {
                host.Dispose();
            }
        }

        private ExecutionCoordinatorReductionResult ObserveLegacyBodyFacts(
            ValidatedExecutionPlan plan,
            ExecutionCoordinatorReductionResult current,
            ExecutionCoordinatorEffectIntent bodyIntent,
            global::ExecutionRequest legacyRequest,
            IList<BodyExecutionRuntimeFact> facts,
            DateTime baseUtc,
            ref int sequence)
        {
            ExecutionCoordinatorStepState step = current.State.FindStep(
                plan.SourcePlan.Steps[0].StepId);
            var request = new BodyExecutionRequest(
                legacyRequest.RequestId,
                bodyIntent.IntentId,
                bodyIntent.PlanId,
                bodyIntent.StepId,
                step != null ? step.CurrentAttemptId : string.Empty,
                ExecutionLookAroundContract.ActionId,
                string.Empty,
                plan.SourcePlan.Steps[0].RequestReference,
                baseUtc,
                plan.SourcePlan.Steps[0].CompletionPolicy);

            for (int i = 0; i < facts.Count; i++)
            {
                BodyExecutionRuntimeFact fact = facts[i];
                if (fact == null ||
                    fact.ActionId != ExecutionLookAroundContract.ActionId)
                    continue;
                BodyShadowExecutionResult observed = bodyShadowDriver.Observe(
                    request,
                    fact,
                    new BodyShadowEventIds(
                        NextId("body-lifecycle", ref sequence),
                        NextId("body-completion", ref sequence),
                        NextId("body-terminal", ref sequence)),
                    NextId("body-observation", ref sequence),
                    EventSource,
                    plan.SourcePlan.PlanGeneration,
                    stateController != null
                        ? stateController.CurrentState
                        : InteractionState.Idle,
                    null,
                    plan,
                    current.State,
                    ReducerContext(plan,
                        fact.OccurredAtUtc != default(DateTime)
                            ? fact.OccurredAtUtc
                            : baseUtc.AddTicks(sequence),
                        ref sequence));
                if (observed == null)
                    continue;
                for (int j = 0; j < observed.ReductionResults.Count; j++)
                {
                    ExecutionCoordinatorReductionResult reduction =
                        observed.ReductionResults[j];
                    if (reduction != null)
                        current = reduction;
                }
            }
            return current;
        }

        private static bool BodyAuthorityIsShadowOnly(
            ValidatedExecutionPlan plan,
            ExecutionCoordinatorEffectIntent intent,
            DateTime evaluatedAtUtc)
        {
            ExecutionAuthorityDecision decision =
                new ExecutionLimitedLiveAuthorityGate().Evaluate(
                    ExecutionLiveAuthorityPolicy.CreateDefault(),
                    new ExecutionAuthorityRequest(
                        intent.PlanId,
                        intent.StepId,
                        intent.ExecutionAttemptId,
                        intent.RequestId,
                        ExecutionAuthorityDomain.Body,
                        intent.IntentType,
                        ExecutionLookAroundContract.ActionId,
                        string.Empty,
                        string.Empty,
                        ExecutionControlScope.None,
                        false,
                        evaluatedAtUtc),
                    ExecutionLiveAuthorityPolicy
                        .CreateCurrentCapabilityFixture(),
                    new ExecutionAuthorityEvaluationContext(
                        false, true, evaluatedAtUtc, EventSource));
            return decision != null && !decision.DispatchAllowed &&
                decision.Disposition ==
                    ExecutionAuthorityDisposition.ShadowOnly &&
                decision.EffectiveMode ==
                    ExecutionAuthorityMode.ShadowOnly;
        }

        private static ExecutionCoordinatorReductionResult Apply(
            ValidatedExecutionPlan plan,
            ExecutionCoordinatorReductionResult current,
            ExecutionCoordinatorEvent coordinatorEvent,
            ExecutionCoordinatorReducerContext context)
        {
            return ExecutionCoordinatorReducer.Reduce(
                plan, current.State, coordinatorEvent, context);
        }

        private static ExecutionCoordinatorEvent CoordinatorEvent(
            ValidatedExecutionPlan plan,
            ExecutionCoordinatorEventType type,
            string eventId,
            string attemptId,
            DateTime occurredAtUtc)
        {
            return new ExecutionCoordinatorEvent(
                eventId,
                type,
                plan.SourcePlan.PlanId,
                plan.SourcePlan.Steps[0].StepId,
                attemptId,
                string.Empty,
                occurredAtUtc,
                EventSource,
                plan.SourcePlan.PlanGeneration,
                null,
                ExecutionTerminalOutcome.None,
                new string[0],
                string.Empty);
        }

        private static ExecutionCoordinatorReducerContext ReducerContext(
            ValidatedExecutionPlan plan,
            DateTime nowUtc,
            ref int sequence)
        {
            var intentIds = new List<string>();
            for (int i = 0; i < ReducerIntentCapacity; i++)
                intentIds.Add(NextId("intent", ref sequence));
            return new ExecutionCoordinatorReducerContext(
                nowUtc,
                NextId("attempt", ref sequence),
                NextId("runtime-request", ref sequence),
                intentIds,
                plan.SourcePlan.PlanGeneration);
        }

        private static ExecutionCoordinatorEffectIntent Find(
            ExecutionCoordinatorReductionResult reduction,
            ExecutionCoordinatorEffectIntentType type)
        {
            if (reduction == null)
                return null;
            for (int i = 0; i < reduction.EffectIntents.Count; i++)
                if (reduction.EffectIntents[i].IntentType == type)
                    return reduction.EffectIntents[i];
            return null;
        }

        private static int Count(
            ExecutionCoordinatorReductionResult reduction,
            ExecutionCoordinatorEffectIntentType type)
        {
            if (reduction == null)
                return 0;
            int count = 0;
            for (int i = 0; i < reduction.EffectIntents.Count; i++)
                if (reduction.EffectIntents[i].IntentType == type)
                    count++;
            return count;
        }

        private static string NextId(string prefix, ref int sequence)
        {
            sequence++;
            return prefix + "-" + sequence + "-" +
                Guid.NewGuid().ToString("N");
        }

        private static DateTime Utc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc)
                return value;
            return value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        private static AutonomousLookAroundShadowObservation Failure(
            global::ExecutionRequest request,
            bool legacyAccepted,
            int factCount,
            string diagnostic)
        {
            return new AutonomousLookAroundShadowObservation(
                request != null ? request.RequestId : string.Empty,
                legacyAccepted,
                false,
                ExecutionProductionSubmissionStatus.SubmissionFailed,
                AutonomousLookAroundShadowPermissionResult.NotEvaluated,
                ResourceShadowAvailabilityStatus.Unknown,
                ExecutionLogicalLeaseShadowDisposition.Invalid,
                0,
                ExecutionTerminalOutcome.None,
                string.Empty,
                factCount,
                0,
                0,
                diagnostic,
                false);
        }

        private static AutonomousLookAroundShadowObservation Rejected(
            global::ExecutionRequest request,
            bool legacyAccepted,
            int factCount,
            ExecutionSubmissionResult submission,
            string diagnostic)
        {
            return new AutonomousLookAroundShadowObservation(
                request != null ? request.RequestId : string.Empty,
                legacyAccepted,
                false,
                submission != null
                    ? submission.Status
                    : ExecutionProductionSubmissionStatus.SubmissionFailed,
                AutonomousLookAroundShadowPermissionResult.NotEvaluated,
                ResourceShadowAvailabilityStatus.Unknown,
                ExecutionLogicalLeaseShadowDisposition.Invalid,
                0,
                ExecutionTerminalOutcome.None,
                string.Empty,
                factCount,
                0,
                0,
                diagnostic,
                false);
        }

        private static AutonomousLookAroundShadowObservation Result(
            global::ExecutionRequest request,
            bool legacyAccepted,
            ExecutionSubmissionResult submission,
            AutonomousLookAroundShadowPermissionResult permission,
            ResourceShadowAvailabilityStatus availability,
            ExecutionLogicalLeaseShadowDisposition lease,
            int bodyIntentCount,
            ExecutionTerminalOutcome terminal,
            string domainStageId,
            int factCount,
            int liveLeaseMutations,
            int liveBodyDispatches,
            string diagnostic,
            bool completed)
        {
            return new AutonomousLookAroundShadowObservation(
                request != null ? request.RequestId : string.Empty,
                legacyAccepted,
                submission != null && submission.IsStarted,
                submission != null
                    ? submission.Status
                    : ExecutionProductionSubmissionStatus.SubmissionFailed,
                permission,
                availability,
                lease,
                bodyIntentCount,
                terminal,
                domainStageId,
                factCount,
                liveLeaseMutations,
                liveBodyDispatches,
                diagnostic,
                completed);
        }
    }
}
