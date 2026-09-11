// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;

using SalieriAI.Core.Execution.Orchestration.Contracts;
using SalieriAI.Core.Execution.Orchestration.Integration;
using SalieriAI.Core.Execution.Orchestration.Runtime;
using SalieriAI.Core.Execution.Orchestration.Validation;

namespace SalieriAI.Core.Execution.Orchestration.Reduction
{
    /// <summary>
    /// Pure, deterministic state reducer. It never executes an effect and
    /// never reads time, IDs, Unity state, or runtime services implicitly.
    /// </summary>
    public static class ExecutionCoordinatorReducer
    {
        public const string ReducerVersion =
            "execution-coordinator-reducer-3c0a.1";

        public static ExecutionCoordinatorRuntimeState CreateInitialState(
            ValidatedExecutionPlan plan,
            ExecutionCoordinatorReducerContext context)
        {
            if (plan == null)
                throw new ArgumentNullException("plan");
            if (plan.SourcePlan == null)
                throw new ArgumentException("SourcePlan is required.", "plan");
            if (context == null)
                throw new ArgumentNullException("context");
            if (context.Generation != plan.SourcePlan.PlanGeneration)
            {
                throw new ArgumentException(
                    "Context generation must match the validated plan.",
                    "context"
                );
            }

            ReactionExecutionPlan source = plan.SourcePlan;
            List<ExecutionCoordinatorStepState> steps =
                new List<ExecutionCoordinatorStepState>();

            for (int i = 0; i < source.Steps.Count; i++)
            {
                ReactionExecutionStep definition = source.Steps[i];
                bool dependencySatisfied =
                    !HasIncomingDependency(source, definition.StepId);
                steps.Add(
                    NewStepState(
                        definition.StepId,
                        dependencySatisfied
                            ? ExecutionCoordinatorStepLifecycle.Ready
                            : ExecutionCoordinatorStepLifecycle
                                .BlockedByDependency,
                        dependencySatisfied
                    )
                );
            }

            return new ExecutionCoordinatorRuntimeState(
                source.PlanId,
                source.PlanGeneration,
                ExecutionCoordinatorPlanLifecycle.Accepted,
                steps,
                new ExecutionCoordinatorAttemptState[0],
                new ResourceLeaseReference[0],
                new ExecutionCoordinatorControlState[0],
                new string[0],
                null,
                context.NowUtc,
                ExecutionTerminalOutcome.None,
                new string[0]
            );
        }

        public static ExecutionCoordinatorReductionResult Reduce(
            ValidatedExecutionPlan plan,
            ExecutionCoordinatorRuntimeState state,
            ExecutionCoordinatorEvent coordinatorEvent,
            ExecutionCoordinatorReducerContext context)
        {
            if (plan == null || plan.SourcePlan == null ||
                state == null || context == null || coordinatorEvent == null)
            {
                return Rejected(
                    state,
                    ExecutionCoordinatorDiagnosticCodes.InvalidEvent
                );
            }

            if (Contains(state.ProcessedEventIds, coordinatorEvent.EventId))
            {
                return new ExecutionCoordinatorReductionResult(
                    state,
                    new ExecutionCoordinatorEffectIntent[0],
                    new[]
                    {
                        ExecutionCoordinatorDiagnosticCodes.DuplicateEvent
                    },
                    ExecutionCoordinatorEventDisposition.Duplicate
                );
            }

            if (!Same(state.PlanId, plan.SourcePlan.PlanId) ||
                !Same(coordinatorEvent.PlanId, state.PlanId) ||
                coordinatorEvent.Generation != state.Generation ||
                context.Generation != state.Generation)
            {
                return Stale(state, "Plan or generation mismatch.");
            }

            ExecutionCoordinatorContractValidationResult validation =
                ExecutionCoordinatorContractValidator.ValidateEvent(
                    coordinatorEvent,
                    state.PlanId
                );
            if (!validation.IsValid)
            {
                return new ExecutionCoordinatorReductionResult(
                    state,
                    new ExecutionCoordinatorEffectIntent[0],
                    validation.FailureCodes,
                    ExecutionCoordinatorEventDisposition.Rejected
                );
            }

            if (state.PlanLifecycle ==
                    ExecutionCoordinatorPlanLifecycle.Terminal)
            {
                return Stale(state, "Plan is terminal.");
            }

            ReactionExecutionStep eventStep = FindPlanStep(
                plan.SourcePlan,
                coordinatorEvent.StepId
            );
            if (!string.IsNullOrEmpty(coordinatorEvent.StepId) &&
                eventStep == null)
            {
                return Stale(state, "Step is not part of the plan.");
            }

            ExecutionCoordinatorStepState currentStep =
                !string.IsNullOrEmpty(coordinatorEvent.StepId)
                    ? state.FindStep(coordinatorEvent.StepId)
                    : null;
            if (eventStep != null && currentStep == null)
                return Stale(state, "Runtime step is missing.");

            if (RequiresCurrentAttempt(coordinatorEvent.EventType) &&
                (currentStep == null ||
                 !Same(
                     currentStep.CurrentAttemptId,
                     coordinatorEvent.ExecutionAttemptId)))
            {
                return Stale(state, "Execution attempt is stale.");
            }

            ExecutionCoordinatorReductionResult leaseGuard =
                GuardLeaseEvent(
                    state,
                    eventStep,
                    currentStep,
                    coordinatorEvent
                );
            if (leaseGuard != null)
                return leaseGuard;

            ExecutionCoordinatorReductionResult resourceWaitGuard =
                GuardResourceWaitEvent(
                    state,
                    eventStep,
                    currentStep,
                    coordinatorEvent
                );
            if (resourceWaitGuard != null)
                return resourceWaitGuard;

            if (state.PlanLifecycle ==
                    ExecutionCoordinatorPlanLifecycle.SafetyPreempting &&
                !IsSafetyOrCleanupEvent(coordinatorEvent.EventType))
            {
                return Stale(state, "Normal event after emergency preempt.");
            }

            MutableReduction reduction = new MutableReduction(
                plan.SourcePlan,
                state,
                context,
                coordinatorEvent
            );

            switch (coordinatorEvent.EventType)
            {
                case ExecutionCoordinatorEventType.PlanReceived:
                    break;
                case ExecutionCoordinatorEventType.PlanStartRequested:
                    reduction.StartPlan();
                    break;
                case ExecutionCoordinatorEventType.PermissionGranted:
                    reduction.PermissionGranted(eventStep);
                    break;
                case ExecutionCoordinatorEventType.PermissionDenied:
                    reduction.PermissionDenied(eventStep);
                    break;
                case ExecutionCoordinatorEventType.PermissionWaitTimedOut:
                    reduction.PermissionTimedOut(eventStep);
                    break;
                case ExecutionCoordinatorEventType.LeaseAcquired:
                    reduction.LeaseAcquired(eventStep);
                    break;
                case ExecutionCoordinatorEventType.LeaseRejected:
                    reduction.LeaseRejected(eventStep);
                    break;
                case ExecutionCoordinatorEventType.LeaseReleased:
                    reduction.LeaseReleased(eventStep);
                    break;
                case ExecutionCoordinatorEventType.LeaseReleaseFailed:
                    reduction.LeaseReleaseFailed(eventStep);
                    break;
                case ExecutionCoordinatorEventType
                    .ResourceAvailabilityEvaluated:
                    reduction.ResourceAvailabilityEvaluated(eventStep);
                    break;
                case ExecutionCoordinatorEventType.ResourceWaitTimedOut:
                    reduction.ResourceWaitTimedOut(eventStep);
                    break;
                case ExecutionCoordinatorEventType.ExecutorStartAccepted:
                    reduction.ExecutorStartAccepted(eventStep);
                    break;
                case ExecutionCoordinatorEventType.ExecutorStartRejected:
                    reduction.ExecutorStartRejected(eventStep);
                    break;
                case ExecutionCoordinatorEventType.DomainStageChanged:
                    reduction.DomainStageChanged(eventStep);
                    break;
                case ExecutionCoordinatorEventType.CompletionPolicyReached:
                    if (!reduction.CompletionPolicyReached(eventStep))
                    {
                        return Rejected(
                            state,
                            "CompletionPolicyReached has no matching " +
                            "domain-stage evidence."
                        );
                    }
                    break;
                case ExecutionCoordinatorEventType.ExecutorTerminal:
                    reduction.ExecutorTerminal(eventStep);
                    break;
                case ExecutionCoordinatorEventType.StepTimedOut:
                case ExecutionCoordinatorEventType.AttemptTimedOut:
                    reduction.TimedOut(eventStep);
                    break;
                case ExecutionCoordinatorEventType.CleanupCompleted:
                    reduction.CleanupCompleted(eventStep);
                    break;
                case ExecutionCoordinatorEventType.CleanupFailed:
                    reduction.CleanupFailed(eventStep);
                    break;
                case ExecutionCoordinatorEventType.CancelRequested:
                case ExecutionCoordinatorEventType.InterruptRequested:
                case ExecutionCoordinatorEventType.SafetyStopRequested:
                case ExecutionCoordinatorEventType
                    .EmergencyPreemptRequested:
                    reduction.ControlRequested();
                    break;
                case ExecutionCoordinatorEventType.ControlDispatched:
                    reduction.ControlDispatched();
                    break;
                case ExecutionCoordinatorEventType.ControlEffectConfirmed:
                    reduction.ControlEffectConfirmed();
                    break;
                case ExecutionCoordinatorEventType.ControlEffectFailed:
                    reduction.ControlEffectFailed();
                    break;
                default:
                    return Rejected(state, "Unsupported event type.");
            }

            return reduction.Finish();
        }

        private static ExecutionCoordinatorReductionResult Rejected(
            ExecutionCoordinatorRuntimeState state,
            string diagnostic)
        {
            return new ExecutionCoordinatorReductionResult(
                state,
                new ExecutionCoordinatorEffectIntent[0],
                new[] { diagnostic ?? string.Empty },
                ExecutionCoordinatorEventDisposition.Rejected
            );
        }

        private static ExecutionCoordinatorReductionResult Stale(
            ExecutionCoordinatorRuntimeState state,
            string diagnostic)
        {
            return new ExecutionCoordinatorReductionResult(
                state,
                new ExecutionCoordinatorEffectIntent[0],
                new[]
                {
                    ExecutionCoordinatorDiagnosticCodes.StaleEvent,
                    diagnostic ?? string.Empty
                },
                ExecutionCoordinatorEventDisposition.Stale
            );
        }

        private static bool Contains(
            IReadOnlyList<string> values,
            string value)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (Same(values[i], value))
                    return true;
            }

            return false;
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(left, right, StringComparison.Ordinal);
        }

        private static bool HasIncomingDependency(
            ReactionExecutionPlan plan,
            string stepId)
        {
            for (int i = 0; i < plan.Dependencies.Count; i++)
            {
                if (Same(plan.Dependencies[i].SuccessorStepId, stepId))
                    return true;
            }

            return false;
        }

        private static ReactionExecutionStep FindPlanStep(
            ReactionExecutionPlan plan,
            string stepId)
        {
            if (string.IsNullOrEmpty(stepId))
                return null;

            for (int i = 0; i < plan.Steps.Count; i++)
            {
                if (Same(plan.Steps[i].StepId, stepId))
                    return plan.Steps[i];
            }

            return null;
        }

        private static ExecutionResourceRequirement FindResource(
            ReactionExecutionStep step,
            string resourceId)
        {
            if (step == null)
                return null;
            for (int i = 0; i < step.RequiredResources.Count; i++)
            {
                if (Same(
                        step.RequiredResources[i].ResourceId,
                        resourceId))
                {
                    return step.RequiredResources[i];
                }
            }
            return null;
        }

        private static ExecutionCoordinatorReductionResult GuardLeaseEvent(
            ExecutionCoordinatorRuntimeState state,
            ReactionExecutionStep definition,
            ExecutionCoordinatorStepState step,
            ExecutionCoordinatorEvent coordinatorEvent)
        {
            if (!IsLeaseEvent(coordinatorEvent.EventType))
                return null;

            ResourceLeaseReference incoming =
                coordinatorEvent.ResourceLease.Lease;
            ExecutionResourceRequirement requirement = FindResource(
                definition,
                incoming.ResourceId
            );
            if (requirement == null)
                return Stale(state, "Lease resource is not required by step.");
            if (requirement.AccessMode != incoming.AccessMode)
                return Stale(state, "Lease access mode mismatch.");
            if (!string.IsNullOrEmpty(incoming.ExecutionAttemptId) &&
                (step == null ||
                 !Same(
                     step.CurrentAttemptId,
                     incoming.ExecutionAttemptId)))
            {
                return Stale(state, "Lease execution attempt is stale.");
            }

            for (int i = 0; i < state.ResourceLeaseReferences.Count; i++)
            {
                ResourceLeaseReference existing =
                    state.ResourceLeaseReferences[i];
                bool sameIdentity = Same(
                        existing.LeaseId,
                        incoming.LeaseId) &&
                    Same(existing.ResourceId, incoming.ResourceId);
                if (coordinatorEvent.EventType ==
                        ExecutionCoordinatorEventType.LeaseAcquired &&
                    sameIdentity)
                {
                    if (existing.Status ==
                        ExecutionResourceLeaseStatus.Acquired)
                    {
                        return new ExecutionCoordinatorReductionResult(
                            state,
                            new ExecutionCoordinatorEffectIntent[0],
                            new[]
                            {
                                ExecutionCoordinatorDiagnosticCodes
                                    .DuplicateLease
                            },
                            ExecutionCoordinatorEventDisposition.Duplicate
                        );
                    }
                    return Stale(state, "Released lease cannot be reused.");
                }

                if (coordinatorEvent.EventType ==
                        ExecutionCoordinatorEventType.LeaseAcquired &&
                    Same(existing.ResourceId, incoming.ResourceId) &&
                    existing.Status == ExecutionResourceLeaseStatus.Acquired &&
                    (existing.AccessMode ==
                        ExecutionResourceAccessMode.Exclusive ||
                     incoming.AccessMode ==
                        ExecutionResourceAccessMode.Exclusive))
                {
                    return Stale(
                        state,
                        "Resource already has an active exclusive lease."
                    );
                }
            }

            if (coordinatorEvent.EventType ==
                    ExecutionCoordinatorEventType.LeaseAcquired &&
                (step == null ||
                 step.Lifecycle !=
                    ExecutionCoordinatorStepLifecycle.WaitingForResource))
            {
                return Stale(
                    state,
                    "Lease acquired outside resource-waiting state."
                );
            }

            if (coordinatorEvent.EventType ==
                    ExecutionCoordinatorEventType.LeaseReleased ||
                coordinatorEvent.EventType ==
                    ExecutionCoordinatorEventType.LeaseReleaseFailed)
            {
                bool found = false;
                for (int i = 0; i < state.ResourceLeaseReferences.Count; i++)
                {
                    ResourceLeaseReference existing =
                        state.ResourceLeaseReferences[i];
                    if (Same(existing.LeaseId, incoming.LeaseId) &&
                        Same(existing.ResourceId, incoming.ResourceId) &&
                        existing.Status !=
                            ExecutionResourceLeaseStatus.Released &&
                        existing.Status !=
                            ExecutionResourceLeaseStatus.Rejected)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    return Stale(state, "Lease release target is stale.");
            }
            return null;
        }

        private static ExecutionCoordinatorReductionResult
            GuardResourceWaitEvent(
                ExecutionCoordinatorRuntimeState state,
                ReactionExecutionStep definition,
                ExecutionCoordinatorStepState step,
                ExecutionCoordinatorEvent coordinatorEvent)
        {
            bool availability = coordinatorEvent.EventType ==
                ExecutionCoordinatorEventType.ResourceAvailabilityEvaluated;
            bool timeout = coordinatorEvent.EventType ==
                ExecutionCoordinatorEventType.ResourceWaitTimedOut;
            if (!availability && !timeout)
                return null;

            if (definition == null || step == null ||
                step.Lifecycle !=
                    ExecutionCoordinatorStepLifecycle.WaitingForResource ||
                string.IsNullOrWhiteSpace(step.ResourceWaitCycleId) ||
                !step.ResourceWaitStartedAtUtc.HasValue)
            {
                return Stale(state, "Resource-wait cycle is not active.");
            }

            if (availability)
            {
                ExecutionResourceAvailabilityEventPayload payload =
                    coordinatorEvent.ResourceAvailability;
                ExecutionResourceRequirement requirement = FindResource(
                    definition,
                    payload.ResourceId
                );
                if (requirement == null)
                {
                    return Stale(
                        state,
                        "Availability resource is not required by step."
                    );
                }
                if (!Same(
                        payload.ResourceWaitCycleId,
                        step.ResourceWaitCycleId) ||
                    !Same(payload.RequestId, definition.RequestId) ||
                    payload.ObservedAtUtc <
                        step.ResourceWaitStartedAtUtc.Value)
                {
                    return Stale(
                        state,
                        "Availability correlation is stale."
                    );
                }
                return null;
            }

            ExecutionResourceWaitTimeoutPolicy policy =
                definition.ResourceWaitTimeoutPolicy;
            ExecutionResourceWaitTimeoutEventPayload timeoutPayload =
                coordinatorEvent.ResourceWaitTimeout;
            if (policy == null || !policy.TimeoutEnabled ||
                !step.ResourceWaitDeadlineUtc.HasValue)
            {
                return Rejected(
                    state,
                    "Resource-wait timeout is disabled."
                );
            }
            if (!Same(
                    timeoutPayload.ResourceWaitCycleId,
                    step.ResourceWaitCycleId) ||
                timeoutPayload.DeadlineUtc !=
                    step.ResourceWaitDeadlineUtc.Value)
            {
                return Stale(state, "Resource-wait timeout is stale.");
            }
            if (timeoutPayload.EvaluationTimeUtc <
                step.ResourceWaitDeadlineUtc.Value)
            {
                return Rejected(
                    state,
                    "Resource-wait deadline has not been reached."
                );
            }
            return null;
        }

        private static bool IsLeaseEvent(
            ExecutionCoordinatorEventType type)
        {
            return type == ExecutionCoordinatorEventType.LeaseAcquired ||
                type == ExecutionCoordinatorEventType.LeaseRejected ||
                type == ExecutionCoordinatorEventType.LeaseReleased ||
                type == ExecutionCoordinatorEventType.LeaseReleaseFailed;
        }

        private static ExecutionCoordinatorStepState NewStepState(
            string stepId,
            ExecutionCoordinatorStepLifecycle lifecycle,
            bool dependencySatisfied)
        {
            return new ExecutionCoordinatorStepState(
                stepId,
                lifecycle,
                null,
                false,
                dependencySatisfied,
                false,
                string.Empty,
                new string[0],
                new string[0],
                null,
                null,
                ExecutionTerminalOutcome.None,
                ExecutionTerminalOutcome.None,
                new string[0]
            );
        }

        private static bool RequiresCurrentAttempt(
            ExecutionCoordinatorEventType type)
        {
            return type == ExecutionCoordinatorEventType
                    .ExecutorStartAccepted ||
                type == ExecutionCoordinatorEventType
                    .ExecutorStartRejected ||
                type == ExecutionCoordinatorEventType.DomainStageChanged ||
                type == ExecutionCoordinatorEventType
                    .CompletionPolicyReached ||
                type == ExecutionCoordinatorEventType.ExecutorTerminal ||
                type == ExecutionCoordinatorEventType.AttemptTimedOut;
        }

        private static bool IsSafetyOrCleanupEvent(
            ExecutionCoordinatorEventType type)
        {
            return type == ExecutionCoordinatorEventType
                    .EmergencyPreemptRequested ||
                type == ExecutionCoordinatorEventType.SafetyStopRequested ||
                type == ExecutionCoordinatorEventType.ControlDispatched ||
                type == ExecutionCoordinatorEventType
                    .ControlEffectConfirmed ||
                type == ExecutionCoordinatorEventType.ControlEffectFailed ||
                type == ExecutionCoordinatorEventType.LeaseReleased ||
                type == ExecutionCoordinatorEventType
                    .LeaseReleaseFailed ||
                type == ExecutionCoordinatorEventType.CleanupCompleted ||
                type == ExecutionCoordinatorEventType.CleanupFailed;
        }

        private sealed class MutableReduction
        {
            private readonly ReactionExecutionPlan plan;
            private readonly ExecutionCoordinatorRuntimeState originalState;
            private readonly ExecutionCoordinatorReducerContext context;
            private readonly ExecutionCoordinatorEvent currentEvent;
            private readonly List<ExecutionCoordinatorStepState> steps;
            private readonly List<ExecutionCoordinatorAttemptState> attempts;
            private readonly List<ResourceLeaseReference> leases;
            private readonly List<ExecutionCoordinatorControlState> controls;
            private readonly List<string> processed;
            private readonly List<string> diagnostics;
            private readonly List<ExecutionCoordinatorEffectIntent> intents;
            private int intentIndex;
            private bool contextFailure;
            private ExecutionCoordinatorPlanLifecycle planLifecycle;
            private DateTime? startedAtUtc;
            private ExecutionTerminalOutcome finalOutcome;

            public MutableReduction(
                ReactionExecutionPlan plan,
                ExecutionCoordinatorRuntimeState state,
                ExecutionCoordinatorReducerContext context,
                ExecutionCoordinatorEvent currentEvent)
            {
                this.plan = plan;
                originalState = state;
                this.context = context;
                this.currentEvent = currentEvent;
                steps = new List<ExecutionCoordinatorStepState>(
                    state.StepStates
                );
                attempts = new List<ExecutionCoordinatorAttemptState>(
                    state.AttemptStates
                );
                leases = new List<ResourceLeaseReference>(
                    state.ResourceLeaseReferences
                );
                controls = new List<ExecutionCoordinatorControlState>(
                    state.PendingControlRequests
                );
                processed = new List<string>(state.ProcessedEventIds);
                diagnostics = new List<string>(state.Diagnostics);
                intents = new List<ExecutionCoordinatorEffectIntent>();
                planLifecycle = state.PlanLifecycle;
                startedAtUtc = state.StartedAtUtc;
                finalOutcome = state.FinalOutcome;
            }

            public void StartPlan()
            {
                if (planLifecycle != ExecutionCoordinatorPlanLifecycle.Accepted)
                    return;

                planLifecycle = ExecutionCoordinatorPlanLifecycle.Running;
                startedAtUtc = context.NowUtc;
                StartReadySteps();
            }

            public void PermissionGranted(ReactionExecutionStep definition)
            {
                ExecutionCoordinatorStepState step = Step(definition.StepId);
                if (step.Lifecycle == ExecutionCoordinatorStepLifecycle
                    .WaitingForPermissionBeforeResources)
                {
                    if (definition.RequiredResources.Count > 0)
                    {
                        DateTime? resourceWaitDeadlineUtc = null;
                        ExecutionResourceWaitTimeoutPolicy waitPolicy =
                            definition.ResourceWaitTimeoutPolicy;
                        if (waitPolicy != null &&
                            waitPolicy.TimeoutEnabled)
                        {
                            resourceWaitDeadlineUtc = context.NowUtc.AddSeconds(
                                waitPolicy.TimeoutSeconds
                            );
                        }
                        Replace(step, CopyStep(
                            step,
                            lifecycle: ExecutionCoordinatorStepLifecycle
                                .WaitingForResource,
                            resourceWaitCycleId: currentEvent.EventId,
                            resourceWaitStartedAtUtc: context.NowUtc,
                            resourceWaitDeadlineUtc:
                                resourceWaitDeadlineUtc
                        ));
                        for (int i = 0;
                            i < definition.RequiredResources.Count;
                            i++)
                        {
                            AddResourceIntent(
                                ExecutionCoordinatorEffectIntentType
                                    .AcquireResourceLease,
                                definition,
                                definition.RequiredResources[i],
                                string.Empty
                            );
                        }
                    }
                    else
                    {
                        Replace(step, CopyStep(
                            step,
                            lifecycle: ExecutionCoordinatorStepLifecycle
                                .WaitingForPermissionBeforeStart
                        ));
                        AddPermissionIntent(definition);
                    }
                    return;
                }

                if (step.Lifecycle == ExecutionCoordinatorStepLifecycle
                    .WaitingForPermissionBeforeStart)
                {
                    StartAttempt(definition, step);
                }
            }

            public void PermissionDenied(ReactionExecutionStep definition)
            {
                ExecutionCoordinatorStepState step = Step(definition.StepId);
                ExecutionPermissionWaitPolicy policy =
                    definition.PermissionWaitPolicy;
                if (policy != null && policy.Behavior ==
                    ExecutionPermissionWaitBehavior.WaitWithTimeout)
                {
                    AddIntent(
                        ExecutionCoordinatorEffectIntentType
                            .StartTimeoutWatch,
                        definition,
                        string.Empty,
                        null,
                        new ExecutionTimeoutPolicy(
                            policy.TimeoutSeconds,
                            ExecutionTimeoutAction.Fail
                        )
                    );
                    planLifecycle = ExecutionCoordinatorPlanLifecycle.Waiting;
                    return;
                }

                ExecutionTerminalOutcome outcome =
                    definition.Requiredness ==
                        ExecutionRequiredness.Optional
                        ? ExecutionTerminalOutcome.Rejected
                        : ExecutionTerminalOutcome.Failed;
                BeginTerminal(definition, step, outcome, new[]
                {
                    ExecutionFailureCodes.PermissionDenied
                });
            }

            public void PermissionTimedOut(ReactionExecutionStep definition)
            {
                BeginTerminal(
                    definition,
                    Step(definition.StepId),
                    ExecutionTerminalOutcome.TimedOut,
                    new[] { ExecutionFailureCodes.TimedOut }
                );
            }

            public void LeaseAcquired(ReactionExecutionStep definition)
            {
                ExecutionCoordinatorStepState step = Step(definition.StepId);
                if (step.Lifecycle !=
                    ExecutionCoordinatorStepLifecycle.WaitingForResource)
                {
                    return;
                }

                ResourceLeaseReference acquired =
                    currentEvent.ResourceLease.Lease;
                string leaseId = acquired.LeaseId;
                leases.Add(acquired);
                List<string> leaseIds = new List<string>(
                    step.ActiveLeaseIds
                );
                leaseIds.Add(leaseId);
                bool allRequired = AllRequiredResourcesAcquired(
                    definition
                );
                Replace(step, CopyStep(
                    step,
                    lifecycle: allRequired
                        ? ExecutionCoordinatorStepLifecycle
                            .WaitingForPermissionBeforeStart
                        : ExecutionCoordinatorStepLifecycle
                            .WaitingForResource,
                    activeLeaseIds: leaseIds,
                    clearResourceWait: allRequired
                ));
                if (allRequired)
                    AddPermissionIntent(definition);
            }

            public void LeaseRejected(ReactionExecutionStep definition)
            {
                ResourceLeaseReference rejected =
                    currentEvent.ResourceLease.Lease;
                leases.Add(rejected);
                ExecutionResourceRequirement requirement = FindResource(
                    definition,
                    rejected.ResourceId
                );

                if (requirement.Requiredness ==
                    ExecutionRequiredness.Required)
                {
                    BeginTerminal(
                        definition,
                        Step(definition.StepId),
                        ExecutionTerminalOutcome.Failed,
                        new[] { ExecutionFailureCodes.ResourceUnavailable }
                    );
                }
                else
                {
                    ExecutionCoordinatorStepState step =
                        Step(definition.StepId);
                    bool allRequired = AllRequiredResourcesAcquired(
                        definition
                    );
                    if (allRequired)
                    {
                        Replace(step, CopyStep(
                            step,
                            lifecycle: ExecutionCoordinatorStepLifecycle
                                .WaitingForPermissionBeforeStart,
                            clearResourceWait: true
                        ));
                        AddPermissionIntent(definition);
                    }
                }
            }

            public void ResourceAvailabilityEvaluated(
                ReactionExecutionStep definition)
            {
                ExecutionResourceAvailabilityEventPayload payload =
                    currentEvent.ResourceAvailability;
                if (payload.Availability !=
                    ResourceShadowAvailabilityStatus.Available)
                {
                    return;
                }

                if (IsResourceAcquired(
                        definition.StepId,
                        payload.ResourceId))
                {
                    return;
                }

                ExecutionResourceRequirement requirement = FindResource(
                    definition,
                    payload.ResourceId
                );
                AddResourceIntent(
                    ExecutionCoordinatorEffectIntentType
                        .AcquireResourceLease,
                    definition,
                    requirement,
                    string.Empty
                );
            }

            public void ResourceWaitTimedOut(
                ReactionExecutionStep definition)
            {
                BeginTerminal(
                    definition,
                    Step(definition.StepId),
                    ExecutionTerminalOutcome.TimedOut,
                    new[] { ExecutionFailureCodes.TimedOut }
                );
            }

            public void LeaseReleased(ReactionExecutionStep definition)
            {
                ResourceLeaseReference released =
                    currentEvent.ResourceLease.Lease;
                UpdateLeaseStatus(
                    released.LeaseId,
                    released.ResourceId,
                    ExecutionResourceLeaseStatus.Released
                );
                ExecutionCoordinatorStepState step = Step(definition.StepId);
                List<string> active = new List<string>(step.ActiveLeaseIds);
                active.Remove(released.LeaseId);
                Replace(step, CopyStep(
                    step,
                    activeLeaseIds: active
                ));
            }

            public void LeaseReleaseFailed(ReactionExecutionStep definition)
            {
                ResourceLeaseReference failed =
                    currentEvent.ResourceLease.Lease;
                UpdateLeaseStatus(
                    failed.LeaseId,
                    failed.ResourceId,
                    ExecutionResourceLeaseStatus.ReleaseFailed
                );
                AddDiagnostic(
                    ExecutionCoordinatorDiagnosticCodes
                        .ResourceReleaseFailed
                );
                CleanupFailed(definition);
            }

            public void ExecutorStartAccepted(
                ReactionExecutionStep definition)
            {
                ExecutionCoordinatorStepState step = Step(definition.StepId);
                if (step.Lifecycle !=
                    ExecutionCoordinatorStepLifecycle.Starting)
                {
                    return;
                }

                Replace(step, CopyStep(
                    step,
                    lifecycle: ExecutionCoordinatorStepLifecycle.Running,
                    executorAccepted: true,
                    startedAtUtc: context.NowUtc
                ));
                UpdateAttempt(
                    currentEvent.ExecutionAttemptId,
                    ExecutionAttemptLifecycle.Running,
                    null,
                    null,
                    null
                );
                UnlockDependencies();
            }

            public void ExecutorStartRejected(
                ReactionExecutionStep definition)
            {
                UpdateAttempt(
                    currentEvent.ExecutionAttemptId,
                    ExecutionAttemptLifecycle.Terminal,
                    context.NowUtc,
                    ExecutionTerminalOutcome.Failed,
                    currentEvent.FailureCodes
                );
                BeginTerminal(
                    definition,
                    Step(definition.StepId),
                    ExecutionTerminalOutcome.Failed,
                    currentEvent.FailureCodes
                );
            }

            public void DomainStageChanged(
                ReactionExecutionStep definition)
            {
                ExecutionCoordinatorStepState step = Step(definition.StepId);
                Replace(step, CopyStep(
                    step,
                    lifecycle: ExecutionCoordinatorStepLifecycle.Running,
                    domainStage: currentEvent.DomainStage
                ));
                UpdateAttempt(
                    currentEvent.ExecutionAttemptId,
                    ExecutionAttemptLifecycle.Running,
                    null,
                    null,
                    null,
                    currentEvent.DomainStage
                );
                UnlockDependencies();
            }

            public bool CompletionPolicyReached(
                ReactionExecutionStep definition)
            {
                ExecutionCoordinatorStepState step = Step(definition.StepId);
                if (!CompletionEvidenceMatches(
                        definition.CompletionPolicy,
                        step.DomainStage))
                {
                    return false;
                }

                Replace(step, CopyStep(
                    step,
                    lifecycle: ExecutionCoordinatorStepLifecycle
                        .WaitingForCompletion,
                    completionPolicySatisfied: true
                ));
                UnlockDependencies();
                return true;
            }

            public void ExecutorTerminal(ReactionExecutionStep definition)
            {
                ExecutionCoordinatorStepState step = Step(definition.StepId);
                ExecutionTerminalOutcome outcome =
                    currentEvent.TerminalOutcome;

                if ((outcome == ExecutionTerminalOutcome.Succeeded ||
                     outcome == ExecutionTerminalOutcome
                        .SucceededUnverified) &&
                    !step.CompletionPolicySatisfied)
                {
                    outcome = ExecutionTerminalOutcome.ContinueImpossible;
                }
                else if (outcome == ExecutionTerminalOutcome.Succeeded &&
                    definition.CompletionPolicy != null &&
                    definition.CompletionPolicy.AllowsUnverifiedCompletion &&
                    !definition.CompletionPolicy.RequiresVerifiedCompletion)
                {
                    outcome = ExecutionTerminalOutcome.SucceededUnverified;
                    AddDiagnostic(
                        ExecutionCoordinatorDiagnosticCodes
                            .CompletionUnverified
                    );
                }

                UpdateAttempt(
                    currentEvent.ExecutionAttemptId,
                    ExecutionAttemptLifecycle.Terminal,
                    context.NowUtc,
                    outcome,
                    currentEvent.FailureCodes
                );

                if (CanRetry(definition, step, currentEvent.FailureCodes))
                {
                    Replace(step, CopyStep(
                        step,
                        lifecycle: ExecutionCoordinatorStepLifecycle
                            .WaitingForPermissionBeforeStart,
                        currentAttemptId: string.Empty,
                        pendingTerminalOutcome:
                            ExecutionTerminalOutcome.None,
                        failureCodes: currentEvent.FailureCodes
                    ));
                    AddPermissionIntent(definition);
                    return;
                }

                BeginTerminal(
                    definition,
                    step,
                    outcome,
                    currentEvent.FailureCodes
                );
            }

            public void TimedOut(ReactionExecutionStep definition)
            {
                ExecutionCoordinatorStepState step = Step(definition.StepId);
                if (!string.IsNullOrEmpty(step.CurrentAttemptId))
                {
                    AddIntent(
                        ExecutionCoordinatorEffectIntentType
                            .RequestExecutorCancel,
                        definition,
                        "timeout:" + currentEvent.EventId,
                        null,
                        null
                    );
                }
                BeginTerminal(
                    definition,
                    step,
                    ExecutionTerminalOutcome.TimedOut,
                    new[] { ExecutionFailureCodes.TimedOut }
                );
            }

            public void CleanupCompleted(ReactionExecutionStep definition)
            {
                ExecutionCoordinatorStepState step = Step(definition.StepId);
                if (step.ActiveLeaseIds.Count > 0)
                {
                    AddDiagnostic("CLEANUP_HAS_ACTIVE_LEASES");
                    return;
                }
                ExecutionTerminalOutcome outcome =
                    step.PendingTerminalOutcome ==
                        ExecutionTerminalOutcome.None
                        ? ExecutionTerminalOutcome.Succeeded
                        : step.PendingTerminalOutcome;
                FinalizeStep(definition, step, outcome, step.FailureCodes);
            }

            public void CleanupFailed(ReactionExecutionStep definition)
            {
                AddDiagnostic(
                    ExecutionCoordinatorDiagnosticCodes.CleanupFailed
                );
                FinalizeStep(
                    definition,
                    Step(definition.StepId),
                    ExecutionTerminalOutcome.Failed,
                    new[] { ExecutionFailureCodes.CleanupFailed }
                );
            }

            public void ControlRequested()
            {
                ExecutionControlType type = ControlType(
                    currentEvent.EventType
                );
                int rank = ExecutionControlPriorityPolicyV01.GetRank(type);
                int highest = 0;
                for (int i = 0; i < controls.Count; i++)
                    highest = Math.Max(highest, controls[i].PriorityRank);

                if (rank < highest)
                {
                    AddDiagnostic("LOWER_PRIORITY_CONTROL_IGNORED");
                    return;
                }

                controls.Add(new ExecutionCoordinatorControlState(
                    currentEvent.ControlRequestId,
                    type,
                    rank,
                    false,
                    false
                ));

                bool emergency = type ==
                    ExecutionControlType.EmergencyPreempt;
                planLifecycle = emergency
                    ? ExecutionCoordinatorPlanLifecycle.SafetyPreempting
                    : ExecutionCoordinatorPlanLifecycle.Cancelling;
                ExecutionTerminalOutcome outcome = ControlOutcome(type);

                for (int i = 0; i < steps.Count; i++)
                {
                    ExecutionCoordinatorStepState step = steps[i];
                    if (step.Lifecycle ==
                        ExecutionCoordinatorStepLifecycle.Terminal)
                    {
                        continue;
                    }

                    ReactionExecutionStep definition =
                        FindPlanStep(plan, step.StepId);
                    if (step.Lifecycle ==
                            ExecutionCoordinatorStepLifecycle.Running ||
                        step.Lifecycle ==
                            ExecutionCoordinatorStepLifecycle.Starting ||
                        step.Lifecycle == ExecutionCoordinatorStepLifecycle
                            .WaitingForCompletion ||
                        step.Lifecycle ==
                            ExecutionCoordinatorStepLifecycle.Cancelling)
                    {
                        Replace(step, CopyStep(
                            step,
                            lifecycle: ExecutionCoordinatorStepLifecycle
                                .Cancelling,
                            pendingTerminalOutcome: outcome
                        ));
                        AddIntent(
                            emergency
                                ? ExecutionCoordinatorEffectIntentType
                                    .RequestSafetyPreempt
                                : type == ExecutionControlType.Interrupt
                                    ? ExecutionCoordinatorEffectIntentType
                                        .RequestExecutorInterrupt
                                    : ExecutionCoordinatorEffectIntentType
                                        .RequestExecutorCancel,
                            definition,
                            currentEvent.ControlRequestId,
                            null,
                            null
                        );
                    }
                    else
                    {
                        BeginTerminal(
                            definition,
                            step,
                            outcome,
                            new[] { ControlFailureCode(type) }
                        );
                    }
                }

                AggregatePlan();
            }

            public void ControlDispatched()
            {
                UpdateControl(currentEvent.ControlRequestId, true, false);
            }

            public void ControlEffectConfirmed()
            {
                ExecutionCoordinatorControlState control =
                    FindControl(currentEvent.ControlRequestId);
                if (control == null)
                    return;
                UpdateControl(currentEvent.ControlRequestId, true, true);
                ExecutionTerminalOutcome outcome =
                    ControlOutcome(control.ControlType);

                for (int i = steps.Count - 1; i >= 0; i--)
                {
                    ExecutionCoordinatorStepState step = steps[i];
                    if (step.Lifecycle !=
                        ExecutionCoordinatorStepLifecycle.Cancelling)
                    {
                        continue;
                    }

                    ReactionExecutionStep definition =
                        FindPlanStep(plan, step.StepId);
                    BeginTerminal(
                        definition,
                        step,
                        outcome,
                        new[] { ControlFailureCode(control.ControlType) }
                    );
                }
            }

            public void ControlEffectFailed()
            {
                AddDiagnostic(
                    ExecutionCoordinatorDiagnosticCodes.ControlEffectFailed
                );
                ExecutionCoordinatorControlState control =
                    FindControl(currentEvent.ControlRequestId);
                if (control == null)
                    return;
                UpdateControl(currentEvent.ControlRequestId, true, false);

                ExecutionTerminalOutcome outcome = control.ControlType ==
                    ExecutionControlType.EmergencyPreempt
                        ? ExecutionTerminalOutcome.SafetyPreempted
                        : ExecutionTerminalOutcome.Failed;
                for (int i = steps.Count - 1; i >= 0; i--)
                {
                    if (steps[i].Lifecycle ==
                        ExecutionCoordinatorStepLifecycle.Cancelling)
                    {
                        ReactionExecutionStep definition =
                            FindPlanStep(plan, steps[i].StepId);
                        BeginTerminal(
                            definition,
                            steps[i],
                            outcome,
                            currentEvent.FailureCodes
                        );
                    }
                }
            }

            public ExecutionCoordinatorReductionResult Finish()
            {
                if (contextFailure)
                {
                    return new ExecutionCoordinatorReductionResult(
                        originalState,
                        new ExecutionCoordinatorEffectIntent[0],
                        new[]
                        {
                            ExecutionCoordinatorDiagnosticCodes
                                .MissingContextId
                        },
                        ExecutionCoordinatorEventDisposition.Rejected
                    );
                }

                if (!Contains(processed, currentEvent.EventId))
                    processed.Add(currentEvent.EventId);

                UnlockDependencies();
                AggregatePlan();

                ExecutionCoordinatorRuntimeState resultState =
                    new ExecutionCoordinatorRuntimeState(
                        plan.PlanId,
                        plan.PlanGeneration,
                        planLifecycle,
                        steps,
                        attempts,
                        leases,
                        controls,
                        processed,
                        startedAtUtc,
                        context.NowUtc,
                        finalOutcome,
                        diagnostics
                    );
                return new ExecutionCoordinatorReductionResult(
                    resultState,
                    intents,
                    diagnostics,
                    ExecutionCoordinatorEventDisposition.Applied
                );
            }

            private void StartReadySteps()
            {
                for (int i = steps.Count - 1; i >= 0; i--)
                {
                    ExecutionCoordinatorStepState step = steps[i];
                    if (step.Lifecycle !=
                        ExecutionCoordinatorStepLifecycle.Ready)
                    {
                        continue;
                    }

                    ReactionExecutionStep definition =
                        FindPlanStep(plan, step.StepId);
                    if (ExecutionNoOpContract.IsNoOpStep(definition))
                    {
                        CompleteNoOp(definition, step);
                        continue;
                    }
                    Replace(step, CopyStep(
                        step,
                        lifecycle: ExecutionCoordinatorStepLifecycle
                            .WaitingForPermissionBeforeResources
                    ));
                    AddPermissionIntent(definition);
                }
            }

            private void CompleteNoOp(
                ReactionExecutionStep definition,
                ExecutionCoordinatorStepState step)
            {
                ExecutionCoordinatorStepState completed = CopyStep(
                    step,
                    domainStage: new ExecutionDomainStage(
                        ExecutionDomainStageIds.LogicalNoOpCompleted),
                    completionPolicySatisfied: true,
                    startedAtUtc: context.NowUtc);
                Replace(step, completed);
                FinalizeStep(
                    definition,
                    completed,
                    ExecutionTerminalOutcome.Succeeded,
                    new string[0]);
            }

            private void AddPermissionIntent(
                ReactionExecutionStep definition)
            {
                AddIntent(
                    ExecutionCoordinatorEffectIntentType.CheckPermission,
                    definition,
                    string.Empty,
                    null,
                    null
                );
            }

            private void StartAttempt(
                ReactionExecutionStep definition,
                ExecutionCoordinatorStepState step)
            {
                if (string.IsNullOrWhiteSpace(
                        context.NextExecutionAttemptId) ||
                    string.IsNullOrWhiteSpace(context.NextRequestId))
                {
                    contextFailure = true;
                    AddDiagnostic(
                        ExecutionCoordinatorDiagnosticCodes.MissingContextId
                    );
                    return;
                }

                List<string> attemptIds = new List<string>(step.AttemptIds);
                if (Contains(attemptIds, context.NextExecutionAttemptId))
                {
                    contextFailure = true;
                    AddDiagnostic("ATTEMPT_ID_REUSE_REJECTED");
                    return;
                }

                for (int i = 0; i < attempts.Count; i++)
                {
                    if (Same(attempts[i].RequestId, context.NextRequestId))
                    {
                        contextFailure = true;
                        AddDiagnostic("REQUEST_ID_REUSE_REJECTED");
                        return;
                    }
                }

                attemptIds.Add(context.NextExecutionAttemptId);
                attempts.Add(new ExecutionCoordinatorAttemptState(
                    context.NextExecutionAttemptId,
                    definition.StepId,
                    context.NextRequestId,
                    attemptIds.Count,
                    ExecutionAttemptLifecycle.Created,
                    null,
                    null,
                    null,
                    ExecutionTerminalOutcome.None,
                    new string[0]
                ));
                Replace(step, CopyStep(
                    step,
                    lifecycle: ExecutionCoordinatorStepLifecycle.Starting,
                    currentAttemptId: context.NextExecutionAttemptId,
                    attemptIds: attemptIds
                ));
                AddIntent(
                    ExecutionCoordinatorEffectIntentType.StartExecutor,
                    definition,
                    string.Empty,
                    null,
                    null,
                    context.NextExecutionAttemptId,
                    context.NextRequestId
                );
                if (definition.TimeoutPolicy != null &&
                    definition.TimeoutPolicy.TimeoutSeconds > 0d)
                {
                    AddIntent(
                        ExecutionCoordinatorEffectIntentType
                            .StartTimeoutWatch,
                        definition,
                        string.Empty,
                        null,
                        definition.TimeoutPolicy,
                        context.NextExecutionAttemptId,
                        context.NextRequestId
                    );
                }
            }

            private void BeginTerminal(
                ReactionExecutionStep definition,
                ExecutionCoordinatorStepState step,
                ExecutionTerminalOutcome outcome,
                IEnumerable<string> failureCodes)
            {
                if (step.Lifecycle ==
                    ExecutionCoordinatorStepLifecycle.Terminal)
                {
                    return;
                }

                bool needsCleanup = step.ActiveLeaseIds.Count > 0 ||
                    outcome == ExecutionTerminalOutcome.Cancelled ||
                    outcome == ExecutionTerminalOutcome.Interrupted ||
                    outcome == ExecutionTerminalOutcome.SafetyPreempted ||
                    outcome == ExecutionTerminalOutcome.TimedOut;

                if (!needsCleanup)
                {
                    FinalizeStep(definition, step, outcome, failureCodes);
                    return;
                }

                Replace(step, CopyStep(
                    step,
                    lifecycle: ExecutionCoordinatorStepLifecycle.CleaningUp,
                    pendingTerminalOutcome: outcome,
                    failureCodes: failureCodes
                ));

                if (step.ActiveLeaseIds.Count > 0)
                {
                    RequestLeaseReleases(definition, outcome ==
                        ExecutionTerminalOutcome.SafetyPreempted);
                }
                AddIntent(
                    ExecutionCoordinatorEffectIntentType.BeginCleanup,
                    definition,
                    string.Empty,
                    null,
                    null
                );
            }

            private void FinalizeStep(
                ReactionExecutionStep definition,
                ExecutionCoordinatorStepState step,
                ExecutionTerminalOutcome outcome,
                IEnumerable<string> failureCodes)
            {
                Replace(step, CopyStep(
                    step,
                    lifecycle: ExecutionCoordinatorStepLifecycle.Terminal,
                    completedAtUtc: context.NowUtc,
                    terminalOutcome: outcome,
                    pendingTerminalOutcome: ExecutionTerminalOutcome.None,
                    failureCodes: failureCodes
                ));
                if (definition.Requiredness ==
                        ExecutionRequiredness.Optional &&
                    IsFailure(outcome))
                {
                    AddDiagnostic(
                        ExecutionCoordinatorDiagnosticCodes
                            .OptionalStepFailed
                    );
                }
                if (definition.Role == ExecutionStepRole.Fallback &&
                    IsSuccess(outcome))
                {
                    AddDiagnostic(
                        ExecutionCoordinatorDiagnosticCodes.FallbackUsed
                    );
                }
                UnlockDependencies();
                AggregatePlan();
            }

            private bool CanRetry(
                ReactionExecutionStep definition,
                ExecutionCoordinatorStepState step,
                IReadOnlyList<string> failureCodes)
            {
                ExecutionRetryPolicy retry = definition.RetryPolicy;
                if (retry == null || retry.MaxAttempts <= step.AttemptIds.Count)
                    return false;

                for (int i = 0; i < failureCodes.Count; i++)
                {
                    if (Contains(
                            retry.RetryableFailureCodes,
                            failureCodes[i]))
                    {
                        return true;
                    }
                }

                return false;
            }

            private void UnlockDependencies()
            {
                bool changed;
                do
                {
                    changed = false;
                    for (int i = 0; i < steps.Count; i++)
                    {
                        ExecutionCoordinatorStepState step = steps[i];
                        if (step.Lifecycle != ExecutionCoordinatorStepLifecycle
                            .BlockedByDependency)
                        {
                            continue;
                        }

                        if (!DependenciesSatisfied(step.StepId))
                            continue;

                        Replace(step, CopyStep(
                            step,
                            lifecycle: ExecutionCoordinatorStepLifecycle.Ready,
                            dependencySatisfied: true
                        ));
                        changed = true;
                    }
                }
                while (changed);

                if (planLifecycle == ExecutionCoordinatorPlanLifecycle.Running ||
                    planLifecycle == ExecutionCoordinatorPlanLifecycle.Waiting)
                {
                    StartReadySteps();
                }
            }

            private bool DependenciesSatisfied(string successorStepId)
            {
                bool has = false;
                for (int i = 0; i < plan.Dependencies.Count; i++)
                {
                    ExecutionDependency dependency = plan.Dependencies[i];
                    if (!Same(
                            dependency.SuccessorStepId,
                            successorStepId))
                    {
                        continue;
                    }
                    has = true;
                    ExecutionCoordinatorStepState predecessor =
                        Step(dependency.PredecessorStepId);
                    if (!GateSatisfied(dependency, predecessor))
                        return false;
                }

                return has;
            }

            private static bool GateSatisfied(
                ExecutionDependency dependency,
                ExecutionCoordinatorStepState predecessor)
            {
                if (predecessor == null)
                    return false;

                bool terminal = predecessor.Lifecycle ==
                    ExecutionCoordinatorStepLifecycle.Terminal;
                switch (dependency.GateType)
                {
                    case ExecutionDependencyGate.AfterAccepted:
                        return predecessor.ExecutorAccepted;
                    case ExecutionDependencyGate.AfterStarted:
                        return predecessor.StartedAtUtc.HasValue;
                    case ExecutionDependencyGate.AfterCompletionPolicy:
                        return predecessor.CompletionPolicySatisfied;
                    case ExecutionDependencyGate.AfterSuccess:
                        return terminal && IsSuccess(
                            predecessor.TerminalOutcome
                        );
                    case ExecutionDependencyGate.AfterFailure:
                    case ExecutionDependencyGate.Fallback:
                        return terminal && IsFailure(
                            predecessor.TerminalOutcome
                        );
                    case ExecutionDependencyGate.AfterTerminal:
                        return terminal;
                    case ExecutionDependencyGate.AfterSpecificOutcome:
                        return terminal && predecessor.TerminalOutcome ==
                            dependency.RequiredOutcome;
                    default:
                        return false;
                }
            }

            private void AggregatePlan()
            {
                if (planLifecycle == ExecutionCoordinatorPlanLifecycle.Terminal)
                    return;

                bool allRelevantTerminal = true;
                bool hasRequiredUnverified = false;
                ExecutionTerminalOutcome failure =
                    ExecutionTerminalOutcome.None;

                for (int i = 0; i < plan.Steps.Count; i++)
                {
                    ReactionExecutionStep definition = plan.Steps[i];
                    ExecutionCoordinatorStepState step = Step(
                        definition.StepId
                    );

                    if (definition.Role == ExecutionStepRole.Fallback &&
                        step.Lifecycle == ExecutionCoordinatorStepLifecycle
                            .BlockedByDependency)
                    {
                        continue;
                    }

                    if (step.Lifecycle !=
                        ExecutionCoordinatorStepLifecycle.Terminal)
                    {
                        allRelevantTerminal = false;
                        continue;
                    }

                    if (definition.Requiredness ==
                        ExecutionRequiredness.Optional)
                    {
                        continue;
                    }

                    if (step.TerminalOutcome ==
                        ExecutionTerminalOutcome.SucceededUnverified)
                    {
                        hasRequiredUnverified = true;
                    }
                    else if (IsFailure(step.TerminalOutcome))
                    {
                        failure = StrongerOutcome(
                            failure,
                            step.TerminalOutcome
                        );
                    }
                }

                if (!allRelevantTerminal)
                    return;

                planLifecycle = ExecutionCoordinatorPlanLifecycle.Terminal;
                finalOutcome = failure != ExecutionTerminalOutcome.None
                    ? failure
                    : hasRequiredUnverified
                        ? ExecutionTerminalOutcome.SucceededUnverified
                        : ExecutionTerminalOutcome.Succeeded;
                AddIntent(
                    ExecutionCoordinatorEffectIntentType
                        .PublishTerminalResult,
                    null,
                    string.Empty,
                    null,
                    null
                );
            }

            private static ExecutionTerminalOutcome StrongerOutcome(
                ExecutionTerminalOutcome current,
                ExecutionTerminalOutcome candidate)
            {
                return OutcomeRank(candidate) > OutcomeRank(current)
                    ? candidate
                    : current;
            }

            private static int OutcomeRank(
                ExecutionTerminalOutcome outcome)
            {
                switch (outcome)
                {
                    case ExecutionTerminalOutcome.SafetyPreempted:
                        return 700;
                    case ExecutionTerminalOutcome.Interrupted:
                        return 600;
                    case ExecutionTerminalOutcome.Cancelled:
                        return 500;
                    case ExecutionTerminalOutcome.TimedOut:
                        return 450;
                    case ExecutionTerminalOutcome.Failed:
                    case ExecutionTerminalOutcome.Rejected:
                    case ExecutionTerminalOutcome.ContinueImpossible:
                        return 400;
                    default:
                        return 0;
                }
            }

            private static bool CompletionEvidenceMatches(
                ExecutionCompletionPolicy policy,
                ExecutionDomainStage stage)
            {
                if (policy == null || stage == null)
                    return false;
                if (policy.RequiredDomainStage == null)
                    return policy.Kind != ExecutionCompletionKind.None;
                return Same(
                    policy.RequiredDomainStage.StageId,
                    stage.StageId
                );
            }

            private void AddIntent(
                ExecutionCoordinatorEffectIntentType type,
                ReactionExecutionStep definition,
                string controlRequestId,
                IEnumerable<ExecutionResourceRequirement> resources,
                ExecutionTimeoutPolicy timeoutPolicy,
                string attemptId = "",
                string requestId = "",
                ExecutionResourceLeaseIntentPayload resourceLease = null)
            {
                if (intentIndex >= context.NextIntentIds.Count ||
                    string.IsNullOrWhiteSpace(
                        context.NextIntentIds[intentIndex]))
                {
                    contextFailure = true;
                    AddDiagnostic(
                        ExecutionCoordinatorDiagnosticCodes.MissingContextId
                    );
                    return;
                }

                string intentId = context.NextIntentIds[intentIndex++];
                intents.Add(new ExecutionCoordinatorEffectIntent(
                    intentId,
                    type,
                    plan.PlanId,
                    definition != null ? definition.StepId : string.Empty,
                    attemptId,
                    requestId,
                    controlRequestId,
                    context.NowUtc,
                    resources,
                    timeoutPolicy,
                    ReducerVersion,
                    resourceLease
                ));
            }

            private void AddResourceIntent(
                ExecutionCoordinatorEffectIntentType type,
                ReactionExecutionStep definition,
                ExecutionResourceRequirement requirement,
                string leaseId)
            {
                AddIntent(
                    type,
                    definition,
                    string.Empty,
                    new[] { requirement },
                    null,
                    string.Empty,
                    string.Empty,
                    new ExecutionResourceLeaseIntentPayload(
                        leaseId,
                        requirement.ResourceId,
                        requirement.ResourceId,
                        requirement.AccessMode
                    )
                );
            }

            private bool AllRequiredResourcesAcquired(
                ReactionExecutionStep definition)
            {
                for (int i = 0; i < definition.RequiredResources.Count; i++)
                {
                    ExecutionResourceRequirement requirement =
                        definition.RequiredResources[i];
                    if (requirement.Requiredness !=
                        ExecutionRequiredness.Required)
                    {
                        continue;
                    }

                    bool acquired = false;
                    for (int j = 0; j < leases.Count; j++)
                    {
                        if (Same(
                                leases[j].StepId,
                                definition.StepId) &&
                            Same(
                                leases[j].ResourceId,
                                requirement.ResourceId) &&
                            leases[j].Status ==
                                ExecutionResourceLeaseStatus.Acquired)
                        {
                            acquired = true;
                            break;
                        }
                    }
                    if (!acquired)
                        return false;
                }
                return true;
            }

            private bool IsResourceAcquired(
                string stepId,
                string resourceId)
            {
                for (int i = 0; i < leases.Count; i++)
                {
                    if (Same(leases[i].StepId, stepId) &&
                        Same(leases[i].ResourceId, resourceId) &&
                        leases[i].Status ==
                            ExecutionResourceLeaseStatus.Acquired)
                    {
                        return true;
                    }
                }
                return false;
            }

            private void RequestLeaseReleases(
                ReactionExecutionStep definition,
                bool force)
            {
                for (int i = 0; i < leases.Count; i++)
                {
                    ResourceLeaseReference lease = leases[i];
                    if (!Same(lease.StepId, definition.StepId) ||
                        lease.Status !=
                            ExecutionResourceLeaseStatus.Acquired)
                    {
                        continue;
                    }

                    ExecutionResourceRequirement requirement = FindResource(
                        definition,
                        lease.ResourceId
                    );
                    if (requirement == null)
                        continue;
                    AddResourceIntent(
                        ExecutionCoordinatorEffectIntentType
                            .ReleaseResourceLease,
                        definition,
                        requirement,
                        lease.LeaseId
                    );
                    leases[i] = lease.WithStatus(
                        force
                            ? ExecutionResourceLeaseStatus
                                .ForceReleaseRequested
                            : ExecutionResourceLeaseStatus.ReleaseRequested
                    );
                }
            }

            private void UpdateLeaseStatus(
                string leaseId,
                string resourceId,
                ExecutionResourceLeaseStatus status)
            {
                for (int i = 0; i < leases.Count; i++)
                {
                    if (Same(leases[i].LeaseId, leaseId) &&
                        Same(leases[i].ResourceId, resourceId))
                    {
                        leases[i] = leases[i].WithStatus(status);
                        return;
                    }
                }
            }

            private ExecutionCoordinatorStepState Step(string stepId)
            {
                for (int i = 0; i < steps.Count; i++)
                {
                    if (Same(steps[i].StepId, stepId))
                        return steps[i];
                }

                return null;
            }

            private void Replace(
                ExecutionCoordinatorStepState oldValue,
                ExecutionCoordinatorStepState newValue)
            {
                for (int i = 0; i < steps.Count; i++)
                {
                    if (object.ReferenceEquals(steps[i], oldValue) ||
                        Same(steps[i].StepId, oldValue.StepId))
                    {
                        steps[i] = newValue;
                        return;
                    }
                }
            }

            private static ExecutionCoordinatorStepState CopyStep(
                ExecutionCoordinatorStepState source,
                ExecutionCoordinatorStepLifecycle? lifecycle = null,
                ExecutionDomainStage domainStage = null,
                bool? completionPolicySatisfied = null,
                bool? dependencySatisfied = null,
                bool? executorAccepted = null,
                string currentAttemptId = null,
                IEnumerable<string> attemptIds = null,
                IEnumerable<string> activeLeaseIds = null,
                DateTime? startedAtUtc = null,
                DateTime? completedAtUtc = null,
                ExecutionTerminalOutcome? terminalOutcome = null,
                ExecutionTerminalOutcome? pendingTerminalOutcome = null,
                IEnumerable<string> failureCodes = null,
                string resourceWaitCycleId = null,
                DateTime? resourceWaitStartedAtUtc = null,
                DateTime? resourceWaitDeadlineUtc = null,
                bool clearResourceWait = false)
            {
                return new ExecutionCoordinatorStepState(
                    source.StepId,
                    lifecycle ?? source.Lifecycle,
                    domainStage ?? source.DomainStage,
                    completionPolicySatisfied ??
                        source.CompletionPolicySatisfied,
                    dependencySatisfied ?? source.DependencySatisfied,
                    executorAccepted ?? source.ExecutorAccepted,
                    currentAttemptId ?? source.CurrentAttemptId,
                    attemptIds ?? source.AttemptIds,
                    activeLeaseIds ?? source.ActiveLeaseIds,
                    startedAtUtc ?? source.StartedAtUtc,
                    completedAtUtc ?? source.CompletedAtUtc,
                    terminalOutcome ?? source.TerminalOutcome,
                    pendingTerminalOutcome ??
                        source.PendingTerminalOutcome,
                    failureCodes ?? source.FailureCodes,
                    clearResourceWait
                        ? string.Empty
                        : resourceWaitCycleId ??
                            source.ResourceWaitCycleId,
                    clearResourceWait
                        ? (DateTime?)null
                        : resourceWaitStartedAtUtc ??
                            source.ResourceWaitStartedAtUtc,
                    clearResourceWait
                        ? (DateTime?)null
                        : resourceWaitDeadlineUtc ??
                            source.ResourceWaitDeadlineUtc
                );
            }

            private void UpdateAttempt(
                string attemptId,
                ExecutionAttemptLifecycle lifecycle,
                DateTime? endedAtUtc,
                ExecutionTerminalOutcome? outcome,
                IEnumerable<string> failureCodes,
                ExecutionDomainStage stage = null)
            {
                for (int i = 0; i < attempts.Count; i++)
                {
                    ExecutionCoordinatorAttemptState source = attempts[i];
                    if (!Same(source.ExecutionAttemptId, attemptId))
                        continue;
                    attempts[i] = new ExecutionCoordinatorAttemptState(
                        source.ExecutionAttemptId,
                        source.StepId,
                        source.RequestId,
                        source.AttemptNumber,
                        lifecycle,
                        stage ?? source.DomainStage,
                        lifecycle == ExecutionAttemptLifecycle.Running &&
                            !source.StartedAtUtc.HasValue
                            ? context.NowUtc
                            : source.StartedAtUtc,
                        endedAtUtc ?? source.EndedAtUtc,
                        outcome ?? source.TerminalOutcome,
                        failureCodes ?? source.FailureCodes
                    );
                    return;
                }
            }

            private void AddDiagnostic(string diagnostic)
            {
                if (!string.IsNullOrEmpty(diagnostic) &&
                    !Contains(diagnostics, diagnostic))
                {
                    diagnostics.Add(diagnostic);
                }
            }

            private ExecutionCoordinatorControlState FindControl(
                string controlRequestId)
            {
                for (int i = controls.Count - 1; i >= 0; i--)
                {
                    if (Same(
                            controls[i].ControlRequestId,
                            controlRequestId))
                    {
                        return controls[i];
                    }
                }
                return null;
            }

            private void UpdateControl(
                string controlRequestId,
                bool dispatched,
                bool confirmed)
            {
                for (int i = controls.Count - 1; i >= 0; i--)
                {
                    ExecutionCoordinatorControlState source = controls[i];
                    if (!Same(source.ControlRequestId, controlRequestId))
                        continue;
                    controls[i] = new ExecutionCoordinatorControlState(
                        source.ControlRequestId,
                        source.ControlType,
                        source.PriorityRank,
                        dispatched,
                        confirmed
                    );
                    return;
                }
            }

            private static ExecutionControlType ControlType(
                ExecutionCoordinatorEventType type)
            {
                switch (type)
                {
                    case ExecutionCoordinatorEventType.InterruptRequested:
                        return ExecutionControlType.Interrupt;
                    case ExecutionCoordinatorEventType.SafetyStopRequested:
                        return ExecutionControlType.SafetyStop;
                    case ExecutionCoordinatorEventType
                        .EmergencyPreemptRequested:
                        return ExecutionControlType.EmergencyPreempt;
                    default:
                        return ExecutionControlType.Cancel;
                }
            }

            private static ExecutionTerminalOutcome ControlOutcome(
                ExecutionControlType type)
            {
                switch (type)
                {
                    case ExecutionControlType.EmergencyPreempt:
                    case ExecutionControlType.SafetyStop:
                        return ExecutionTerminalOutcome.SafetyPreempted;
                    case ExecutionControlType.Interrupt:
                    case ExecutionControlType.Stop:
                        return ExecutionTerminalOutcome.Interrupted;
                    default:
                        return ExecutionTerminalOutcome.Cancelled;
                }
            }

            private static string ControlFailureCode(
                ExecutionControlType type)
            {
                switch (type)
                {
                    case ExecutionControlType.EmergencyPreempt:
                    case ExecutionControlType.SafetyStop:
                        return ExecutionFailureCodes.SafetyPreempted;
                    case ExecutionControlType.Interrupt:
                    case ExecutionControlType.Stop:
                        return ExecutionFailureCodes.InterruptedByPriority;
                    default:
                        return ExecutionFailureCodes.Cancellation;
                }
            }

            private static bool IsSuccess(
                ExecutionTerminalOutcome outcome)
            {
                return outcome == ExecutionTerminalOutcome.Succeeded ||
                    outcome == ExecutionTerminalOutcome
                        .SucceededUnverified;
            }

            private static bool IsFailure(
                ExecutionTerminalOutcome outcome)
            {
                return outcome != ExecutionTerminalOutcome.None &&
                    !IsSuccess(outcome);
            }
        }
    }
}
