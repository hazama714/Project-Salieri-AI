// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Execution.Orchestration.Contracts
{
    /// <summary>
    /// Explicit cancellation and interruption capability/evidence for one
    /// delegated physical body action. Support, accepted control effect, and
    /// physically verified stop are deliberately separate facts.
    /// </summary>
    public sealed class ExecutionBodyActionCancellationCapability
    {
        public bool SupportsCancel { get; }
        public bool SupportsInterrupt { get; }
        public bool CancelEffectConfirmed { get; }
        public bool PhysicalStopVerified { get; }

        public ExecutionBodyActionCancellationCapability(
            bool supportsCancel,
            bool supportsInterrupt,
            bool cancelEffectConfirmed,
            bool physicalStopVerified)
        {
            SupportsCancel = supportsCancel;
            SupportsInterrupt = supportsInterrupt;
            CancelEffectConfirmed = cancelEffectConfirmed;
            PhysicalStopVerified = physicalStopVerified;
        }
    }

    /// <summary>
    /// Common evidence boundary for a physical body action delegated to a
    /// lower runtime. It owns no ActionId, resource, timeout, retry, policy,
    /// lifecycle ID, authority, or physical execution behavior.
    ///
    /// Request/dispatch observations do not imply completion. Verified
    /// completion is admissible only when explicit physical feedback support
    /// has been declared by the action-specific runtime contract.
    /// </summary>
    public sealed class ExecutionDelegatedPhysicalBodyActionContract
    {
        public bool AllowsUnverifiedCompletion { get; }
        public bool SupportsPhysicalCompletionFeedback { get; }
        public ExecutionBodyActionCancellationCapability
            CancellationCapability { get; }

        public bool SupportsCancel =>
            CancellationCapability != null &&
            CancellationCapability.SupportsCancel;
        public bool SupportsInterrupt =>
            CancellationCapability != null &&
            CancellationCapability.SupportsInterrupt;
        public bool CancelEffectConfirmed =>
            CancellationCapability != null &&
            CancellationCapability.CancelEffectConfirmed;
        public bool PhysicalStopVerified =>
            CancellationCapability != null &&
            CancellationCapability.PhysicalStopVerified;

        public ExecutionDelegatedPhysicalBodyActionContract(
            bool allowsUnverifiedCompletion,
            bool supportsPhysicalCompletionFeedback,
            ExecutionBodyActionCancellationCapability
                cancellationCapability)
        {
            AllowsUnverifiedCompletion = allowsUnverifiedCompletion;
            SupportsPhysicalCompletionFeedback =
                supportsPhysicalCompletionFeedback;
            CancellationCapability = cancellationCapability;
        }
    }
}
