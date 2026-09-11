// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;

using SalieriAI.Core.Language.Contracts;

namespace SalieriAI.Core.Language.Cgl
{
    /// <summary>
    /// Bounded in-memory handoff from Phase 2A shadow analysis to Phase 2B comparison.
    /// Completed interaction IDs are retained briefly to suppress duplicate comparisons.
    /// </summary>
    internal static class CglShadowResultStore
    {
        private const int MaximumPendingCount = 256;
        private const int MaximumCompletedCount = 512;
        private static readonly TimeSpan EntryLifetime = TimeSpan.FromMinutes(5);
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, PendingEntry> Pending =
            new Dictionary<string, PendingEntry>(StringComparer.Ordinal);
        private static readonly Dictionary<string, DateTime> Completed =
            new Dictionary<string, DateTime>(StringComparer.Ordinal);

        public static void Register(CglInterpretation interpretation)
        {
            if (interpretation == null ||
                string.IsNullOrWhiteSpace(interpretation.InteractionId))
            {
                return;
            }

            lock (SyncRoot)
            {
                DateTime now = DateTime.UtcNow;
                PurgeExpired(now);

                Pending[interpretation.InteractionId] =
                    new PendingEntry(interpretation, now);

                TrimOldestPending();
            }
        }

        public static CglShadowTakeResult TryTake(
            string interactionId,
            out CglInterpretation interpretation)
        {
            interpretation = null;

            if (string.IsNullOrWhiteSpace(interactionId))
                return CglShadowTakeResult.Missing;

            lock (SyncRoot)
            {
                DateTime now = DateTime.UtcNow;
                PurgeExpired(now);

                if (Completed.ContainsKey(interactionId))
                    return CglShadowTakeResult.Duplicate;

                PendingEntry entry;
                if (!Pending.TryGetValue(interactionId, out entry))
                {
                    Completed[interactionId] = now;
                    TrimOldestCompleted();
                    return CglShadowTakeResult.Missing;
                }

                Pending.Remove(interactionId);
                Completed[interactionId] = now;
                TrimOldestCompleted();
                interpretation = entry.Interpretation;
                return CglShadowTakeResult.Found;
            }
        }

        private static void PurgeExpired(DateTime now)
        {
            List<string> expired = new List<string>();

            foreach (KeyValuePair<string, PendingEntry> pair in Pending)
            {
                if (now - pair.Value.RegisteredAtUtc > EntryLifetime)
                    expired.Add(pair.Key);
            }

            for (int i = 0; i < expired.Count; i++)
                Pending.Remove(expired[i]);

            expired.Clear();

            foreach (KeyValuePair<string, DateTime> pair in Completed)
            {
                if (now - pair.Value > EntryLifetime)
                    expired.Add(pair.Key);
            }

            for (int i = 0; i < expired.Count; i++)
                Completed.Remove(expired[i]);
        }

        private static void TrimOldestPending()
        {
            while (Pending.Count > MaximumPendingCount)
            {
                string oldestKey = null;
                DateTime oldestTime = DateTime.MaxValue;

                foreach (KeyValuePair<string, PendingEntry> pair in Pending)
                {
                    if (pair.Value.RegisteredAtUtc < oldestTime)
                    {
                        oldestKey = pair.Key;
                        oldestTime = pair.Value.RegisteredAtUtc;
                    }
                }

                if (oldestKey == null)
                    return;

                Pending.Remove(oldestKey);
            }
        }

        private static void TrimOldestCompleted()
        {
            while (Completed.Count > MaximumCompletedCount)
            {
                string oldestKey = null;
                DateTime oldestTime = DateTime.MaxValue;

                foreach (KeyValuePair<string, DateTime> pair in Completed)
                {
                    if (pair.Value < oldestTime)
                    {
                        oldestKey = pair.Key;
                        oldestTime = pair.Value;
                    }
                }

                if (oldestKey == null)
                    return;

                Completed.Remove(oldestKey);
            }
        }

        private sealed class PendingEntry
        {
            public CglInterpretation Interpretation { get; }
            public DateTime RegisteredAtUtc { get; }

            public PendingEntry(
                CglInterpretation interpretation,
                DateTime registeredAtUtc)
            {
                Interpretation = interpretation;
                RegisteredAtUtc = registeredAtUtc;
            }
        }
    }

    internal enum CglShadowTakeResult
    {
        Found,
        Missing,
        Duplicate
    }
}
