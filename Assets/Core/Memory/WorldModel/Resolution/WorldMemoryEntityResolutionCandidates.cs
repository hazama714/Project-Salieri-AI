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

using SalieriAI.Core.Memory.WorldModel.Contracts;

namespace SalieriAI.Core.Memory.WorldModel.Resolution
{
    public sealed class WorldMemoryEntityResolutionCandidate
    {
        public string EntityId { get; }
        public IReadOnlyList<WorldMemoryRecognitionEvidence> Evidence { get; }

        internal WorldMemoryEntityResolutionCandidate(
            string entityId,
            IEnumerable<WorldMemoryRecognitionEvidence> evidence)
        {
            EntityId = WorldMemoryContractUtility.Id(entityId);
            Evidence = new ReadOnlyCollection<WorldMemoryRecognitionEvidence>(
                new List<WorldMemoryRecognitionEvidence>(evidence));
        }
    }

    /// <summary>
    /// Deterministic, insertion-ordered recognition candidates for exactly one
    /// Observation. It groups evidence but applies no score threshold.
    /// </summary>
    public sealed class WorldMemoryEntityResolutionCandidateSet
    {
        public string ObservationId { get; }
        public IReadOnlyList<WorldMemoryRecognitionEvidence> ActiveEvidence
            { get; }
        public IReadOnlyList<WorldMemoryEntityResolutionCandidate> Candidates
            { get; }

        internal WorldMemoryEntityResolutionCandidateSet(
            string observationId,
            IEnumerable<WorldMemoryRecognitionEvidence> evidence)
        {
            ObservationId = WorldMemoryContractUtility.Id(observationId);

            var active = new List<WorldMemoryRecognitionEvidence>();
            var candidateOrder = new List<string>();
            var grouped = new Dictionary<string,
                List<WorldMemoryRecognitionEvidence>>(StringComparer.Ordinal);

            if (evidence != null)
            {
                foreach (WorldMemoryRecognitionEvidence item in evidence)
                {
                    if (item == null ||
                        item.Status !=
                            WorldMemoryRecognitionEvidenceStatus.Active)
                    {
                        continue;
                    }

                    active.Add(item);
                    if (item.CandidateEntityId.Length == 0)
                        continue;

                    if (!grouped.TryGetValue(
                            item.CandidateEntityId,
                            out List<WorldMemoryRecognitionEvidence> values))
                    {
                        values = new List<WorldMemoryRecognitionEvidence>();
                        grouped.Add(item.CandidateEntityId, values);
                        candidateOrder.Add(item.CandidateEntityId);
                    }

                    values.Add(item);
                }
            }

            var candidates = new List<WorldMemoryEntityResolutionCandidate>();
            for (int i = 0; i < candidateOrder.Count; i++)
            {
                string entityId = candidateOrder[i];
                candidates.Add(new WorldMemoryEntityResolutionCandidate(
                    entityId, grouped[entityId]));
            }

            ActiveEvidence =
                new ReadOnlyCollection<WorldMemoryRecognitionEvidence>(active);
            Candidates =
                new ReadOnlyCollection<WorldMemoryEntityResolutionCandidate>(
                    candidates);
        }

        public bool TryGetCandidate(
            string entityId,
            out WorldMemoryEntityResolutionCandidate candidate)
        {
            string id = WorldMemoryContractUtility.Id(entityId);
            for (int i = 0; i < Candidates.Count; i++)
            {
                if (string.Equals(
                        Candidates[i].EntityId,
                        id,
                        StringComparison.Ordinal))
                {
                    candidate = Candidates[i];
                    return true;
                }
            }

            candidate = null;
            return false;
        }

        public bool ContainsEvidence(string recognitionEvidenceId)
        {
            string id = WorldMemoryContractUtility.Id(recognitionEvidenceId);
            for (int i = 0; i < ActiveEvidence.Count; i++)
            {
                if (string.Equals(
                        ActiveEvidence[i].RecognitionEvidenceId,
                        id,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Explicit policy output. No default production policy or threshold is
    /// supplied by M5-DB2.
    /// </summary>
    public sealed class WorldMemoryEntityResolutionPolicyDecision
    {
        public WorldMemoryResolutionStatus Status { get; }
        public string EntityId { get; }
        public bool HasConfidence { get; }
        public float Confidence { get; }
        public IReadOnlyList<string> SupportingRecognitionEvidenceIds
            { get; }

        public WorldMemoryEntityResolutionPolicyDecision(
            WorldMemoryResolutionStatus status,
            string entityId,
            bool hasConfidence,
            float confidence,
            IEnumerable<string> supportingRecognitionEvidenceIds)
        {
            Status = status;
            EntityId = WorldMemoryContractUtility.Id(entityId);
            HasConfidence = hasConfidence;
            Confidence = confidence;
            SupportingRecognitionEvidenceIds =
                WorldMemoryContractUtility.IdCopy(
                    supportingRecognitionEvidenceIds);
        }
    }

    public interface IWorldMemoryEntityResolutionPolicy
    {
        WorldMemoryEntityResolutionPolicyDecision Decide(
            WorldMemoryEntityResolutionCandidateSet candidates);
    }
}
