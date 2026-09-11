// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Core.Experience.Storage;
using SalieriAI.Core.Experience.AnswerBinding;
using SalieriAI.Core.Perception.ObjectTargeting;
using SalieriAI.Core.Perception.VisualSnapshots;

using UnityEngine;

namespace SalieriAI.Core.Experience.Observation
{
    public interface IObservedObjectMemoryTargetSource
    {
        ObservationTarget CurrentTarget { get; }

        event Action<ObservationTarget> TargetChanged;
        event Action<string> TargetCleared;

        bool TryGetVisualFrameSnapshot(
            string sessionId,
            long sourceFrameId,
            DateTime evaluatedAtUtc,
            out VisualFrameSnapshot snapshot,
            out string error);
    }

    internal sealed class ObservationTargetMemorySourceAdapter :
        IObservedObjectMemoryTargetSource
    {
        private readonly ObservationTargetSelectionService source;

        internal ObservationTargetMemorySourceAdapter(
            ObservationTargetSelectionService source)
        {
            this.source = source;
        }

        public ObservationTarget CurrentTarget =>
            source != null ? source.CurrentTarget : null;

        public event Action<ObservationTarget> TargetChanged
        {
            add
            {
                if (source != null)
                    source.TargetChanged += value;
            }
            remove
            {
                if (source != null)
                    source.TargetChanged -= value;
            }
        }

        public event Action<string> TargetCleared
        {
            add
            {
                if (source != null)
                    source.TargetCleared += value;
            }
            remove
            {
                if (source != null)
                    source.TargetCleared -= value;
            }
        }

        public bool TryGetVisualFrameSnapshot(
            string sessionId,
            long sourceFrameId,
            DateTime evaluatedAtUtc,
            out VisualFrameSnapshot snapshot,
            out string error)
        {
            if (source == null)
            {
                snapshot = null;
                error = "ObservationTargetSelectionService is unavailable.";
                return false;
            }
            return source.TryGetVisualFrameSnapshot(
                sessionId,
                sourceFrameId,
                evaluatedAtUtc,
                out snapshot,
                out error);
        }
    }

    /// <summary>
    /// Main-thread Production boundary from TargetChanged to read-only memory
    /// assessment. It deliberately does not subscribe to TargetUpdated.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ObservedObjectMemoryContextRuntimeHost :
        MonoBehaviour,
        IObservedObjectMemoryContextProvider,
        IPostSaveMemoryContextRefresher
    {
        private readonly ObservedObjectMemoryContextService contextService =
            new ObservedObjectMemoryContextService();

        private IObservedObjectMemoryTargetSource targetSource;
        private ObservedObjectMemoryAssessmentService assessmentService;
        private bool subscribed;

        public bool HasCurrentContext => contextService.HasCurrentContext;
        public ObservedObjectMemoryContext CurrentContext =>
            contextService.CurrentContext;
        public int EvaluationCount { get; private set; }
        public string LastEvaluationError { get; private set; } = string.Empty;

        public event Action<ObservedObjectMemoryContext> ContextChanged
        {
            add => contextService.ContextChanged += value;
            remove => contextService.ContextChanged -= value;
        }

        public event Action<ObservedObjectMemoryContext, string> ContextCleared
        {
            add => contextService.ContextCleared += value;
            remove => contextService.ContextCleared -= value;
        }

        public void Configure(
            ObservationTargetSelectionService source,
            IExperienceStore store)
        {
            Configure(
                source != null
                    ? new ObservationTargetMemorySourceAdapter(source)
                    : null,
                store);
        }

        public void Configure(
            IObservedObjectMemoryTargetSource source,
            IExperienceStore store)
        {
            Unsubscribe();
            contextService.Clear("Runtime host reconfigured.");
            targetSource = source;
            assessmentService = new ObservedObjectMemoryAssessmentService(store);
            LastEvaluationError = string.Empty;

            if (isActiveAndEnabled)
                SubscribeAndEvaluateInitial();
        }

        private void OnEnable()
        {
            SubscribeAndEvaluateInitial();
        }

        private void OnDisable()
        {
            Unsubscribe();
            contextService.Clear("Runtime host disabled.");
        }

        private void OnDestroy()
        {
            Unsubscribe();
            contextService.Clear("Runtime host destroyed.");
        }

        public bool RefreshCurrentTarget(DateTime evaluatedAtUtc)
        {
            return EvaluateTarget(
                targetSource != null ? targetSource.CurrentTarget : null,
                evaluatedAtUtc);
        }

        public PostSaveMemoryContextRefreshResult TryRefreshSavedTarget(
            TargetContext frozenTarget,
            DateTime evaluatedAtUtc)
        {
            if (frozenTarget == null || !frozenTarget.HasSufficientEvidence)
            {
                return PostSaveMemoryContextRefreshResult.Reject(
                    PostSaveMemoryContextRefreshStatus.InvalidTargetEvidence,
                    frozenTarget != null
                        ? frozenTarget.TargetKey
                        : string.Empty,
                    frozenTarget != null ? frozenTarget.TrackId : -1,
                    "Frozen target evidence is unavailable.");
            }

            ObservationTarget current = targetSource != null
                ? targetSource.CurrentTarget
                : null;
            if (current == null || current.TrackSnapshot == null)
            {
                return PostSaveMemoryContextRefreshResult.Reject(
                    PostSaveMemoryContextRefreshStatus.CurrentTargetUnavailable,
                    frozenTarget.TargetKey,
                    frozenTarget.TrackId,
                    "Current observation target is unavailable.");
            }

            if (!string.Equals(
                    current.TargetKey,
                    frozenTarget.TargetKey,
                    StringComparison.Ordinal) ||
                current.TrackSnapshot.TrackId != frozenTarget.TrackId)
            {
                return PostSaveMemoryContextRefreshResult.Reject(
                    PostSaveMemoryContextRefreshStatus.CurrentTargetMismatch,
                    frozenTarget.TargetKey,
                    frozenTarget.TrackId,
                    "Current observation target does not match the saved " +
                    "question target.");
            }

            if (!RefreshCurrentTarget(evaluatedAtUtc) ||
                contextService.CurrentContext == null)
            {
                return PostSaveMemoryContextRefreshResult.Reject(
                    PostSaveMemoryContextRefreshStatus.RefreshFailed,
                    frozenTarget.TargetKey,
                    frozenTarget.TrackId,
                    LastEvaluationError);
            }

            ObservedObjectMemoryContext context =
                contextService.CurrentContext;
            return new PostSaveMemoryContextRefreshResult(
                PostSaveMemoryContextRefreshStatus.Refreshed,
                frozenTarget.TargetKey,
                frozenTarget.TrackId,
                context.RecallStatus,
                context.KnownName,
                context.ExperienceRecordId,
                context.DiagnosticError);
        }

        private void SubscribeAndEvaluateInitial()
        {
            if (subscribed || targetSource == null || assessmentService == null)
                return;

            targetSource.TargetChanged += HandleTargetChanged;
            targetSource.TargetCleared += HandleTargetCleared;
            subscribed = true;

            if (targetSource.CurrentTarget != null)
                EvaluateTarget(targetSource.CurrentTarget, DateTime.UtcNow);
        }

        private void Unsubscribe()
        {
            if (!subscribed || targetSource == null)
                return;

            targetSource.TargetChanged -= HandleTargetChanged;
            targetSource.TargetCleared -= HandleTargetCleared;
            subscribed = false;
        }

        private void HandleTargetChanged(ObservationTarget target)
        {
            if (target == null)
            {
                contextService.Clear("TargetChanged supplied no target.");
                return;
            }
            EvaluateTarget(target, DateTime.UtcNow);
        }

        private void HandleTargetCleared(string reason)
        {
            LastEvaluationError = reason ?? string.Empty;
            contextService.Clear(reason);
        }

        private bool EvaluateTarget(
            ObservationTarget target,
            DateTime evaluatedAtUtc)
        {
            ObservedObjectMemoryAssessmentRequest request =
                ObservedObjectMemoryAssessmentRequest.FromTarget(target);
            if (request == null || targetSource == null || assessmentService == null)
            {
                LastEvaluationError = "Memory assessment dependencies are unavailable.";
                contextService.Clear(LastEvaluationError);
                return false;
            }

            EvaluationCount++;
            VisualCropSnapshot crop = null;
            string evidenceError = string.Empty;
            if (!targetSource.TryGetVisualFrameSnapshot(
                    request.SessionId,
                    request.SourceFrameId,
                    evaluatedAtUtc,
                    out VisualFrameSnapshot frame,
                    out evidenceError))
            {
                frame = null;
            }
            else if (frame == null ||
                     !string.Equals(
                         frame.SessionId,
                         request.SessionId,
                         StringComparison.Ordinal) ||
                     frame.SourceFrameId != request.SourceFrameId)
            {
                evidenceError =
                    "Resolved visual snapshot correlation does not match target.";
            }
            else if (!frame.TryCreateCrop(
                         request.CreateBoundingBox(),
                         out crop,
                         out evidenceError))
            {
                crop = null;
            }

            ObservedObjectMemoryContext assessed = assessmentService.Evaluate(
                request,
                crop,
                evaluatedAtUtc,
                evidenceError);

            ObservationTarget current = targetSource.CurrentTarget;
            if (!request.Matches(current))
            {
                LastEvaluationError =
                    "Memory assessment discarded because target correlation changed.";
                return false;
            }

            LastEvaluationError = assessed != null
                ? assessed.DiagnosticError
                : "Memory assessment returned no context.";
            if (assessed == null)
                return false;

            contextService.ReplaceCurrent(assessed);
            return true;
        }
    }
}
