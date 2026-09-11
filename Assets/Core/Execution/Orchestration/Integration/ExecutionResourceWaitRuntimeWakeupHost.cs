// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

/* Project Salieri AI - Apache-2.0 */
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using SalieriAI.Core.Execution.Orchestration.Authority;
using SalieriAI.Core.Execution.Orchestration.Contracts;
using SalieriAI.Core.Execution.Orchestration.Reduction;
using SalieriAI.Core.Execution.Orchestration.Resources;
using SalieriAI.Core.Execution.Orchestration.Runtime;
using SalieriAI.Core.Execution.Orchestration.Validation;
using SalieriAI.Core.State;

namespace SalieriAI.Core.Execution.Orchestration.Integration
{
    /// <summary>
    /// Production boundary that turns a completed InteractionState profile
    /// change into one resource-wait re-evaluation cycle.
    ///
    /// The host owns only durable orchestration state and event subscription.
    /// Availability remains read-only in ExecutionResourceRuntimeAdapter;
    /// logical ownership remains in LogicalResourceLeaseService through the
    /// existing coordinator loop. It creates no timer and consumes no retry.
    /// </summary>
    public sealed class ExecutionResourceWaitRuntimeWakeupHost : IDisposable
    {
        public const string EventSource =
            "ExecutionResourceWaitRuntimeWakeupHost";
        public const string HostVersion =
            "execution-resource-wait-runtime-wakeup-host-3d4b.1";

        private const int ReducerIntentIdCapacity = 64;

        private readonly InteractionStateController stateController;
        private readonly ExecutionResourceRuntimeAdapter resourceAdapter;
        private readonly Func<DateTime> utcNow;
        private readonly Func<string> eventIdFactory;
        private readonly Func<string> correlationIdFactory;
        private readonly ExecutionLogicalResourceLeaseCoordinatorLoop loop =
            new ExecutionLogicalResourceLeaseCoordinatorLoop();
        private readonly List<ExecutionCoordinatorEffectIntent>
            pendingAcquireIntents =
                new List<ExecutionCoordinatorEffectIntent>();

        private bool started;
        private bool processing;
        private AutonomousClock clockPulseSource;
        private ValidatedExecutionPlan plan;
        private ExecutionCoordinatorReductionResult currentReduction;
        private LogicalResourceLeaseState currentLeaseState;
        private IReadOnlyList<ExecutionLogicalLeaseLoopResult>
            lastWakeupResults = EmptyResults();

        public bool IsStarted => started;
        public ValidatedExecutionPlan Plan => plan;
        public ExecutionCoordinatorReductionResult CurrentReduction =>
            currentReduction;
        public LogicalResourceLeaseState CurrentLeaseState =>
            currentLeaseState;
        public int PendingAcquireCount => pendingAcquireIntents.Count;
        public int WakeupCount { get; private set; }
        public int ClockPulseCount { get; private set; }
        public int TimeoutEventCount { get; private set; }
        public int PlanStartEventCount { get; private set; }
        public string LastWakeupDiagnostic { get; private set; } =
            string.Empty;
        public IReadOnlyList<ExecutionLogicalLeaseLoopResult>
            LastWakeupResults => lastWakeupResults;
        public ExecutionCoordinatorEvent LastTimeoutEvent { get; private set; }
        public ExecutionCoordinatorReductionResult LastTimeoutReduction
        {
            get;
            private set;
        }
        public ExecutionLogicalLeaseLoopResult LastTimeoutReleaseResult
        {
            get;
            private set;
        }
        public ExecutionCoordinatorEvent LastPlanStartEvent { get; private set; }
        public ExecutionCoordinatorReductionResult LastPlanStartReduction
        {
            get;
            private set;
        }

        public ExecutionResourceWaitRuntimeWakeupHost(
            InteractionStateController stateController,
            ExecutionResourceManager resourceManager)
            : this(
                stateController,
                new ExecutionResourceRuntimeAdapter(resourceManager),
                () => DateTime.UtcNow,
                () => Guid.NewGuid().ToString("N"),
                () => Guid.NewGuid().ToString("N"))
        {
        }

        /// <summary>
        /// Explicit-value constructor for deterministic verification and for
        /// an outer runtime that owns its clock/identity source. The adapter
        /// is still the sole availability-query boundary.
        /// </summary>
        public ExecutionResourceWaitRuntimeWakeupHost(
            InteractionStateController stateController,
            ExecutionResourceRuntimeAdapter resourceAdapter,
            Func<DateTime> utcNow,
            Func<string> eventIdFactory,
            Func<string> correlationIdFactory)
        {
            this.stateController = stateController;
            this.resourceAdapter = resourceAdapter;
            this.utcNow = utcNow;
            this.eventIdFactory = eventIdFactory;
            this.correlationIdFactory = correlationIdFactory;
        }

        /// <summary>
        /// Starts durable ownership after an external Production builder and
        /// validator have produced a validated plan and initial runtime state.
        /// Those bootstrap responsibilities intentionally remain external.
        /// </summary>
        public bool Start(
            ValidatedExecutionPlan validatedPlan,
            ExecutionCoordinatorReductionResult initialReduction,
            LogicalResourceLeaseState initialLeaseState)
        {
            Stop();
            LastWakeupDiagnostic = string.Empty;

            if (!ValidStart(
                    validatedPlan, initialReduction, initialLeaseState))
            {
                LastWakeupDiagnostic = "RUNTIME_WAKEUP_START_INVALID";
                return false;
            }

            plan = validatedPlan;
            currentReduction = initialReduction;
            currentLeaseState = initialLeaseState;
            pendingAcquireIntents.Clear();
            CaptureAcquireIntents(initialReduction.EffectIntents);
            lastWakeupResults = EmptyResults();
            WakeupCount = 0;
            ClockPulseCount = 0;
            TimeoutEventCount = 0;
            PlanStartEventCount = 0;
            LastTimeoutEvent = null;
            LastTimeoutReduction = null;
            LastTimeoutReleaseResult = null;
            LastPlanStartEvent = null;
            LastPlanStartReduction = null;
            started = true;
            stateController.OnStateChanged += OnStateChanged;
            if (clockPulseSource != null)
                clockPulseSource.OnInputPollPulseUtc += OnClockPulseUtc;
            return true;
        }

        /// <summary>
        /// Binds the existing AutonomousClock input-poll cadence. The clock
        /// supplies only UTC pulses; it never sees plans, reductions, leases,
        /// or resource-wait deadlines.
        /// </summary>
        public void BindClockPulseSource(AutonomousClock source)
        {
            if (started && clockPulseSource != null)
                clockPulseSource.OnInputPollPulseUtc -= OnClockPulseUtc;
            clockPulseSource = source;
            if (started && clockPulseSource != null)
                clockPulseSource.OnInputPollPulseUtc += OnClockPulseUtc;
        }

        public void Stop()
        {
            if (started && stateController != null)
                stateController.OnStateChanged -= OnStateChanged;
            if (started && clockPulseSource != null)
                clockPulseSource.OnInputPollPulseUtc -= OnClockPulseUtc;
            started = false;
            processing = false;
        }

        public void Dispose()
        {
            Stop();
        }

        private void OnStateChanged(
            InteractionState previous,
            InteractionState next)
        {
            ProcessResourceProfileChange(previous, next);
        }

        private void OnClockPulseUtc(DateTime evaluatedAtUtc)
        {
            ProcessClockPulseUtc(evaluatedAtUtc);
        }

        /// <summary>
        /// Explicit Production plan-start boundary. It supplies UTC and IDs
        /// from the outer runtime and only applies the pure reducer result.
        /// It does not execute, infer, or dispatch an action effect.
        /// </summary>
        public bool ProcessPlanStartUtc(DateTime occurredAtUtc)
        {
            if (!started || processing || !ValidRuntimeState())
                return false;

            DateTime now = NormalizeUtc(occurredAtUtc);
            if (now == default(DateTime) ||
                currentReduction.State.PlanLifecycle !=
                    ExecutionCoordinatorPlanLifecycle.Accepted)
            {
                return false;
            }

            processing = true;
            LastPlanStartEvent = null;
            LastPlanStartReduction = null;
            try
            {
                var coordinatorEvent = new ExecutionCoordinatorEvent(
                    RequiredId(eventIdFactory()),
                    ExecutionCoordinatorEventType.PlanStartRequested,
                    currentReduction.State.PlanId,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    now,
                    EventSource,
                    currentReduction.State.Generation,
                    null,
                    ExecutionTerminalOutcome.None,
                    new string[0],
                    string.Empty);
                ExecutionCoordinatorReductionResult reduction =
                    ExecutionCoordinatorReducer.Reduce(
                        plan,
                        currentReduction.State,
                        coordinatorEvent,
                        ReducerContext(
                            now,
                            currentReduction.State.Generation));

                LastPlanStartEvent = coordinatorEvent;
                LastPlanStartReduction = reduction;
                if (reduction == null ||
                    reduction.EventDisposition !=
                        ExecutionCoordinatorEventDisposition.Applied)
                {
                    LastWakeupDiagnostic =
                        "RUNTIME_PLAN_START_NOT_APPLIED";
                    return false;
                }

                currentReduction = reduction;
                CaptureAcquireIntents(reduction.EffectIntents);
                PlanStartEventCount++;
                return true;
            }
            catch (Exception exception)
            {
                LastWakeupDiagnostic =
                    "RUNTIME_PLAN_START_EXCEPTION:" +
                    exception.GetType().Name;
                return false;
            }
            finally
            {
                processing = false;
            }
        }

        /// <summary>
        /// Evaluates only active resource-wait deadlines. A due timeout is
        /// reduced first; only reducer-produced ReleaseResourceLease intents
        /// are then sent through the existing logical lease loop.
        /// </summary>
        public bool ProcessClockPulseUtc(DateTime evaluatedAtUtc)
        {
            if (!started || processing)
                return false;

            DateTime now = NormalizeUtc(evaluatedAtUtc);
            if (now == default(DateTime))
                return false;

            processing = true;
            ClockPulseCount++;
            LastTimeoutEvent = null;
            LastTimeoutReduction = null;
            LastTimeoutReleaseResult = null;
            try
            {
                if (!ValidRuntimeState())
                {
                    LastWakeupDiagnostic =
                        "RUNTIME_TIMEOUT_STATE_INVALID";
                    return false;
                }

                bool fired = false;
                ExecutionCoordinatorStepState[] steps =
                    CopySteps(currentReduction.State.StepStates);
                for (int i = 0; i < steps.Length; i++)
                {
                    ExecutionCoordinatorStepState step =
                        currentReduction.State.FindStep(steps[i].StepId);
                    if (!IsTimeoutDue(step, now))
                        continue;

                    ExecutionCoordinatorEvent timeoutEvent =
                        CreateTimeoutEvent(step, now);
                    ExecutionCoordinatorReductionResult timeoutReduction =
                        ExecutionCoordinatorReducer.Reduce(
                            plan,
                            currentReduction.State,
                            timeoutEvent,
                            ReducerContext(
                                now,
                                currentReduction.State.Generation));
                    if (timeoutReduction == null ||
                        timeoutReduction.EventDisposition !=
                            ExecutionCoordinatorEventDisposition.Applied)
                    {
                        continue;
                    }

                    fired = true;
                    TimeoutEventCount++;
                    LastTimeoutEvent = timeoutEvent;
                    LastTimeoutReduction = timeoutReduction;
                    currentReduction = timeoutReduction;
                    RemovePendingForStep(step.StepId);

                    ExecutionLogicalLeaseLoopResult release =
                        PumpTimeoutReleases(timeoutReduction, now);
                    if (release != null)
                    {
                        LastTimeoutReleaseResult = release;
                        currentReduction = release.FinalReduction;
                        currentLeaseState = release.FinalLeaseState;
                    }
                }
                return fired;
            }
            catch (Exception exception)
            {
                LastWakeupDiagnostic =
                    "RUNTIME_TIMEOUT_EXCEPTION:" +
                    exception.GetType().Name;
                return false;
            }
            finally
            {
                processing = false;
            }
        }

        private bool IsTimeoutDue(
            ExecutionCoordinatorStepState step,
            DateTime evaluatedAtUtc)
        {
            if (step == null || step.Lifecycle !=
                    ExecutionCoordinatorStepLifecycle.WaitingForResource ||
                string.IsNullOrWhiteSpace(step.ResourceWaitCycleId) ||
                !step.ResourceWaitStartedAtUtc.HasValue ||
                !step.ResourceWaitDeadlineUtc.HasValue ||
                evaluatedAtUtc < step.ResourceWaitDeadlineUtc.Value)
            {
                return false;
            }

            ReactionExecutionStep definition =
                FindStepDefinition(step.StepId);
            return definition != null &&
                definition.ResourceWaitTimeoutPolicy != null &&
                definition.ResourceWaitTimeoutPolicy.TimeoutEnabled;
        }

        private ExecutionCoordinatorEvent CreateTimeoutEvent(
            ExecutionCoordinatorStepState step,
            DateTime evaluatedAtUtc)
        {
            DateTime deadline = step.ResourceWaitDeadlineUtc.Value;
            return new ExecutionCoordinatorEvent(
                "resource-wait-timeout-" +
                    RequiredId(eventIdFactory()),
                ExecutionCoordinatorEventType.ResourceWaitTimedOut,
                currentReduction.State.PlanId,
                step.StepId,
                string.Empty,
                string.Empty,
                evaluatedAtUtc,
                EventSource,
                currentReduction.State.Generation,
                null,
                ExecutionTerminalOutcome.None,
                new string[0],
                HostVersion,
                null,
                null,
                new ExecutionResourceWaitTimeoutEventPayload(
                    step.ResourceWaitCycleId,
                    evaluatedAtUtc,
                    deadline));
        }

        private ExecutionLogicalLeaseLoopResult PumpTimeoutReleases(
            ExecutionCoordinatorReductionResult timeoutReduction,
            DateTime evaluatedAtUtc)
        {
            var releases = new List<ExecutionCoordinatorEffectIntent>();
            for (int i = 0;
                i < timeoutReduction.EffectIntents.Count;
                i++)
            {
                ExecutionCoordinatorEffectIntent intent =
                    timeoutReduction.EffectIntents[i];
                if (intent != null && intent.ResourceLease != null &&
                    intent.IntentType ==
                        ExecutionCoordinatorEffectIntentType
                            .ReleaseResourceLease)
                {
                    releases.Add(intent);
                }
            }
            if (releases.Count == 0)
                return null;

            var cycles = new List<ExecutionLogicalLeaseLoopCycleInput>();
            for (int i = 0; i < releases.Count; i++)
            {
                cycles.Add(CycleAt(
                    evaluatedAtUtc,
                    timeoutReduction.State.Generation,
                    "RESOURCE_WAIT_TIMEOUT_CLEANUP",
                    releases[i].ResourceLease.LeaseId));
            }

            return loop.Pump(
                plan,
                new ExecutionCoordinatorReductionResult(
                    timeoutReduction.State,
                    releases,
                    timeoutReduction.Diagnostics,
                    timeoutReduction.EventDisposition),
                currentLeaseState,
                ExecutionLiveAuthorityPolicy.CreateDefault(),
                ExecutionLiveAuthorityPolicy
                    .CreateCurrentCapabilityFixture(),
                cycles,
                releases.Count);
        }

        private ReactionExecutionStep FindStepDefinition(string stepId)
        {
            if (plan == null || plan.SourcePlan == null)
                return null;
            for (int i = 0; i < plan.SourcePlan.Steps.Count; i++)
            {
                ReactionExecutionStep step = plan.SourcePlan.Steps[i];
                if (step != null && Same(step.StepId, stepId))
                    return step;
            }
            return null;
        }

        private static ExecutionCoordinatorStepState[] CopySteps(
            IReadOnlyList<ExecutionCoordinatorStepState> source)
        {
            var result = new ExecutionCoordinatorStepState[source.Count];
            for (int i = 0; i < source.Count; i++)
                result[i] = source[i];
            return result;
        }

        /// <summary>
        /// Public for an explicit Production wake-up call and deterministic
        /// tests. Normal use is the OnStateChanged subscription installed by
        /// Start().
        /// </summary>
        public bool ProcessResourceProfileChange(
            InteractionState previous,
            InteractionState next)
        {
            if (!started || processing)
                return false;

            processing = true;
            WakeupCount++;
            LastWakeupDiagnostic = string.Empty;
            var results = new List<ExecutionLogicalLeaseLoopResult>();
            try
            {
                if (!ValidRuntimeState())
                {
                    LastWakeupDiagnostic =
                        "RUNTIME_WAKEUP_STATE_INVALID";
                    return false;
                }

                ExecutionCoordinatorEffectIntent[] wakeupSnapshot =
                    pendingAcquireIntents.ToArray();
                for (int i = 0; i < wakeupSnapshot.Length; i++)
                {
                    ExecutionCoordinatorEffectIntent intent =
                        wakeupSnapshot[i];
                    if (!IsStillWaiting(intent))
                    {
                        RemovePending(intent);
                        continue;
                    }
                    if (IsAlreadyAcquired(intent))
                    {
                        RemovePending(intent);
                        continue;
                    }

                    ExecutionLogicalLeaseLoopResult availability =
                        EvaluateAvailability(intent, previous, next);
                    results.Add(availability);
                    if (availability == null)
                    {
                        LastWakeupDiagnostic =
                            "RUNTIME_WAKEUP_AVAILABILITY_NULL";
                        continue;
                    }

                    if (availability.StopReason ==
                        ExecutionLogicalLeaseLoopStopReason
                            .DuplicateEventInput)
                    {
                        LastWakeupDiagnostic =
                            "RUNTIME_WAKEUP_DUPLICATE_EVENT";
                        continue;
                    }

                    ExecutionLogicalLeaseLoopTrace availabilityTrace =
                        FirstAvailabilityTrace(availability);
                    if (availabilityTrace == null ||
                        availabilityTrace.GeneratedEvent == null ||
                        availabilityTrace.ReappliedReduction == null ||
                        availabilityTrace.ReappliedReduction
                            .EventDisposition !=
                                ExecutionCoordinatorEventDisposition.Applied)
                    {
                        LastWakeupDiagnostic =
                            "RUNTIME_WAKEUP_AVAILABILITY_NOT_APPLIED";
                        continue;
                    }

                    currentReduction = availability.FinalReduction;
                    currentLeaseState = availability.FinalLeaseState;

                    if (availabilityTrace.AvailabilityCorrelation == null ||
                        availabilityTrace.AvailabilityCorrelation
                            .Disposition !=
                                ExecutionResourceAvailabilityLeaseDisposition
                                    .Available)
                    {
                        continue;
                    }

                    ExecutionCoordinatorEffectIntent acquire =
                        FindAcquireIntent(
                            availability.FinalReduction.EffectIntents,
                            intent.StepId,
                            intent.ResourceLease.ResourceId);
                    if (acquire == null)
                    {
                        if (IsAlreadyAcquired(intent))
                            RemovePending(intent);
                        continue;
                    }

                    ExecutionLogicalLeaseLoopResult dispatch =
                        DispatchAvailableAcquire(
                            availability.FinalReduction,
                            acquire,
                            previous,
                            next);
                    results.Add(dispatch);
                    if (dispatch == null)
                    {
                        LastWakeupDiagnostic =
                            "RUNTIME_WAKEUP_DISPATCH_NULL";
                        continue;
                    }

                    currentReduction = dispatch.FinalReduction;
                    currentLeaseState = dispatch.FinalLeaseState;
                    RemovePending(intent);
                    CaptureAcquireIntents(
                        currentReduction != null
                            ? currentReduction.EffectIntents
                            : null);
                }

                return true;
            }
            catch (Exception exception)
            {
                LastWakeupDiagnostic =
                    "RUNTIME_WAKEUP_EXCEPTION:" +
                    exception.GetType().Name;
                return false;
            }
            finally
            {
                lastWakeupResults =
                    new ReadOnlyCollection<ExecutionLogicalLeaseLoopResult>(
                        results);
                processing = false;
            }
        }

        private ExecutionLogicalLeaseLoopResult EvaluateAvailability(
            ExecutionCoordinatorEffectIntent intent,
            InteractionState previous,
            InteractionState next)
        {
            ExecutionCoordinatorReductionResult isolated =
                Isolate(intent);
            ExecutionLogicalLeaseLoopCycleInput cycle = Cycle(
                currentReduction.State.Generation,
                "PROFILE_CHANGED:" + previous + "->" + next);
            return loop.PumpWithAvailability(
                plan,
                isolated,
                currentLeaseState,
                ExecutionLiveAuthorityPolicy.CreateDefault(),
                ExecutionLiveAuthorityPolicy
                    .CreateCurrentCapabilityFixture(),
                new[] { cycle },
                1,
                resourceAdapter);
        }

        private ExecutionLogicalLeaseLoopResult DispatchAvailableAcquire(
            ExecutionCoordinatorReductionResult availableReduction,
            ExecutionCoordinatorEffectIntent intent,
            InteractionState previous,
            InteractionState next)
        {
            ExecutionLogicalLeaseLoopCycleInput cycle = Cycle(
                availableReduction.State.Generation,
                "PROFILE_AVAILABLE:" + previous + "->" + next);
            return loop.Pump(
                plan,
                new ExecutionCoordinatorReductionResult(
                    availableReduction.State,
                    new[] { intent },
                    availableReduction.Diagnostics,
                    availableReduction.EventDisposition),
                currentLeaseState,
                ExecutionLiveAuthorityPolicy.CreateDefault(),
                ExecutionLiveAuthorityPolicy
                    .CreateCurrentCapabilityFixture(),
                new[] { cycle },
                1);
        }

        private ExecutionLogicalLeaseLoopCycleInput Cycle(
            int generation,
            string diagnostic)
        {
            return CycleAt(
                NormalizeUtc(utcNow()),
                generation,
                diagnostic,
                string.Empty);
        }

        private ExecutionLogicalLeaseLoopCycleInput CycleAt(
            DateTime now,
            int generation,
            string diagnostic,
            string existingLeaseId)
        {
            string eventId = RequiredId(eventIdFactory());
            string requestId = RequiredId(correlationIdFactory());
            string leaseId = string.IsNullOrWhiteSpace(existingLeaseId)
                ? RequiredId(correlationIdFactory())
                : existingLeaseId;
            ExecutionCoordinatorReducerContext reducerContext =
                ReducerContext(now, generation);

            return new ExecutionLogicalLeaseLoopCycleInput(
                eventId,
                EventSource,
                requestId,
                leaseId,
                new ExecutionAuthorityEvaluationContext(
                    false,
                    false,
                    now,
                    diagnostic),
                reducerContext,
                ResourceShadowAvailabilityStatus.Available,
                false,
                true,
                string.Empty);
        }

        private ExecutionCoordinatorReducerContext ReducerContext(
            DateTime now,
            int generation)
        {
            var intentIds = new string[ReducerIntentIdCapacity];
            for (int i = 0; i < intentIds.Length; i++)
                intentIds[i] = RequiredId(correlationIdFactory());
            return new ExecutionCoordinatorReducerContext(
                now,
                RequiredId(correlationIdFactory()),
                RequiredId(correlationIdFactory()),
                intentIds,
                generation);
        }

        private ExecutionCoordinatorReductionResult Isolate(
            ExecutionCoordinatorEffectIntent intent)
        {
            return new ExecutionCoordinatorReductionResult(
                currentReduction.State,
                new[] { intent },
                currentReduction.Diagnostics,
                currentReduction.EventDisposition);
        }

        private void CaptureAcquireIntents(
            IReadOnlyList<ExecutionCoordinatorEffectIntent> intents)
        {
            if (intents == null)
                return;
            for (int i = 0; i < intents.Count; i++)
            {
                ExecutionCoordinatorEffectIntent intent = intents[i];
                if (intent == null ||
                    intent.IntentType !=
                        ExecutionCoordinatorEffectIntentType
                            .AcquireResourceLease ||
                    intent.ResourceLease == null ||
                    ContainsPending(intent.StepId,
                        intent.ResourceLease.ResourceId))
                {
                    continue;
                }
                pendingAcquireIntents.Add(intent);
            }
        }

        private bool ContainsPending(string stepId, string resourceId)
        {
            for (int i = 0; i < pendingAcquireIntents.Count; i++)
            {
                ExecutionCoordinatorEffectIntent value =
                    pendingAcquireIntents[i];
                if (value != null && value.ResourceLease != null &&
                    Same(value.StepId, stepId) &&
                    Same(value.ResourceLease.ResourceId, resourceId))
                    return true;
            }
            return false;
        }

        private void RemovePending(
            ExecutionCoordinatorEffectIntent expected)
        {
            if (expected == null || expected.ResourceLease == null)
                return;
            for (int i = pendingAcquireIntents.Count - 1; i >= 0; i--)
            {
                ExecutionCoordinatorEffectIntent value =
                    pendingAcquireIntents[i];
                if (value != null && value.ResourceLease != null &&
                    Same(value.StepId, expected.StepId) &&
                    Same(value.ResourceLease.ResourceId,
                        expected.ResourceLease.ResourceId))
                    pendingAcquireIntents.RemoveAt(i);
            }
        }

        private void RemovePendingForStep(string stepId)
        {
            for (int i = pendingAcquireIntents.Count - 1; i >= 0; i--)
            {
                if (pendingAcquireIntents[i] != null &&
                    Same(pendingAcquireIntents[i].StepId, stepId))
                    pendingAcquireIntents.RemoveAt(i);
            }
        }

        private bool IsStillWaiting(
            ExecutionCoordinatorEffectIntent intent)
        {
            if (intent == null || currentReduction == null ||
                currentReduction.State == null)
                return false;
            ExecutionCoordinatorStepState step =
                currentReduction.State.FindStep(intent.StepId);
            return step != null && step.Lifecycle ==
                ExecutionCoordinatorStepLifecycle.WaitingForResource;
        }

        private bool IsAlreadyAcquired(
            ExecutionCoordinatorEffectIntent intent)
        {
            if (intent == null || intent.ResourceLease == null ||
                currentReduction == null || currentReduction.State == null)
                return false;
            for (int i = 0;
                i < currentReduction.State.ResourceLeaseReferences.Count;
                i++)
            {
                ResourceLeaseReference lease =
                    currentReduction.State.ResourceLeaseReferences[i];
                if (lease != null &&
                    lease.Status == ExecutionResourceLeaseStatus.Acquired &&
                    Same(lease.StepId, intent.StepId) &&
                    Same(lease.ResourceId,
                        intent.ResourceLease.ResourceId))
                    return true;
            }
            return false;
        }

        private static ExecutionLogicalLeaseLoopTrace
            FirstAvailabilityTrace(
                ExecutionLogicalLeaseLoopResult result)
        {
            if (result == null)
                return null;
            for (int i = 0; i < result.Traces.Count; i++)
            {
                if (result.Traces[i].AvailabilityCorrelation != null)
                    return result.Traces[i];
            }
            return null;
        }

        private static ExecutionCoordinatorEffectIntent FindAcquireIntent(
            IReadOnlyList<ExecutionCoordinatorEffectIntent> intents,
            string stepId,
            string resourceId)
        {
            if (intents == null)
                return null;
            for (int i = 0; i < intents.Count; i++)
            {
                ExecutionCoordinatorEffectIntent intent = intents[i];
                if (intent != null && intent.ResourceLease != null &&
                    intent.IntentType ==
                        ExecutionCoordinatorEffectIntentType
                            .AcquireResourceLease &&
                    Same(intent.StepId, stepId) &&
                    Same(intent.ResourceLease.ResourceId, resourceId))
                    return intent;
            }
            return null;
        }

        private static bool ValidStart(
            ValidatedExecutionPlan plan,
            ExecutionCoordinatorReductionResult reduction,
            LogicalResourceLeaseState leaseState)
        {
            return plan != null && plan.SourcePlan != null &&
                reduction != null && reduction.State != null &&
                leaseState != null &&
                Same(plan.SourcePlan.PlanId, reduction.State.PlanId) &&
                plan.SourcePlan.PlanGeneration ==
                    reduction.State.Generation &&
                leaseState.Generation == reduction.State.Generation;
        }

        private bool ValidRuntimeState()
        {
            return stateController != null && resourceAdapter != null &&
                utcNow != null && eventIdFactory != null &&
                correlationIdFactory != null &&
                ValidStart(plan, currentReduction, currentLeaseState);
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            if (value == default(DateTime) ||
                value.Kind == DateTimeKind.Utc)
                return value;
            return value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        private static string RequiredId(string value)
        {
            return value ?? string.Empty;
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(left, right, StringComparison.Ordinal);
        }

        private static IReadOnlyList<ExecutionLogicalLeaseLoopResult>
            EmptyResults()
        {
            return new ReadOnlyCollection<ExecutionLogicalLeaseLoopResult>(
                new List<ExecutionLogicalLeaseLoopResult>());
        }
    }
}
