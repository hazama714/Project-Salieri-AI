// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

namespace SalieriAI.Core.Experience.Observation
{
    /// <summary>
    /// Sole owner of the latest immutable memory assessment snapshot.
    /// </summary>
    public sealed class ObservedObjectMemoryContextService :
        IObservedObjectMemoryContextProvider
    {
        public bool HasCurrentContext => CurrentContext != null;
        public ObservedObjectMemoryContext CurrentContext { get; private set; }

        public event Action<ObservedObjectMemoryContext> ContextChanged;
        public event Action<ObservedObjectMemoryContext, string> ContextCleared;

        public bool ReplaceCurrent(ObservedObjectMemoryContext next)
        {
            if (next == null)
                return Clear("Replacement context is null.");

            ObservedObjectMemoryContext previous = CurrentContext;
            bool changed = previous == null ||
                !previous.IsSemanticallyEquivalentTo(next);
            CurrentContext = next;

            if (changed)
                ContextChanged?.Invoke(next);
            return changed;
        }

        public bool Clear(string reason)
        {
            ObservedObjectMemoryContext previous = CurrentContext;
            if (previous == null)
                return false;

            CurrentContext = null;
            ContextCleared?.Invoke(previous, reason ?? string.Empty);
            return true;
        }
    }
}
