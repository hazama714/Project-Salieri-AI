// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SalieriAI.Core.Skills
{
    public enum SkillPlanExecutionStatus
    {
        Succeeded,
        Failed,
        Cancelled,
        TimedOut,
        Rejected
    }

    public sealed class SkillPlanExecutionResult
    {
        public string RunId { get; }
        public string PlanId { get; }
        public SkillPlanExecutionStatus Status { get; }
        public ISkillPayload FinalPayload { get; }
        public string Message { get; }
        public int ExecutedStepCount { get; }
        public DateTime StartedAt { get; }
        public DateTime FinishedAt { get; }
        public TimeSpan Duration => FinishedAt - StartedAt;

        internal SkillPlanExecutionResult(
            string runId,
            string planId,
            SkillPlanExecutionStatus status,
            ISkillPayload finalPayload,
            string message,
            int executedStepCount,
            DateTime startedAt,
            DateTime finishedAt)
        {
            RunId = runId ?? string.Empty;
            PlanId = planId ?? string.Empty;
            Status = status;
            FinalPayload = finalPayload;
            Message = message ?? string.Empty;
            ExecutedStepCount = executedStepCount;
            StartedAt = startedAt;
            FinishedAt = finishedAt < startedAt ? startedAt : finishedAt;
        }
    }

    /// <summary>
    /// Minimal sequential runner for validated v0.1 Plans.
    /// It does not access Scene objects, BODY, LLM, voice, DB or Unity APIs.
    /// </summary>
    public sealed class SkillPlanRunner
    {
        private readonly SkillImplementationRegistry implementationRegistry;
        private readonly object stateLock = new object();

        private bool isRunning;
        private int activeGeneration;
        private int pendingHandlerTaskCount;
        private CancellationTokenSource activeCancellation;

        public bool IsRunning
        {
            get
            {
                lock (stateLock)
                    return isRunning;
            }
        }

        public SkillPlanRunner(
            SkillImplementationRegistry implementationRegistry)
        {
            this.implementationRegistry = implementationRegistry;
        }

        public void CancelCurrent()
        {
            lock (stateLock)
            {
                if (!isRunning || activeCancellation == null)
                    return;

                activeCancellation.Cancel();
            }
        }

        public async Task<SkillPlanExecutionResult> RunAsync(
            ValidatedSkillPlan validatedPlan,
            ISkillPayload initialPayload,
            CancellationToken cancellationToken = default(CancellationToken),
            IReadOnlyDictionary<string, string> contextValues = null)
        {
            DateTime startedAt = DateTime.UtcNow;

            if (validatedPlan == null)
            {
                return CreateResult(
                    string.Empty,
                    string.Empty,
                    SkillPlanExecutionStatus.Rejected,
                    null,
                    "Validated Plan is null.",
                    0,
                    startedAt
                );
            }

            if (implementationRegistry == null)
            {
                return CreateResult(
                    string.Empty,
                    validatedPlan.PlanId,
                    SkillPlanExecutionStatus.Rejected,
                    null,
                    "Implementation Registry is null.",
                    0,
                    startedAt
                );
            }

            string runId = Guid.NewGuid().ToString("N");
            int generation;
            CancellationTokenSource manualCancellation =
                new CancellationTokenSource();

            lock (stateLock)
            {
                if (isRunning || pendingHandlerTaskCount > 0)
                {
                    manualCancellation.Dispose();

                    return CreateResult(
                        runId,
                        validatedPlan.PlanId,
                        SkillPlanExecutionStatus.Rejected,
                        null,
                        "Runner already has an active or unfinished Handler.",
                        0,
                        startedAt
                    );
                }

                isRunning = true;
                generation = ++activeGeneration;
                activeCancellation = manualCancellation;
            }

            SkillPlanData plan = validatedPlan.Snapshot;
            CancellationTokenSource planTimeoutCancellation =
                new CancellationTokenSource();
            CancellationTokenSource linkedCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    manualCancellation.Token,
                    planTimeoutCancellation.Token
                );

            planTimeoutCancellation.CancelAfter(
                TimeSpan.FromSeconds(plan.maximumExecutionSeconds)
            );

            int executedStepCount = 0;
            ISkillPayload lastSuccessfulPayload = initialPayload;
            Dictionary<string, ISkillPayload> outputByStep =
                new Dictionary<string, ISkillPayload>(StringComparer.Ordinal);
            Dictionary<string, SkillPlanStepData> stepMap =
                BuildStepMap(plan);
            string currentStepId = plan.entryStepId;

            try
            {
                while (true)
                {
                    if (!IsCurrentGeneration(generation))
                    {
                        return CreateResult(
                            runId,
                            plan.planId,
                            SkillPlanExecutionStatus.Cancelled,
                            lastSuccessfulPayload,
                            "Run generation is no longer current.",
                            executedStepCount,
                            startedAt
                        );
                    }

                    if (planTimeoutCancellation.IsCancellationRequested)
                    {
                        return CreateResult(
                            runId,
                            plan.planId,
                            SkillPlanExecutionStatus.TimedOut,
                            lastSuccessfulPayload,
                            "Plan execution timed out.",
                            executedStepCount,
                            startedAt
                        );
                    }

                    if (cancellationToken.IsCancellationRequested ||
                        manualCancellation.IsCancellationRequested)
                    {
                        return CreateResult(
                            runId,
                            plan.planId,
                            SkillPlanExecutionStatus.Cancelled,
                            lastSuccessfulPayload,
                            "Plan execution was cancelled.",
                            executedStepCount,
                            startedAt
                        );
                    }

                    if (currentStepId == SkillPlanValidator.CompleteTerminal)
                    {
                        return CreateResult(
                            runId,
                            plan.planId,
                            SkillPlanExecutionStatus.Succeeded,
                            lastSuccessfulPayload,
                            "Plan completed.",
                            executedStepCount,
                            startedAt
                        );
                    }

                    if (currentStepId == SkillPlanValidator.FailedTerminal)
                    {
                        return CreateResult(
                            runId,
                            plan.planId,
                            SkillPlanExecutionStatus.Failed,
                            lastSuccessfulPayload,
                            "Plan reached $failed.",
                            executedStepCount,
                            startedAt
                        );
                    }

                    if (executedStepCount >= plan.maximumSteps ||
                        executedStepCount >= SkillPlanValidator.HardMaximumSteps)
                    {
                        return CreateResult(
                            runId,
                            plan.planId,
                            SkillPlanExecutionStatus.Failed,
                            lastSuccessfulPayload,
                            "Plan exceeded maximumSteps.",
                            executedStepCount,
                            startedAt
                        );
                    }

                    if (!stepMap.TryGetValue(
                            currentStepId,
                            out SkillPlanStepData step))
                    {
                        return CreateResult(
                            runId,
                            plan.planId,
                            SkillPlanExecutionStatus.Failed,
                            lastSuccessfulPayload,
                            "Step was not found after validation: " + currentStepId,
                            executedStepCount,
                            startedAt
                        );
                    }

                    if (!implementationRegistry.TryGet(
                            step.skillId,
                            step.skillVersion,
                            out ISkillHandler handler))
                    {
                        return CreateResult(
                            runId,
                            plan.planId,
                            SkillPlanExecutionStatus.Failed,
                            lastSuccessfulPayload,
                            "Handler was not found after validation: " +
                            step.skillId + "@" + step.skillVersion,
                            executedStepCount,
                            startedAt
                        );
                    }

                    ISkillPayload inputPayload;

                    if (string.IsNullOrWhiteSpace(step.inputFrom))
                    {
                        inputPayload = initialPayload;
                    }
                    else if (!outputByStep.TryGetValue(
                                 step.inputFrom,
                                 out inputPayload))
                    {
                        return CreateResult(
                            runId,
                            plan.planId,
                            SkillPlanExecutionStatus.Failed,
                            lastSuccessfulPayload,
                            "inputFrom payload is unavailable: " + step.inputFrom,
                            executedStepCount,
                            startedAt
                        );
                    }

                    if (!validatedPlan.TryGetDescriptor(
                            step.stepId,
                            out SkillDescriptorData descriptor))
                    {
                        return CreateResult(
                            runId,
                            plan.planId,
                            SkillPlanExecutionStatus.Failed,
                            lastSuccessfulPayload,
                            "Descriptor snapshot is unavailable: " + step.stepId,
                            executedStepCount,
                            startedAt
                        );
                    }

                    DateTime stepStartedAt = DateTime.UtcNow;

                    using (CancellationTokenSource stepCancellation =
                           CancellationTokenSource.CreateLinkedTokenSource(
                               linkedCancellation.Token))
                    {
                        double remainingPlanSeconds =
                            Math.Max(
                                0.001,
                                plan.maximumExecutionSeconds -
                                (DateTime.UtcNow - startedAt).TotalSeconds
                            );

                        double stepTimeoutSeconds = descriptor.timeoutSeconds > 0f
                            ? Math.Min(descriptor.timeoutSeconds, remainingPlanSeconds)
                            : remainingPlanSeconds;

                        SkillExecutionContext context =
                            new SkillExecutionContext(
                                runId,
                                plan.planId,
                                step.stepId,
                                stepStartedAt,
                                validatedPlan.Platform,
                                stepCancellation.Token,
                                contextValues
                            );

                        Task<SkillExecutionResult> handlerTask =
                            handler.ExecuteUntypedAsync(inputPayload, context);

                        if (handlerTask == null)
                        {
                            return CreateResult(
                                runId,
                                plan.planId,
                                SkillPlanExecutionStatus.Failed,
                                lastSuccessfulPayload,
                                "Handler returned a null Task.",
                                executedStepCount,
                                startedAt
                            );
                        }

                        HandlerWaitResult waitResult = await WaitForHandlerAsync(
                            handlerTask,
                            stepTimeoutSeconds,
                            linkedCancellation.Token
                        );

                        if (waitResult.Kind != HandlerWaitKind.Completed)
                        {
                            stepCancellation.Cancel();
                            TrackAbandonedHandlerTask(handlerTask);

                            if (planTimeoutCancellation.IsCancellationRequested ||
                                waitResult.Kind == HandlerWaitKind.TimedOut)
                            {
                                return CreateResult(
                                    runId,
                                    plan.planId,
                                    SkillPlanExecutionStatus.TimedOut,
                                    lastSuccessfulPayload,
                                    waitResult.Kind == HandlerWaitKind.TimedOut
                                        ? "Skill Step timed out: " + step.stepId
                                        : "Plan execution timed out.",
                                    executedStepCount,
                                    startedAt
                                );
                            }

                            return CreateResult(
                                runId,
                                plan.planId,
                                SkillPlanExecutionStatus.Cancelled,
                                lastSuccessfulPayload,
                                "Plan execution was cancelled.",
                                executedStepCount,
                                startedAt
                            );
                        }

                        SkillExecutionResult stepResult = waitResult.Result;
                        executedStepCount++;

                        if (!IsCurrentGeneration(generation))
                        {
                            return CreateResult(
                                runId,
                                plan.planId,
                                SkillPlanExecutionStatus.Cancelled,
                                lastSuccessfulPayload,
                                "Stale Run result was discarded.",
                                executedStepCount,
                                startedAt
                            );
                        }

                        if (stepResult == null)
                        {
                            currentStepId = step.onFailure;
                            continue;
                        }

                        if (stepResult.Status == SkillExecutionStatus.Cancelled)
                        {
                            return CreateResult(
                                runId,
                                plan.planId,
                                SkillPlanExecutionStatus.Cancelled,
                                lastSuccessfulPayload,
                                stepResult.Message,
                                executedStepCount,
                                startedAt
                            );
                        }

                        if (stepResult.Status == SkillExecutionStatus.Succeeded)
                        {
                            if (stepResult.Payload == null ||
                                !handler.OutputPayloadType.IsInstanceOfType(
                                    stepResult.Payload))
                            {
                                currentStepId = step.onFailure;
                                continue;
                            }

                            outputByStep[step.stepId] = stepResult.Payload;
                            lastSuccessfulPayload = stepResult.Payload;
                            currentStepId = step.onSuccess;
                        }
                        else
                        {
                            currentStepId = step.onFailure;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                SkillPlanExecutionStatus status =
                    planTimeoutCancellation.IsCancellationRequested
                        ? SkillPlanExecutionStatus.TimedOut
                        : SkillPlanExecutionStatus.Cancelled;

                return CreateResult(
                    runId,
                    plan.planId,
                    status,
                    lastSuccessfulPayload,
                    status == SkillPlanExecutionStatus.TimedOut
                        ? "Plan execution timed out."
                        : "Plan execution was cancelled.",
                    executedStepCount,
                    startedAt
                );
            }
            catch (Exception ex)
            {
                return CreateResult(
                    runId,
                    plan.planId,
                    SkillPlanExecutionStatus.Failed,
                    lastSuccessfulPayload,
                    "Runner exception: " +
                    ex.GetType().Name + ": " + ex.Message,
                    executedStepCount,
                    startedAt
                );
            }
            finally
            {
                linkedCancellation.Dispose();
                planTimeoutCancellation.Dispose();

                lock (stateLock)
                {
                    if (activeGeneration == generation)
                    {
                        isRunning = false;
                        activeCancellation = null;
                    }
                }

                manualCancellation.Dispose();
            }
        }

        private bool IsCurrentGeneration(int generation)
        {
            lock (stateLock)
                return isRunning && activeGeneration == generation;
        }

        private void TrackAbandonedHandlerTask(Task task)
        {
            if (task == null || task.IsCompleted)
            {
                ObserveCompletedTask(task);
                return;
            }

            lock (stateLock)
                pendingHandlerTaskCount++;

            _ = ObserveAbandonedHandlerTaskAsync(task);
        }

        private async Task ObserveAbandonedHandlerTaskAsync(Task task)
        {
            try
            {
                await task;
            }
            catch
            {
                // Exception is deliberately observed. The stale result is discarded.
            }
            finally
            {
                lock (stateLock)
                    pendingHandlerTaskCount--;
            }
        }

        private static void ObserveCompletedTask(Task task)
        {
            if (task == null || !task.IsFaulted)
                return;

            _ = task.Exception;
        }

        private static async Task<HandlerWaitResult> WaitForHandlerAsync(
            Task<SkillExecutionResult> handlerTask,
            double timeoutSeconds,
            CancellationToken cancellationToken)
        {
            Task timeoutTask = Task.Delay(
                TimeSpan.FromSeconds(Math.Max(0.001, timeoutSeconds))
            );

            Task cancellationTask = CreateCancellationTask(cancellationToken);

            Task completed = await Task.WhenAny(
                handlerTask,
                timeoutTask,
                cancellationTask
            );

            if (completed == handlerTask)
            {
                return new HandlerWaitResult(
                    HandlerWaitKind.Completed,
                    await handlerTask
                );
            }

            if (completed == cancellationTask)
            {
                return new HandlerWaitResult(
                    HandlerWaitKind.Cancelled,
                    null
                );
            }

            return new HandlerWaitResult(
                HandlerWaitKind.TimedOut,
                null
            );
        }

        private static Task CreateCancellationTask(
            CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled)
                return Task.Delay(Timeout.Infinite);

            TaskCompletionSource<bool> source =
                new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously
                );

            cancellationToken.Register(
                state => ((TaskCompletionSource<bool>)state).TrySetResult(true),
                source
            );

            return source.Task;
        }

        private static Dictionary<string, SkillPlanStepData> BuildStepMap(
            SkillPlanData plan)
        {
            Dictionary<string, SkillPlanStepData> map =
                new Dictionary<string, SkillPlanStepData>(StringComparer.Ordinal);

            SkillPlanStepData[] steps =
                plan.steps ?? Array.Empty<SkillPlanStepData>();

            for (int i = 0; i < steps.Length; i++)
            {
                SkillPlanStepData step = steps[i];
                if (step != null)
                    map.Add(step.stepId, step);
            }

            return map;
        }

        private static SkillPlanExecutionResult CreateResult(
            string runId,
            string planId,
            SkillPlanExecutionStatus status,
            ISkillPayload payload,
            string message,
            int executedStepCount,
            DateTime startedAt)
        {
            return new SkillPlanExecutionResult(
                runId,
                planId,
                status,
                payload,
                message,
                executedStepCount,
                startedAt,
                DateTime.UtcNow
            );
        }

        private enum HandlerWaitKind
        {
            Completed,
            Cancelled,
            TimedOut
        }

        private sealed class HandlerWaitResult
        {
            public HandlerWaitKind Kind { get; }
            public SkillExecutionResult Result { get; }

            public HandlerWaitResult(
                HandlerWaitKind kind,
                SkillExecutionResult result)
            {
                Kind = kind;
                Result = result;
            }
        }
    }
}
