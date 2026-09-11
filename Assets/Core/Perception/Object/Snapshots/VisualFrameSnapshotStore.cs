// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;

namespace SalieriAI.Core.Perception.VisualSnapshots
{
    /// <summary>
    /// SessionId + SourceFrameIdだけでexact lookupするbounded in-memory store。
    /// latest/nearest/Class/Track fallbackは行わない。
    /// </summary>
    public sealed class VisualFrameSnapshotStore
    {
        private readonly object gate = new object();
        private readonly int maximumSnapshotCount;
        private readonly TimeSpan timeToLive;
        private readonly LinkedList<Entry> order = new LinkedList<Entry>();
        private readonly Dictionary<string, LinkedListNode<Entry>> entries =
            new Dictionary<string, LinkedListNode<Entry>>(StringComparer.Ordinal);

        public int MaximumSnapshotCount => maximumSnapshotCount;
        public TimeSpan TimeToLive => timeToLive;

        public int Count
        {
            get
            {
                lock (gate)
                    return entries.Count;
            }
        }

        public VisualFrameSnapshotStore(
            int maximumSnapshotCount,
            TimeSpan timeToLive)
        {
            if (maximumSnapshotCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumSnapshotCount));
            if (timeToLive <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeToLive));

            this.maximumSnapshotCount = maximumSnapshotCount;
            this.timeToLive = timeToLive;
        }

        public bool TryAdd(
            VisualFrameSnapshot snapshot,
            DateTime storedAtUtc,
            out string error)
        {
            error = string.Empty;
            if (snapshot == null || !snapshot.IsValid)
            {
                error = "VisualFrameSnapshot is invalid.";
                return false;
            }
            if (!TryUtc(storedAtUtc, out DateTime normalizedUtc))
            {
                error = "StoredAtUtc is invalid.";
                return false;
            }

            string key = Key(snapshot.SessionId, snapshot.SourceFrameId);
            lock (gate)
            {
                RemoveExpired(normalizedUtc);
                if (entries.TryGetValue(key, out LinkedListNode<Entry> existing))
                {
                    order.Remove(existing);
                    entries.Remove(key);
                }

                var entry = new Entry(key, snapshot, normalizedUtc);
                LinkedListNode<Entry> node = order.AddLast(entry);
                entries.Add(key, node);
                while (entries.Count > maximumSnapshotCount)
                    RemoveFirst();
            }
            return true;
        }

        public bool TryGetExact(
            string sessionId,
            long sourceFrameId,
            DateTime evaluatedAtUtc,
            out VisualFrameSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            error = string.Empty;
            string normalizedSession = Normalize(sessionId);
            if (normalizedSession.Length == 0 || sourceFrameId <= 0)
            {
                error = "Visual snapshot correlation key is invalid.";
                return false;
            }
            if (!TryUtc(evaluatedAtUtc, out DateTime normalizedUtc))
            {
                error = "EvaluatedAtUtc is invalid.";
                return false;
            }

            lock (gate)
            {
                RemoveExpired(normalizedUtc);
                if (!entries.TryGetValue(
                        Key(normalizedSession, sourceFrameId),
                        out LinkedListNode<Entry> node))
                {
                    error = "Exact visual SourceFrame snapshot was not found.";
                    return false;
                }

                snapshot = node.Value.Snapshot;
                return true;
            }
        }

        public void Clear()
        {
            lock (gate)
            {
                entries.Clear();
                order.Clear();
            }
        }

        private void RemoveExpired(DateTime evaluatedAtUtc)
        {
            while (order.First != null &&
                   evaluatedAtUtc >= order.First.Value.StoredAtUtc + timeToLive)
            {
                RemoveFirst();
            }
        }

        private void RemoveFirst()
        {
            LinkedListNode<Entry> first = order.First;
            if (first == null)
                return;
            order.RemoveFirst();
            entries.Remove(first.Value.Key);
        }

        private static string Key(string sessionId, long sourceFrameId)
        {
            return Normalize(sessionId) + "\u001f" + sourceFrameId;
        }

        private static string Normalize(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }

        private static bool TryUtc(DateTime value, out DateTime utc)
        {
            utc = default(DateTime);
            if (value == default(DateTime))
                return false;
            utc = value.Kind == DateTimeKind.Utc
                ? value
                : value.ToUniversalTime();
            return true;
        }

        private sealed class Entry
        {
            internal readonly string Key;
            internal readonly VisualFrameSnapshot Snapshot;
            internal readonly DateTime StoredAtUtc;

            internal Entry(
                string key,
                VisualFrameSnapshot snapshot,
                DateTime storedAtUtc)
            {
                Key = key;
                Snapshot = snapshot;
                StoredAtUtc = storedAtUtc;
            }
        }
    }
}
