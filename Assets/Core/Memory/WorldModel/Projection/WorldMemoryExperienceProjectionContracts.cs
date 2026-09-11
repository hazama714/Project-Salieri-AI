// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Core.Experience.Storage;
using SalieriAI.Core.Memory.WorldModel.Contracts;

namespace SalieriAI.Core.Memory.WorldModel.Projection
{
    public enum WorldMemoryExperienceProjectionStatus
    {
        Projected = 0,
        AlreadyProjected = 1,
        UnsupportedQuestion = 2,
        InvalidExperience = 3,
        MissingEntity = 4,
        InvalidAnswer = 5,
        MissingSource = 6,
        InvalidProvenance = 7,
        InvalidGeneratedIdentity = 8,
        StoreRejected = 9
    }

    public interface IWorldMemoryProjectionIdentityGenerator
    {
        string CreateFactId();
        string CreateEvidenceId();
    }

    public sealed class GuidWorldMemoryProjectionIdentityGenerator :
        IWorldMemoryProjectionIdentityGenerator
    {
        public string CreateFactId()
        {
            return Guid.NewGuid().ToString("N");
        }

        public string CreateEvidenceId()
        {
            return Guid.NewGuid().ToString("N");
        }
    }

    public sealed class WorldMemoryExperienceProjectionRequest
    {
        public ExperienceRecord Experience { get; }
        public string EntityId { get; }
        public WorldMemoryEvidenceType EvidenceType { get; }
        public string SourceId { get; }
        public string AttributedSourceId { get; }
        public DateTime ProjectedAtUtc { get; }

        public WorldMemoryExperienceProjectionRequest(
            ExperienceRecord experience,
            string entityId,
            WorldMemoryEvidenceType evidenceType,
            string sourceId,
            string attributedSourceId,
            DateTime projectedAtUtc)
        {
            Experience = experience;
            EntityId = WorldMemoryContractUtility.Id(entityId);
            EvidenceType = evidenceType;
            SourceId = WorldMemoryContractUtility.Id(sourceId);
            AttributedSourceId = WorldMemoryContractUtility.Id(
                attributedSourceId);
            ProjectedAtUtc = WorldMemoryContractUtility.Utc(projectedAtUtc);
        }
    }

    public sealed class WorldMemoryExperienceProjectionResult
    {
        public WorldMemoryExperienceProjectionStatus Status { get; }
        public string EntityId { get; }
        public WorldMemoryFact Fact { get; }
        public WorldMemoryEvidence Evidence { get; }
        public string SourceId { get; }
        public bool FactWasCreated { get; }
        public string Diagnostic { get; }
        public bool Succeeded =>
            Status == WorldMemoryExperienceProjectionStatus.Projected;

        internal WorldMemoryExperienceProjectionResult(
            WorldMemoryExperienceProjectionStatus status,
            string entityId,
            WorldMemoryFact fact,
            WorldMemoryEvidence evidence,
            string sourceId,
            bool factWasCreated,
            string diagnostic)
        {
            Status = status;
            EntityId = entityId ?? string.Empty;
            Fact = fact;
            Evidence = evidence;
            SourceId = sourceId ?? string.Empty;
            FactWasCreated = factWasCreated;
            Diagnostic = diagnostic ?? string.Empty;
        }
    }
}
