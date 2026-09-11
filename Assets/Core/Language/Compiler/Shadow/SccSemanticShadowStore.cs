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

namespace SalieriAI.Core.Language.Compiler.Shadow
{
    public enum SccSemanticShadowRegisterResult
    {
        Stored = 0,
        Duplicate = 1,
        Invalid = 2,
        CapacityRejected = 3
    }

    /// <summary>
    /// Read-only point-in-time diagnostics for the Candidate Set shadow store.
    /// Reading this snapshot applies the same TTL purge as StoredCount.
    /// </summary>
    public sealed class SccSemanticShadowStoreDiagnosticsSnapshot
    {
        public DateTime ObservedAtUtc { get; }
        public int StoredCount { get; }
        public TimeSpan EntryLifetime { get; }
        public int MaximumCapacity { get; }
        public int ExpiredEntryRemovedCount { get; }

        internal SccSemanticShadowStoreDiagnosticsSnapshot(
            DateTime observedAtUtc,
            int storedCount,
            TimeSpan entryLifetime,
            int maximumCapacity,
            int expiredEntryRemovedCount)
        {
            ObservedAtUtc = observedAtUtc;
            StoredCount = storedCount;
            EntryLifetime = entryLifetime;
            MaximumCapacity = maximumCapacity;
            ExpiredEntryRemovedCount = expiredEntryRemovedCount;
        }
    }

    /// <summary>
    /// Bounded, non-consuming, in-memory observation store for SCC semantic
    /// candidate sets. It is not a database, execution queue, validation
    /// result store, or replacement for the Phase 2B comparison store.
    ///
    /// Static state is process/domain-local, is cleared by a normal Domain
    /// Reload, and can be cleared explicitly by tests.
    /// </summary>
    public static class SccSemanticShadowStore
    {
        public const int MaximumCandidateSetCount = 512;
        public static readonly TimeSpan EntryLifetime =
            TimeSpan.FromMinutes(5);

        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<SemanticShadowKey, StoreEntry>
            Entries =
                new Dictionary<SemanticShadowKey, StoreEntry>();
        private static readonly Dictionary<string, SemanticShadowKey>
            CandidateSetIndex =
                new Dictionary<string, SemanticShadowKey>(
                    StringComparer.Ordinal);

        public static int StoredCount
        {
            get
            {
                lock (SyncRoot)
                {
                    PurgeExpiredUnsafe(DateTime.UtcNow);
                    return Entries.Count;
                }
            }
        }

        public static SccSemanticShadowStoreDiagnosticsSnapshot
            GetDiagnosticsSnapshot()
        {
            lock (SyncRoot)
            {
                DateTime now = DateTime.UtcNow;
                int removed = PurgeExpiredUnsafe(now);

                return new SccSemanticShadowStoreDiagnosticsSnapshot(
                    now,
                    Entries.Count,
                    EntryLifetime,
                    MaximumCandidateSetCount,
                    removed
                );
            }
        }

        public static SccSemanticShadowRegisterResult Register(
            SemanticCandidateSet candidateSet,
            SemanticParserSource parserSource)
        {
            if (!IsValid(candidateSet, parserSource))
                return SccSemanticShadowRegisterResult.Invalid;

            lock (SyncRoot)
            {
                DateTime now = DateTime.UtcNow;
                PurgeExpiredUnsafe(now);

                if (CandidateSetIndex.ContainsKey(
                        candidateSet.CandidateSetId))
                {
                    return SccSemanticShadowRegisterResult.Duplicate;
                }

                if (Entries.Count >= MaximumCandidateSetCount)
                {
                    return
                        SccSemanticShadowRegisterResult.CapacityRejected;
                }

                SemanticShadowKey key = new SemanticShadowKey(
                    candidateSet.InteractionId,
                    candidateSet.AnalysisId,
                    parserSource,
                    candidateSet.CandidateSetId
                );

                Entries.Add(
                    key,
                    new StoreEntry(candidateSet, parserSource, now)
                );
                CandidateSetIndex.Add(candidateSet.CandidateSetId, key);
                return SccSemanticShadowRegisterResult.Stored;
            }
        }

        public static bool TryGetByCandidateSetId(
            string candidateSetId,
            out SemanticCandidateSet candidateSet)
        {
            candidateSet = null;

            if (string.IsNullOrWhiteSpace(candidateSetId))
                return false;

            lock (SyncRoot)
            {
                PurgeExpiredUnsafe(DateTime.UtcNow);

                SemanticShadowKey key;
                StoreEntry entry;
                if (!CandidateSetIndex.TryGetValue(
                        candidateSetId.Trim(),
                        out key) ||
                    !Entries.TryGetValue(key, out entry))
                {
                    return false;
                }

                candidateSet = entry.CandidateSet;
                return true;
            }
        }

        public static IReadOnlyList<SemanticCandidateSet>
            GetByInteractionId(string interactionId)
        {
            return GetMatching(
                interactionId,
                false,
                SemanticParserSource.Unknown
            );
        }

        public static IReadOnlyList<SemanticCandidateSet>
            GetByInteractionAndSource(
                string interactionId,
                SemanticParserSource parserSource)
        {
            return GetMatching(interactionId, true, parserSource);
        }

        public static int RemoveExpired(DateTime nowUtc)
        {
            lock (SyncRoot)
            {
                return PurgeExpiredUnsafe(NormalizeUtc(nowUtc));
            }
        }

        /// <summary>
        /// Test-only reset. Production code must rely on TTL and capacity.
        /// </summary>
        public static void ClearForTests()
        {
            lock (SyncRoot)
            {
                Entries.Clear();
                CandidateSetIndex.Clear();
            }
        }

        private static IReadOnlyList<SemanticCandidateSet> GetMatching(
            string interactionId,
            bool filterSource,
            SemanticParserSource parserSource)
        {
            if (string.IsNullOrWhiteSpace(interactionId))
            {
                return new ReadOnlyCollection<SemanticCandidateSet>(
                    new List<SemanticCandidateSet>()
                );
            }

            lock (SyncRoot)
            {
                PurgeExpiredUnsafe(DateTime.UtcNow);

                string safeInteractionId = interactionId.Trim();
                List<StoreEntry> matches = new List<StoreEntry>();

                foreach (
                    KeyValuePair<SemanticShadowKey, StoreEntry> pair
                    in Entries)
                {
                    if (!string.Equals(
                            pair.Key.InteractionId,
                            safeInteractionId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (filterSource &&
                        pair.Key.ParserSource != parserSource)
                    {
                        continue;
                    }

                    matches.Add(pair.Value);
                }

                matches.Sort(CompareEntries);
                List<SemanticCandidateSet> result =
                    new List<SemanticCandidateSet>(matches.Count);

                for (int i = 0; i < matches.Count; i++)
                    result.Add(matches[i].CandidateSet);

                return new ReadOnlyCollection<SemanticCandidateSet>(
                    result
                );
            }
        }

        private static bool IsValid(
            SemanticCandidateSet candidateSet,
            SemanticParserSource parserSource)
        {
            if (candidateSet == null ||
                parserSource == SemanticParserSource.Unknown ||
                string.IsNullOrWhiteSpace(candidateSet.CandidateSetId) ||
                string.IsNullOrWhiteSpace(candidateSet.InteractionId) ||
                string.IsNullOrWhiteSpace(candidateSet.AnalysisId))
            {
                return false;
            }

            for (int i = 0; i < candidateSet.Candidates.Count; i++)
            {
                SemanticIrCandidate candidate =
                    candidateSet.Candidates[i];

                if (candidate == null ||
                    candidate.ParserSource != parserSource ||
                    !string.Equals(
                        candidate.CandidateSetId,
                        candidateSet.CandidateSetId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        candidate.InputId,
                        candidateSet.InputId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        candidate.InteractionId,
                        candidateSet.InteractionId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        candidate.AnalysisId,
                        candidateSet.AnalysisId,
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static int PurgeExpiredUnsafe(DateTime nowUtc)
        {
            List<SemanticShadowKey> expired =
                new List<SemanticShadowKey>();

            foreach (
                KeyValuePair<SemanticShadowKey, StoreEntry> pair
                in Entries)
            {
                if (nowUtc - pair.Value.RegisteredAtUtc > EntryLifetime)
                    expired.Add(pair.Key);
            }

            for (int i = 0; i < expired.Count; i++)
            {
                SemanticShadowKey key = expired[i];
                Entries.Remove(key);
                CandidateSetIndex.Remove(key.CandidateSetId);
            }

            return expired.Count;
        }

        private static int CompareEntries(StoreEntry left, StoreEntry right)
        {
            int timeComparison =
                left.RegisteredAtUtc.CompareTo(right.RegisteredAtUtc);
            if (timeComparison != 0)
                return timeComparison;

            return string.Compare(
                left.CandidateSet.CandidateSetId,
                right.CandidateSet.CandidateSetId,
                StringComparison.Ordinal
            );
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            DateTime safe = value == default(DateTime)
                ? DateTime.UtcNow
                : value;

            if (safe.Kind == DateTimeKind.Utc)
                return safe;

            if (safe.Kind == DateTimeKind.Local)
                return safe.ToUniversalTime();

            return DateTime.SpecifyKind(safe, DateTimeKind.Utc);
        }

        private struct SemanticShadowKey : IEquatable<SemanticShadowKey>
        {
            public string InteractionId { get; }
            public string AnalysisId { get; }
            public SemanticParserSource ParserSource { get; }
            public string CandidateSetId { get; }

            public SemanticShadowKey(
                string interactionId,
                string analysisId,
                SemanticParserSource parserSource,
                string candidateSetId)
            {
                InteractionId = interactionId;
                AnalysisId = analysisId;
                ParserSource = parserSource;
                CandidateSetId = candidateSetId;
            }

            public bool Equals(SemanticShadowKey other)
            {
                return
                    string.Equals(
                        InteractionId,
                        other.InteractionId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        AnalysisId,
                        other.AnalysisId,
                        StringComparison.Ordinal) &&
                    ParserSource == other.ParserSource &&
                    string.Equals(
                        CandidateSetId,
                        other.CandidateSetId,
                        StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is SemanticShadowKey &&
                       Equals((SemanticShadowKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 +
                           StringComparer.Ordinal.GetHashCode(
                               InteractionId);
                    hash = hash * 31 +
                           StringComparer.Ordinal.GetHashCode(AnalysisId);
                    hash = hash * 31 + (int)ParserSource;
                    hash = hash * 31 +
                           StringComparer.Ordinal.GetHashCode(
                               CandidateSetId);
                    return hash;
                }
            }
        }

        private sealed class StoreEntry
        {
            public SemanticCandidateSet CandidateSet { get; }
            public SemanticParserSource ParserSource { get; }
            public DateTime RegisteredAtUtc { get; }

            public StoreEntry(
                SemanticCandidateSet candidateSet,
                SemanticParserSource parserSource,
                DateTime registeredAtUtc)
            {
                CandidateSet = candidateSet;
                ParserSource = parserSource;
                RegisteredAtUtc = registeredAtUtc;
            }
        }
    }
}
