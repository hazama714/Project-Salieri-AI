// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System.Collections.Generic;
using System.Collections.ObjectModel;

using SalieriAI.Core.Execution.Orchestration.Runtime;

namespace SalieriAI.Core.Execution.Orchestration.Reduction
{
    public sealed class ExecutionCoordinatorReductionResult
    {
        public ExecutionCoordinatorRuntimeState State { get; }
        public IReadOnlyList<ExecutionCoordinatorEffectIntent>
            EffectIntents { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public ExecutionCoordinatorEventDisposition EventDisposition
        {
            get;
        }

        public ExecutionCoordinatorReductionResult(
            ExecutionCoordinatorRuntimeState state,
            IEnumerable<ExecutionCoordinatorEffectIntent> effectIntents,
            IEnumerable<string> diagnostics,
            ExecutionCoordinatorEventDisposition eventDisposition)
        {
            State = state;
            EffectIntents = Copy(effectIntents);
            Diagnostics = Copy(diagnostics);
            EventDisposition = eventDisposition;
        }

        private static IReadOnlyList<T> Copy<T>(IEnumerable<T> source)
        {
            return new ReadOnlyCollection<T>(
                source != null ? new List<T>(source) : new List<T>()
            );
        }
    }
}
