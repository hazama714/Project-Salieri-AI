// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;
using System.Globalization;

using SalieriAI.Core.Memory.WorldModel.Contracts;
using SalieriAI.Core.Memory.WorldModel.Storage;

namespace SalieriAI.Core.Memory.WorldModel.Resolution
{
    public enum WorldMemoryEntityPromotionStatus
    {
        Promoted = 0,
        AlreadyPromoted = 1,
        InvalidRequest = 2,
        MissingObservation = 3,
        AlreadyResolved = 4,
        AmbiguousNotPromotable = 5,
        InvalidGeneratedIdentity = 6,
        StoreRejected = 7
    }

    public interface IWorldMemoryPersistentIdentityGenerator
    {
        string CreateEntityId();
        string CreateResolutionId();
    }

    public sealed class GuidWorldMemoryPersistentIdentityGenerator :
        IWorldMemoryPersistentIdentityGenerator
    {
        public string CreateEntityId()
        {
            return Guid.NewGuid().ToString("N");
        }

        public string CreateResolutionId()
        {
            return Guid.NewGuid().ToString("N");
        }
    }

    public sealed class WorldMemoryEntityPromotionRequest
    {
        public string PromotionRequestId { get; }
        public string ObservationId { get; }
        public WorldMemoryEntityType RequestedEntityType { get; }
        public string CanonicalName { get; }
        public WorldMemoryResolutionMethod ResolutionMethod { get; }
        public string Reason { get; }
        public DateTime CreatedAtUtc { get; }

        public WorldMemoryEntityPromotionRequest(
            string promotionRequestId,
            string observationId,
            WorldMemoryEntityType requestedEntityType,
            string canonicalName,
            WorldMemoryResolutionMethod resolutionMethod,
            string reason,
            DateTime createdAtUtc)
        {
            PromotionRequestId = WorldMemoryContractUtility.Id(
                promotionRequestId);
            ObservationId = WorldMemoryContractUtility.Id(observationId);
            RequestedEntityType = requestedEntityType;
            CanonicalName = WorldMemoryContractUtility.Text(canonicalName);
            ResolutionMethod = resolutionMethod;
            Reason = WorldMemoryContractUtility.Text(reason);
            CreatedAtUtc = WorldMemoryContractUtility.Utc(createdAtUtc);
        }
    }

    public sealed class WorldMemoryEntityPromotionResult
    {
        public WorldMemoryEntityPromotionStatus Status { get; }
        public string PromotionRequestId { get; }
        public WorldMemoryEntity Entity { get; }
        public WorldMemoryEntityResolution Resolution { get; }
        public string Error { get; }
        public bool Succeeded =>
            Status == WorldMemoryEntityPromotionStatus.Promoted;

        internal WorldMemoryEntityPromotionResult(
            WorldMemoryEntityPromotionStatus status,
            string promotionRequestId,
            WorldMemoryEntity entity,
            WorldMemoryEntityResolution resolution,
            string error)
        {
            Status = status;
            PromotionRequestId = promotionRequestId ?? string.Empty;
            Entity = entity;
            Resolution = resolution;
            Error = error ?? string.Empty;
        }
    }

    /// <summary>
    /// The only M5-DB2 boundary allowed to turn an Observation into a new
    /// persistent Entity. It is explicit, fail-closed, and in-memory only.
    /// </summary>
    public sealed class WorldMemoryEntityPromotionService
    {
        private readonly object gate = new object();
        private readonly IWorldMemoryAtomicMutationStore store;
        private readonly IWorldMemoryPersistentIdentityGenerator identities;
        private readonly Dictionary<string, WorldMemoryEntityPromotionResult>
            completed =
                new Dictionary<string, WorldMemoryEntityPromotionResult>(
                    StringComparer.Ordinal);

        public WorldMemoryEntityPromotionService(
            IWorldMemoryAtomicMutationStore store,
            IWorldMemoryPersistentIdentityGenerator identities)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.identities = identities ??
                throw new ArgumentNullException(nameof(identities));
        }

        public WorldMemoryEntityPromotionResult Promote(
            WorldMemoryEntityPromotionRequest request)
        {
            lock (gate)
            {
                if (!ValidRequest(request))
                {
                    return Result(
                        WorldMemoryEntityPromotionStatus.InvalidRequest,
                        request == null
                            ? string.Empty
                            : request.PromotionRequestId,
                        null, null, "Promotion request is invalid.");
                }

                if (completed.TryGetValue(
                        request.PromotionRequestId,
                        out WorldMemoryEntityPromotionResult previous))
                {
                    return Result(
                        WorldMemoryEntityPromotionStatus.AlreadyPromoted,
                        request.PromotionRequestId,
                        previous.Entity,
                        previous.Resolution,
                        "Promotion request was already applied.");
                }

                if (!store.TryGetObservation(
                        request.ObservationId,
                        out WorldMemoryObservation observation))
                {
                    return Result(
                        WorldMemoryEntityPromotionStatus.MissingObservation,
                        request.PromotionRequestId,
                        null, null, "Observation does not exist.");
                }

                IReadOnlyList<WorldMemoryEntityResolution> history =
                    store.GetEntityResolutionsByObservation(
                        request.ObservationId);
                for (int i = 0; i < history.Count; i++)
                {
                    if (history[i].ResolutionStatus ==
                        WorldMemoryResolutionStatus.Resolved)
                    {
                        return Result(
                            WorldMemoryEntityPromotionStatus.AlreadyResolved,
                            request.PromotionRequestId,
                            null, history[i],
                            "Observation already resolves to an Entity.");
                    }
                }

                if (store.TryGetCurrentEntityResolution(
                        request.ObservationId,
                        out WorldMemoryEntityResolution current) &&
                    current.ResolutionStatus ==
                        WorldMemoryResolutionStatus.Ambiguous)
                {
                    return Result(
                        WorldMemoryEntityPromotionStatus
                            .AmbiguousNotPromotable,
                        request.PromotionRequestId,
                        null, current,
                        "Ambiguous Observation cannot be promoted.");
                }

                string entityId = WorldMemoryContractUtility.Id(
                    identities.CreateEntityId());
                string resolutionId = WorldMemoryContractUtility.Id(
                    identities.CreateResolutionId());
                IReadOnlyList<WorldMemoryRecognitionEvidence>
                    recognitionEvidence =
                        store.GetRecognitionEvidenceByObservation(
                            request.ObservationId);
                if (!ValidGeneratedIds(
                        entityId,
                        resolutionId,
                        observation,
                        recognitionEvidence))
                {
                    return Result(
                        WorldMemoryEntityPromotionStatus
                            .InvalidGeneratedIdentity,
                        request.PromotionRequestId,
                        null, null,
                        "Generated identities are invalid or reuse provenance.");
                }

                if (store.TryGetEntity(entityId, out WorldMemoryEntity _) ||
                    store.TryGetEntityResolution(
                        resolutionId,
                        out WorldMemoryEntityResolution _))
                {
                    return Result(
                        WorldMemoryEntityPromotionStatus
                            .InvalidGeneratedIdentity,
                        request.PromotionRequestId,
                        null, null, "Generated identity already exists.");
                }

                var entity = new WorldMemoryEntity(
                    entityId,
                    request.RequestedEntityType,
                    request.CanonicalName,
                    WorldMemoryEntityStatus.Active,
                    request.CreatedAtUtc,
                    request.CreatedAtUtc);
                var resolution = new WorldMemoryEntityResolution(
                    resolutionId,
                    request.ObservationId,
                    entityId,
                    WorldMemoryResolutionStatus.Resolved,
                    request.ResolutionMethod,
                    request.CreatedAtUtc,
                    false,
                    0f,
                    Array.Empty<string>());

                WorldMemoryAtomicMutationResult mutation =
                    store.ApplyAtomicMutation(
                        WorldMemoryAtomicMutation.ForEntityPromotion(
                            entity, resolution));
                if (!mutation.Succeeded)
                {
                    return Result(
                        WorldMemoryEntityPromotionStatus.StoreRejected,
                        request.PromotionRequestId,
                        entity, resolution, mutation.Error);
                }

                WorldMemoryEntityPromotionResult result = Result(
                    WorldMemoryEntityPromotionStatus.Promoted,
                    request.PromotionRequestId,
                    entity, resolution, string.Empty);
                completed.Add(request.PromotionRequestId, result);
                return result;
            }
        }

        private static bool ValidRequest(
            WorldMemoryEntityPromotionRequest request)
        {
            return request != null &&
                WorldMemoryContractUtility.IsCanonicalId(
                    request.PromotionRequestId) &&
                WorldMemoryContractUtility.IsCanonicalId(
                    request.ObservationId) &&
                Enum.IsDefined(
                    typeof(WorldMemoryEntityType),
                    request.RequestedEntityType) &&
                request.RequestedEntityType != WorldMemoryEntityType.Unknown &&
                ValidPromotionMethod(request.ResolutionMethod) &&
                request.Reason.Length > 0 &&
                WorldMemoryContractUtility.IsUtc(request.CreatedAtUtc);
        }

        private static bool ValidPromotionMethod(
            WorldMemoryResolutionMethod method)
        {
            return method == WorldMemoryResolutionMethod.ManualConfirmation ||
                method == WorldMemoryResolutionMethod.HumanTeaching ||
                method == WorldMemoryResolutionMethod.ImportedMapping;
        }

        private static bool ValidGeneratedIds(
            string entityId,
            string resolutionId,
            WorldMemoryObservation observation,
            IReadOnlyList<WorldMemoryRecognitionEvidence> evidence)
        {
            if (!WorldMemoryContractUtility.IsCanonicalId(entityId) ||
                !WorldMemoryContractUtility.IsCanonicalId(resolutionId) ||
                string.Equals(entityId, resolutionId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            string frame = observation.SourceFrameId.HasValue
                ? observation.SourceFrameId.Value.ToString(
                    CultureInfo.InvariantCulture)
                : string.Empty;
            string track = observation.TrackId.HasValue
                ? observation.TrackId.Value.ToString(
                    CultureInfo.InvariantCulture)
                : string.Empty;

            if (Same(entityId, observation.ObservationId) ||
                Same(entityId, observation.SessionId) ||
                Same(entityId, observation.TargetKey) ||
                Same(entityId, frame) ||
                Same(entityId, track))
            {
                return false;
            }

            for (int i = 0; i < evidence.Count; i++)
            {
                if (Same(entityId, evidence[i].RecognitionEvidenceId))
                    return false;
            }

            return !entityId.StartsWith(
                "rk0:", StringComparison.OrdinalIgnoreCase) &&
                !resolutionId.StartsWith(
                    "rk0:", StringComparison.OrdinalIgnoreCase);
        }

        private static bool Same(string first, string second)
        {
            return string.Equals(
                first,
                WorldMemoryContractUtility.Id(second),
                StringComparison.Ordinal);
        }

        private static WorldMemoryEntityPromotionResult Result(
            WorldMemoryEntityPromotionStatus status,
            string requestId,
            WorldMemoryEntity entity,
            WorldMemoryEntityResolution resolution,
            string error)
        {
            return new WorldMemoryEntityPromotionResult(
                status, requestId, entity, resolution, error);
        }
    }
}
