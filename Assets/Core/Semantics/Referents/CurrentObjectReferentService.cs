// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

namespace SalieriAI.Core.Semantics.Referents
{
    /// <summary>Sole owner of the current immutable object referent.</summary>
    public sealed class CurrentObjectReferentService :
        ICurrentObjectReferentProvider
    {
        public bool HasCurrentReferent => CurrentReferent != null;
        public CurrentObjectReferent CurrentReferent { get; private set; }

        public event Action<CurrentObjectReferent> ReferentChanged;
        public event Action<CurrentObjectReferent, string> ReferentCleared;

        public bool ReplaceCurrent(CurrentObjectReferent next)
        {
            if (next == null)
                return Clear("Replacement referent is null.");

            CurrentObjectReferent previous = CurrentReferent;
            bool changed = previous == null ||
                !previous.IsSemanticallyEquivalentTo(next);

            if (!changed)
                return false;

            CurrentReferent = next;
            ReferentChanged?.Invoke(next);
            return true;
        }

        public bool Clear(string reason)
        {
            CurrentObjectReferent previous = CurrentReferent;
            if (previous == null)
                return false;

            CurrentReferent = null;
            ReferentCleared?.Invoke(previous, reason ?? string.Empty);
            return true;
        }
    }
}
