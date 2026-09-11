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
    public interface IObservedObjectMemoryContextProvider
    {
        bool HasCurrentContext { get; }
        ObservedObjectMemoryContext CurrentContext { get; }

        event Action<ObservedObjectMemoryContext> ContextChanged;
        event Action<ObservedObjectMemoryContext, string> ContextCleared;
    }
}
