// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;

using SalieriAI.Core.Memory.WorldModel.Contracts;
using SalieriAI.Core.Memory.WorldModel.Storage;

namespace SalieriAI.Core.Memory.WorldModel.Resolution
{
    public enum WorldMemoryEntityResolverStatus
    {
        Applied = 0,
        InvalidRequest = 1,
        MissingObservation = 2,
        InvalidDecision = 3,
        IdentityConflict = 4,
        StoreRejected = 5
    }

    public sealed class WorldMemoryEntityResolutionRequest
    {
        public string ResolutionId { get; }
        public string ObservationId { get; }
        public DateTime CreatedAtUtc { get; }

        public WorldMemoryEntityResolutionRequest(
            string resolutionId,
            string observationId,
            DateTime createdAtUtc)
        {
            ResolutionId = WorldMemoryContractUtility.Id(resolutionId);
            ObservationId = WorldMemoryContractUtility.Id(observationId);
            CreatedAtUtc = WorldMemoryContractUtility.Utc(createdAtUtc);
        }
    }

    public sealed class WorldMemoryEntityResolverResult
    {
        public WorldMemoryEntityResolverStatus Status { get; }
        public WorldMemoryEntityResolutionCandidateSet Candidates { get; }
        public WorldMemoryEntityResolution Resolution { get; }
        public string Error { get; }
        public bool Succeeded =>
            Status == WorldMemoryEntityResolverStatus.Applied;

        internal WorldMemoryEntityResolverResult(
            WorldMemoryEntityResolverStatus status,
            WorldMemoryEntityResolutionCandidateSet candidates,
            WorldMemoryEntityResolution resolution,
            string error)
        {
            Status = status;
            Candidates = candidates;
            Resolution = resolution;
            Error = error ?? string.Empty;
        }
    }

    /// <summary>
    /// Pure C# resolution coordinator. It reads stored evidence, delegates the
    /// decision to an explicit policy, validates it, and appends history. It
    /// never creates an Entity.
    /// </summary>
    public sealed class WorldMemoryEntityResolver
    {
        private readonly IWorldMemoryStore store;

        public WorldMemoryEntityResolver(IWorldMemoryStore store)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public WorldMemoryEntityResolverResult Resolve(
            WorldMemoryEntityResolutionRequest request,
            IWorldMemoryEntityResolutionPolicy policy)
        {
            if (!ValidRequest(request) || policy == null)
            {
                return Result(WorldMemoryEntityResolverStatus.InvalidRequest,
                    null, null, "Resolution request or policy is invalid.");
            }

            if (!store.TryGetObservation(
                    request.ObservationId,
                    out WorldMemoryObservation _))
            {
                return Result(
                    WorldMemoryEntityResolverStatus.MissingObservation,
                    null, null, "Observation does not exist.");
            }

            IReadOnlyList<WorldMemoryRecognitionEvidence> evidence =
                store.GetRecognitionEvidenceByObservation(
                    request.ObservationId);
            var candidates = new WorldMemoryEntityResolutionCandidateSet(
                request.ObservationId, evidence);

            WorldMemoryEntityResolutionPolicyDecision decision;
            try
            {
                decision = policy.Decide(candidates);
            }
            catch (Exception exception)
            {
                return Result(
                    WorldMemoryEntityResolverStatus.InvalidDecision,
                    candidates, null, exception.GetType().Name);
            }

            if (!ValidDecision(candidates, decision, out string error))
            {
                return Result(
                    WorldMemoryEntityResolverStatus.InvalidDecision,
                    candidates, null, error);
            }

            bool conflict = HasConflictingResolvedHistory(
                request.ObservationId, decision);
            WorldMemoryResolutionStatus status = conflict
                ? WorldMemoryResolutionStatus.Rejected
                : decision.Status;
            string entityId = conflict ? string.Empty : decision.EntityId;
            var resolution = new WorldMemoryEntityResolution(
                request.ResolutionId,
                request.ObservationId,
                entityId,
                status,
                WorldMemoryResolutionMethod.RecognitionEvidence,
                request.CreatedAtUtc,
                decision.HasConfidence,
                decision.Confidence,
                decision.SupportingRecognitionEvidenceIds);

            WorldMemoryWriteResult<WorldMemoryEntityResolution> write =
                store.AddEntityResolution(resolution);
            if (!write.Succeeded)
            {
                return Result(
                    WorldMemoryEntityResolverStatus.StoreRejected,
                    candidates, resolution, write.Error);
            }

            return Result(
                conflict
                    ? WorldMemoryEntityResolverStatus.IdentityConflict
                    : WorldMemoryEntityResolverStatus.Applied,
                candidates, resolution,
                conflict
                    ? "A different persistent Entity is already resolved."
                    : string.Empty);
        }

        private bool HasConflictingResolvedHistory(
            string observationId,
            WorldMemoryEntityResolutionPolicyDecision decision)
        {
            if (decision.Status != WorldMemoryResolutionStatus.Resolved)
                return false;

            IReadOnlyList<WorldMemoryEntityResolution> history =
                store.GetEntityResolutionsByObservation(observationId);
            for (int i = 0; i < history.Count; i++)
            {
                WorldMemoryEntityResolution item = history[i];
                if (item.ResolutionStatus ==
                        WorldMemoryResolutionStatus.Resolved &&
                    !string.Equals(
                        item.EntityId,
                        decision.EntityId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ValidRequest(
            WorldMemoryEntityResolutionRequest request)
        {
            return request != null &&
                WorldMemoryContractUtility.IsCanonicalId(
                    request.ResolutionId) &&
                WorldMemoryContractUtility.IsCanonicalId(
                    request.ObservationId) &&
                WorldMemoryContractUtility.IsUtc(request.CreatedAtUtc);
        }

        private bool ValidDecision(
            WorldMemoryEntityResolutionCandidateSet candidates,
            WorldMemoryEntityResolutionPolicyDecision decision,
            out string error)
        {
            if (decision == null ||
                !Enum.IsDefined(typeof(WorldMemoryResolutionStatus),
                    decision.Status) ||
                decision.Status == WorldMemoryResolutionStatus.Unknown)
            {
                error = "Policy returned an invalid status.";
                return false;
            }

            bool resolved =
                decision.Status == WorldMemoryResolutionStatus.Resolved;
            if (resolved)
            {
                if (!WorldMemoryContractUtility.IsCanonicalId(
                        decision.EntityId) ||
                    !candidates.TryGetCandidate(
                        decision.EntityId,
                        out WorldMemoryEntityResolutionCandidate _) ||
                    !store.TryGetEntity(
                        decision.EntityId,
                        out WorldMemoryEntity _))
                {
                    error = "Resolved Entity is not a valid candidate.";
                    return false;
                }
            }
            else if (decision.EntityId.Length > 0)
            {
                error = "Non-resolved decision fabricated an EntityId.";
                return false;
            }

            if (!WorldMemoryContractUtility.ValidOptionalScore(
                    decision.HasConfidence,
                    decision.Confidence))
            {
                error = "Decision confidence is invalid.";
                return false;
            }

            if (decision.SupportingRecognitionEvidenceIds.Count == 0)
            {
                error = "Recognition decision has no supporting evidence.";
                return false;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0;
                 i < decision.SupportingRecognitionEvidenceIds.Count;
                 i++)
            {
                string id = decision.SupportingRecognitionEvidenceIds[i];
                if (!seen.Add(id) || !candidates.ContainsEvidence(id))
                {
                    error = "Supporting evidence is invalid or duplicated.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static WorldMemoryEntityResolverResult Result(
            WorldMemoryEntityResolverStatus status,
            WorldMemoryEntityResolutionCandidateSet candidates,
            WorldMemoryEntityResolution resolution,
            string error)
        {
            return new WorldMemoryEntityResolverResult(
                status, candidates, resolution, error);
        }
    }
}
