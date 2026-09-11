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

namespace SalieriAI.Core.Language.Compiler.Validation
{
    public enum CglSemanticValidationShadowScope
    {
        Unknown = 0,
        Candidate = 1,
        CandidateSet = 2
    }

    public enum CglSemanticValidationShadowRegisterResult
    {
        Stored = 0,
        Duplicate = 1,
        Invalid = 2,
        CapacityRejected = 3
    }

    /// <summary>
    /// Read-only point-in-time diagnostics for the Validation shadow store.
    /// Reading this snapshot applies the same TTL purge as StoredCount.
    /// </summary>
    public sealed class CglSemanticValidationStoreDiagnosticsSnapshot
    {
        public DateTime ObservedAtUtc { get; }
        public int StoredCount { get; }
        public TimeSpan EntryLifetime { get; }
        public int MaximumCapacity { get; }
        public int ExpiredEntryRemovedCount { get; }

        internal CglSemanticValidationStoreDiagnosticsSnapshot(
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
    /// Immutable validation observation stored separately from Candidates.
    /// Candidate scope retains a defensive SemanticValidationRecord copy.
    /// CandidateSet scope records an Empty Set without inventing a Candidate.
    /// </summary>
    public sealed class CglSemanticValidationShadowSnapshot
    {
        public string ValidationId { get; }
        public CglSemanticValidationShadowScope Scope { get; }
        public string CandidateId { get; }
        public string SemanticIrId { get; }
        public IReadOnlyList<string> SemanticIrIds { get; }
        public string CandidateSetId { get; }
        public string InputId { get; }
        public string InteractionId { get; }
        public string AnalysisId { get; }
        public string ValidatorVersion { get; }
        public SemanticValidationStatus Status { get; }
        public DateTime CheckedAtUtc { get; }
        public DateTime RegisteredAtUtc { get; }
        public IReadOnlyList<string> FailureCodes { get; }
        public IReadOnlyList<string> InvalidFields { get; }
        public bool RequiresClarification { get; }
        public string DiagnosticMessage { get; }
        public SemanticValidationRecord CandidateRecord { get; }

        private CglSemanticValidationShadowSnapshot(
            string validationId,
            CglSemanticValidationShadowScope scope,
            string candidateId,
            string semanticIrId,
            IEnumerable<string> semanticIrIds,
            string candidateSetId,
            string inputId,
            string interactionId,
            string analysisId,
            string validatorVersion,
            SemanticValidationStatus status,
            DateTime checkedAtUtc,
            DateTime registeredAtUtc,
            IEnumerable<string> failureCodes,
            IEnumerable<string> invalidFields,
            bool requiresClarification,
            string diagnosticMessage,
            SemanticValidationRecord candidateRecord)
        {
            ValidationId = Text(validationId);
            Scope = scope;
            CandidateId = Text(candidateId);
            SemanticIrId = Text(semanticIrId);
            SemanticIrIds = ReadOnlyCopy(semanticIrIds);
            CandidateSetId = Text(candidateSetId);
            InputId = Text(inputId);
            InteractionId = Text(interactionId);
            AnalysisId = Text(analysisId);
            ValidatorVersion = Text(validatorVersion);
            Status = status;
            CheckedAtUtc = NormalizeUtc(checkedAtUtc);
            RegisteredAtUtc = NormalizeUtc(registeredAtUtc);
            FailureCodes = ReadOnlyCopy(failureCodes);
            InvalidFields = ReadOnlyCopy(invalidFields);
            RequiresClarification = requiresClarification;
            DiagnosticMessage = Text(diagnosticMessage);
            CandidateRecord = CopyRecord(candidateRecord);
        }

        public static CglSemanticValidationShadowSnapshot FromCandidate(
            SemanticCandidateSet candidateSet,
            SemanticValidationRecord validationRecord,
            string validatorVersion)
        {
            if (candidateSet == null || validationRecord == null)
                return null;

            return new CglSemanticValidationShadowSnapshot(
                validationRecord.ValidationId,
                CglSemanticValidationShadowScope.Candidate,
                validationRecord.CandidateId,
                FindSemanticIrId(
                    candidateSet,
                    validationRecord.CandidateId
                ),
                GetSemanticIrIds(candidateSet),
                validationRecord.CandidateSetId,
                candidateSet.InputId,
                candidateSet.InteractionId,
                candidateSet.AnalysisId,
                validatorVersion,
                validationRecord.Status,
                validationRecord.CheckedAtUtc,
                default(DateTime),
                validationRecord.FailureCodes,
                validationRecord.InvalidFields,
                validationRecord.RequiresClarification,
                validationRecord.DiagnosticMessage,
                validationRecord
            );
        }

        public static CglSemanticValidationShadowSnapshot FromCandidateSet(
            SemanticCandidateSet candidateSet,
            CglSemanticCandidateSetValidationResult setResult,
            CglSemanticValidationContext context,
            string validatorVersion)
        {
            if (candidateSet == null ||
                setResult == null ||
                context == null)
            {
                return null;
            }

            return new CglSemanticValidationShadowSnapshot(
                context.ValidationId,
                CglSemanticValidationShadowScope.CandidateSet,
                string.Empty,
                string.Empty,
                GetSemanticIrIds(candidateSet),
                candidateSet.CandidateSetId,
                candidateSet.InputId,
                candidateSet.InteractionId,
                candidateSet.AnalysisId,
                validatorVersion,
                setResult.Status,
                context.CheckedAtUtc,
                default(DateTime),
                setResult.FailureCodes,
                setResult.InvalidFields,
                setResult.RequiresClarification,
                setResult.DiagnosticMessage,
                null
            );
        }

        internal CglSemanticValidationShadowSnapshot CopyForStore(
            DateTime registeredAtUtc)
        {
            return new CglSemanticValidationShadowSnapshot(
                ValidationId,
                Scope,
                CandidateId,
                SemanticIrId,
                SemanticIrIds,
                CandidateSetId,
                InputId,
                InteractionId,
                AnalysisId,
                ValidatorVersion,
                Status,
                CheckedAtUtc,
                registeredAtUtc,
                FailureCodes,
                InvalidFields,
                RequiresClarification,
                DiagnosticMessage,
                CandidateRecord
            );
        }

        internal CglSemanticValidationShadowSnapshot DefensiveCopy()
        {
            return CopyForStore(RegisteredAtUtc);
        }

        private static SemanticValidationRecord CopyRecord(
            SemanticValidationRecord source)
        {
            if (source == null)
                return null;

            return new SemanticValidationRecord(
                source.ValidationId,
                source.CandidateId,
                source.CandidateSetId,
                source.Status,
                source.CheckedAtUtc,
                source.FailureCodes,
                source.InvalidFields,
                source.RequiresClarification,
                source.DiagnosticMessage
            );
        }

        private static string FindSemanticIrId(
            SemanticCandidateSet candidateSet,
            string candidateId)
        {
            if (candidateSet == null ||
                string.IsNullOrWhiteSpace(candidateId))
            {
                return string.Empty;
            }

            for (int i = 0; i < candidateSet.Candidates.Count; i++)
            {
                SemanticIrCandidate candidate =
                    candidateSet.Candidates[i];

                if (candidate != null &&
                    string.Equals(
                        candidate.CandidateId,
                        candidateId,
                        StringComparison.Ordinal))
                {
                    return candidate.SemanticIrId;
                }
            }

            return string.Empty;
        }

        private static IReadOnlyList<string> GetSemanticIrIds(
            SemanticCandidateSet candidateSet)
        {
            List<string> semanticIrIds = new List<string>();

            if (candidateSet != null)
            {
                for (
                    int i = 0;
                    i < candidateSet.Candidates.Count;
                    i++)
                {
                    SemanticIrCandidate candidate =
                        candidateSet.Candidates[i];

                    if (candidate != null)
                        semanticIrIds.Add(candidate.SemanticIrId);
                }
            }

            return new ReadOnlyCollection<string>(semanticIrIds);
        }

        private static IReadOnlyList<string> ReadOnlyCopy(
            IEnumerable<string> source)
        {
            return new ReadOnlyCollection<string>(
                source != null
                    ? new List<string>(source)
                    : new List<string>()
            );
        }

        private static string Text(string value)
        {
            return value ?? string.Empty;
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            if (value == default(DateTime) ||
                value.Kind == DateTimeKind.Utc)
            {
                return value;
            }

            if (value.Kind == DateTimeKind.Local)
                return value.ToUniversalTime();

            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }
    }

    /// <summary>
    /// Bounded, non-consuming, process-local store for validation snapshots.
    /// It is not a Candidate store, database, selection queue, or execution
    /// authorization source.
    /// </summary>
    public static class CglSemanticValidationShadowStore
    {
        public const int MaximumStoredCount = 1024;
        public static readonly TimeSpan EntryLifetime =
            TimeSpan.FromMinutes(5);

        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<
            string,
            CglSemanticValidationShadowSnapshot> Entries =
                new Dictionary<
                    string,
                    CglSemanticValidationShadowSnapshot>(
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

        public static CglSemanticValidationStoreDiagnosticsSnapshot
            GetDiagnosticsSnapshot()
        {
            lock (SyncRoot)
            {
                DateTime now = DateTime.UtcNow;
                int removed = PurgeExpiredUnsafe(now);

                return
                    new CglSemanticValidationStoreDiagnosticsSnapshot(
                        now,
                        Entries.Count,
                        EntryLifetime,
                        MaximumStoredCount,
                        removed
                    );
            }
        }

        public static CglSemanticValidationShadowRegisterResult Register(
            CglSemanticValidationShadowSnapshot snapshot)
        {
            if (!IsValid(snapshot))
            {
                return
                    CglSemanticValidationShadowRegisterResult.Invalid;
            }

            lock (SyncRoot)
            {
                DateTime now = DateTime.UtcNow;
                PurgeExpiredUnsafe(now);

                if (Entries.ContainsKey(snapshot.ValidationId))
                {
                    return CglSemanticValidationShadowRegisterResult
                        .Duplicate;
                }

                if (Entries.Count >= MaximumStoredCount)
                {
                    return CglSemanticValidationShadowRegisterResult
                        .CapacityRejected;
                }

                Entries.Add(
                    snapshot.ValidationId,
                    snapshot.CopyForStore(now)
                );
                return CglSemanticValidationShadowRegisterResult.Stored;
            }
        }

        public static bool TryGetByValidationId(
            string validationId,
            out CglSemanticValidationShadowSnapshot snapshot)
        {
            snapshot = null;
            if (string.IsNullOrWhiteSpace(validationId))
                return false;

            lock (SyncRoot)
            {
                PurgeExpiredUnsafe(DateTime.UtcNow);

                CglSemanticValidationShadowSnapshot stored;
                if (!Entries.TryGetValue(
                        validationId.Trim(),
                        out stored))
                {
                    return false;
                }

                snapshot = stored.DefensiveCopy();
                return true;
            }
        }

        public static IReadOnlyList<CglSemanticValidationShadowSnapshot>
            GetByCandidateId(string candidateId)
        {
            return GetMatching(
                snapshot =>
                    snapshot.Scope ==
                        CglSemanticValidationShadowScope.Candidate &&
                    string.Equals(
                        snapshot.CandidateId,
                        candidateId,
                        StringComparison.Ordinal)
            );
        }

        public static IReadOnlyList<CglSemanticValidationShadowSnapshot>
            GetByCandidateSetId(string candidateSetId)
        {
            return GetMatching(
                snapshot =>
                    string.Equals(
                        snapshot.CandidateSetId,
                        candidateSetId,
                        StringComparison.Ordinal)
            );
        }

        public static IReadOnlyList<CglSemanticValidationShadowSnapshot>
            GetByInteractionId(string interactionId)
        {
            return GetMatching(
                snapshot =>
                    string.Equals(
                        snapshot.InteractionId,
                        interactionId,
                        StringComparison.Ordinal)
            );
        }

        public static int RemoveExpired(DateTime nowUtc)
        {
            lock (SyncRoot)
            {
                return PurgeExpiredUnsafe(NormalizeUtc(nowUtc));
            }
        }

        /// <summary>
        /// Test-only reset. Runtime code relies on TTL and capacity.
        /// </summary>
        public static void ClearForTests()
        {
            lock (SyncRoot)
                Entries.Clear();
        }

        private static IReadOnlyList<
            CglSemanticValidationShadowSnapshot> GetMatching(
                Func<CglSemanticValidationShadowSnapshot, bool> predicate)
        {
            if (predicate == null)
            {
                return new ReadOnlyCollection<
                    CglSemanticValidationShadowSnapshot>(
                        new List<
                            CglSemanticValidationShadowSnapshot>()
                );
            }

            lock (SyncRoot)
            {
                PurgeExpiredUnsafe(DateTime.UtcNow);
                List<CglSemanticValidationShadowSnapshot> matches =
                    new List<CglSemanticValidationShadowSnapshot>();

                foreach (
                    KeyValuePair<
                        string,
                        CglSemanticValidationShadowSnapshot> pair
                    in Entries)
                {
                    if (predicate(pair.Value))
                        matches.Add(pair.Value);
                }

                matches.Sort(CompareSnapshots);
                List<CglSemanticValidationShadowSnapshot> result =
                    new List<CglSemanticValidationShadowSnapshot>(
                        matches.Count);

                for (int i = 0; i < matches.Count; i++)
                    result.Add(matches[i].DefensiveCopy());

                return new ReadOnlyCollection<
                    CglSemanticValidationShadowSnapshot>(result);
            }
        }

        private static bool IsValid(
            CglSemanticValidationShadowSnapshot snapshot)
        {
            if (snapshot == null ||
                string.IsNullOrWhiteSpace(snapshot.ValidationId) ||
                string.IsNullOrWhiteSpace(snapshot.CandidateSetId) ||
                string.IsNullOrWhiteSpace(snapshot.InputId) ||
                string.IsNullOrWhiteSpace(snapshot.InteractionId) ||
                string.IsNullOrWhiteSpace(snapshot.AnalysisId) ||
                string.IsNullOrWhiteSpace(snapshot.ValidatorVersion) ||
                snapshot.CheckedAtUtc == default(DateTime) ||
                !Enum.IsDefined(typeof(SemanticValidationStatus),
                    snapshot.Status) ||
                snapshot.Status == SemanticValidationStatus.NotChecked ||
                !Enum.IsDefined(
                    typeof(CglSemanticValidationShadowScope),
                    snapshot.Scope) ||
                snapshot.Scope ==
                    CglSemanticValidationShadowScope.Unknown)
            {
                return false;
            }

            if (snapshot.Scope ==
                CglSemanticValidationShadowScope.Candidate)
            {
                SemanticValidationRecord record =
                    snapshot.CandidateRecord;

                return
                    !string.IsNullOrWhiteSpace(snapshot.CandidateId) &&
                    record != null &&
                    string.Equals(
                        snapshot.ValidationId,
                        record.ValidationId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        snapshot.CandidateId,
                        record.CandidateId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        snapshot.CandidateSetId,
                        record.CandidateSetId,
                        StringComparison.Ordinal) &&
                    snapshot.Status == record.Status &&
                    snapshot.CheckedAtUtc == record.CheckedAtUtc;
            }

            return
                snapshot.Scope ==
                    CglSemanticValidationShadowScope.CandidateSet &&
                string.IsNullOrEmpty(snapshot.CandidateId) &&
                snapshot.CandidateRecord == null;
        }

        private static int PurgeExpiredUnsafe(DateTime nowUtc)
        {
            List<string> expired = new List<string>();

            foreach (
                KeyValuePair<
                    string,
                    CglSemanticValidationShadowSnapshot> pair
                in Entries)
            {
                if (nowUtc - pair.Value.RegisteredAtUtc >
                    EntryLifetime)
                {
                    expired.Add(pair.Key);
                }
            }

            for (int i = 0; i < expired.Count; i++)
                Entries.Remove(expired[i]);

            return expired.Count;
        }

        private static int CompareSnapshots(
            CglSemanticValidationShadowSnapshot left,
            CglSemanticValidationShadowSnapshot right)
        {
            int checkedComparison =
                left.CheckedAtUtc.CompareTo(right.CheckedAtUtc);
            if (checkedComparison != 0)
                return checkedComparison;

            return string.Compare(
                left.ValidationId,
                right.ValidationId,
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
    }
}
