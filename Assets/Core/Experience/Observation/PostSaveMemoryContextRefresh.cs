// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Core.Behavior.FindPointAsk;
using SalieriAI.Core.Experience.AnswerBinding;
using SalieriAI.Core.Experience.Recall;
using SalieriAI.Core.Experience.Storage;

using UnityEngine;

namespace SalieriAI.Core.Experience.Observation
{
    public enum PostSaveMemoryContextRefreshStatus
    {
        Refreshed = 0,
        NotEligible = 1,
        InvalidTargetEvidence = 2,
        RuntimeHostUnavailable = 3,
        CurrentTargetUnavailable = 4,
        CurrentTargetMismatch = 5,
        RefreshFailed = 6
    }

    /// <summary>
    /// Immutable result of the narrow Save -> current-memory refresh boundary.
    /// A successful refresh does not imply Known; it reports the exact Recall
    /// assessment produced by the existing M5-A runtime path.
    /// </summary>
    public sealed class PostSaveMemoryContextRefreshResult
    {
        public PostSaveMemoryContextRefreshStatus Status { get; }
        public string TargetKey { get; }
        public int TrackId { get; }
        public RecallStatus RecallStatus { get; }
        public string KnownName { get; }
        public string ExperienceRecordId { get; }
        public string Error { get; }

        public bool Refreshed =>
            Status == PostSaveMemoryContextRefreshStatus.Refreshed;

        public PostSaveMemoryContextRefreshResult(
            PostSaveMemoryContextRefreshStatus status,
            string targetKey,
            int trackId,
            RecallStatus recallStatus,
            string knownName,
            string experienceRecordId,
            string error)
        {
            Status = status;
            TargetKey = Text(targetKey);
            TrackId = trackId;
            RecallStatus = recallStatus;
            KnownName = Text(knownName);
            ExperienceRecordId = Text(experienceRecordId);
            Error = error ?? string.Empty;
        }

        internal static PostSaveMemoryContextRefreshResult Reject(
            PostSaveMemoryContextRefreshStatus status,
            string targetKey,
            int trackId,
            string error)
        {
            return new PostSaveMemoryContextRefreshResult(
                status,
                targetKey,
                trackId,
                RecallStatus.NoKey,
                string.Empty,
                string.Empty,
                error);
        }

        private static string Text(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }
    }

    /// <summary>
    /// Runtime capability consumed by the integration coordinator. TargetKey
    /// and TrackId are used only for immediate runtime correlation.
    /// </summary>
    public interface IPostSaveMemoryContextRefresher
    {
        PostSaveMemoryContextRefreshResult TryRefreshSavedTarget(
            TargetContext frozenTarget,
            DateTime evaluatedAtUtc);
    }

    /// <summary>
    /// Pure admission boundary for a completed OneShot answer outcome.
    /// Duplicate or failed saves never reach the runtime refresher.
    /// </summary>
    public sealed class PostSaveMemoryContextRefreshCoordinator
    {
        public PostSaveMemoryContextRefreshResult RefreshAfterSave(
            OneShotAnswerOutcome outcome,
            IPostSaveMemoryContextRefresher refresher,
            DateTime evaluatedAtUtc)
        {
            TargetContext target = outcome != null
                ? outcome.TargetContext
                : null;
            if (outcome == null || !outcome.BindingMatched ||
                outcome.SaveStatus != ExperienceSaveStatus.Saved)
            {
                return PostSaveMemoryContextRefreshResult.Reject(
                    PostSaveMemoryContextRefreshStatus.NotEligible,
                    target != null ? target.TargetKey : string.Empty,
                    target != null ? target.TrackId : -1,
                    "Only a newly Saved Matched OneShot answer may refresh memory.");
            }

            if (target == null || !target.HasSufficientEvidence)
            {
                return PostSaveMemoryContextRefreshResult.Reject(
                    PostSaveMemoryContextRefreshStatus.InvalidTargetEvidence,
                    target != null ? target.TargetKey : string.Empty,
                    target != null ? target.TrackId : -1,
                    "Frozen answer target evidence is unavailable.");
            }

            if (refresher == null)
            {
                return PostSaveMemoryContextRefreshResult.Reject(
                    PostSaveMemoryContextRefreshStatus.RuntimeHostUnavailable,
                    target.TargetKey,
                    target.TrackId,
                    "Observed object memory runtime host is unavailable.");
            }

            return refresher.TryRefreshSavedTarget(target, evaluatedAtUtc);
        }
    }

    /// <summary>
    /// Production composition boundary. It resolves the single runtime host;
    /// FindPointAsk remains unaware of the memory runtime implementation.
    /// </summary>
    public static class PostSaveMemoryContextRefreshRuntime
    {
        public static PostSaveMemoryContextRefreshResult RefreshAfterSave(
            OneShotAnswerOutcome outcome,
            DateTime evaluatedAtUtc)
        {
            ObservedObjectMemoryContextRuntimeHost[] hosts =
                UnityEngine.Object.FindObjectsOfType<
                    ObservedObjectMemoryContextRuntimeHost>();
            if (hosts == null || hosts.Length != 1)
            {
                TargetContext target = outcome != null
                    ? outcome.TargetContext
                    : null;
                return PostSaveMemoryContextRefreshResult.Reject(
                    PostSaveMemoryContextRefreshStatus.RuntimeHostUnavailable,
                    target != null ? target.TargetKey : string.Empty,
                    target != null ? target.TrackId : -1,
                    "Expected exactly one observed object memory runtime host. " +
                    "Count=" + (hosts != null ? hosts.Length : 0));
            }

            return new PostSaveMemoryContextRefreshCoordinator()
                .RefreshAfterSave(outcome, hosts[0], evaluatedAtUtc);
        }
    }
}
