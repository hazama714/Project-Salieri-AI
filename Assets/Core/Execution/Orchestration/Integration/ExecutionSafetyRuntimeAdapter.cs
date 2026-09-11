// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

/* Project Salieri AI - Apache-2.0 */
using System;
using SalieriAI.Core.Execution.Orchestration.Adapters;
using SalieriAI.Core.Execution.Orchestration.Contracts;
using SalieriAI.Core.Runtime;

namespace SalieriAI.Core.Execution.Orchestration.Integration
{
    /// <summary>Pure observation adapter; it never executes safety work.</summary>
    public sealed class ExecutionSafetyRuntimeAdapter
    {
        public const string AdapterVersion = "execution-safety-runtime-adapter-3c6.1";

        public SafetyRuntimeAdapterResult Observe(
            SafetyControlRequest request, SafetyControlRuntimeFact fact)
        {
            string failure = Validate(request, fact);
            if (failure.Length > 0)
                return Result(fact, SafetyRuntimeAdapterStatus.RejectedInvalid,
                    null, false, false, false, failure);

            bool accepted = fact.Lifecycle == SafetyRuntimeLifecycle.RequestAccepted ||
                fact.Lifecycle == SafetyRuntimeLifecycle.ControlDispatched ||
                fact.Lifecycle == SafetyRuntimeLifecycle.EffectConfirmed;
            bool effect = fact.Lifecycle == SafetyRuntimeLifecycle.EffectConfirmed;
            bool verified = effect && fact.VerificationLevel ==
                SafetyRuntimeVerificationLevel.EffectVerified;

            // The existing Phase 3C-1 result cannot represent observed or
            // accepted-only states without falsely calling them dispatched.
            // Create it only for an actually observed dispatch/effect/terminal.
            SafetyControlResult result = null;
            switch (fact.Lifecycle)
            {
                case SafetyRuntimeLifecycle.ControlDispatched:
                    result = new SafetyControlResult(request.ControlRequestId,
                        request.PlanId, true, false, fact.OccurredAtUtc,
                        string.Empty);
                    break;
                case SafetyRuntimeLifecycle.EffectConfirmed:
                    result = new SafetyControlResult(request.ControlRequestId,
                        request.PlanId, true, true, fact.OccurredAtUtc,
                        string.Empty);
                    break;
                case SafetyRuntimeLifecycle.Rejected:
                case SafetyRuntimeLifecycle.Failed:
                case SafetyRuntimeLifecycle.Interrupted:
                    result = new SafetyControlResult(request.ControlRequestId,
                        request.PlanId, false, false, fact.OccurredAtUtc,
                        fact.FailureReason);
                    break;
            }

            return Result(fact, SafetyRuntimeAdapterStatus.Observed, result,
                accepted, effect, verified, string.Empty);
        }

        private static string Validate(
            SafetyControlRequest request, SafetyControlRuntimeFact fact)
        {
            if (request == null || fact == null)
                return "SAFETY_RUNTIME_INPUT_NULL";
            if (string.IsNullOrWhiteSpace(request.ControlRequestId) ||
                string.IsNullOrWhiteSpace(request.PlanId) ||
                string.IsNullOrWhiteSpace(fact.RuntimeControlToken) ||
                fact.RuntimeGeneration <= 0 ||
                fact.OccurredAtUtc == default(DateTime) ||
                !Enum.IsDefined(typeof(SafetyRuntimeLifecycle), fact.Lifecycle) ||
                !Enum.IsDefined(typeof(SafetyRuntimeActualScope), fact.RuntimeScope) ||
                !Enum.IsDefined(typeof(SafetyRuntimeVerificationLevel), fact.VerificationLevel))
                return "SAFETY_RUNTIME_INPUT_INVALID";
            if (request.ControlKind != fact.ControlKind)
                return "SAFETY_CONTROL_KIND_MISMATCH";

            switch (fact.Lifecycle)
            {
                case SafetyRuntimeLifecycle.RequestObserved:
                case SafetyRuntimeLifecycle.RequestAccepted:
                    return fact.VerificationLevel == SafetyRuntimeVerificationLevel.RequestOnly
                        ? string.Empty : "SAFETY_VERIFICATION_INVALID";
                case SafetyRuntimeLifecycle.ControlDispatched:
                    return fact.VerificationLevel == SafetyRuntimeVerificationLevel.DispatchConfirmed ||
                        fact.VerificationLevel == SafetyRuntimeVerificationLevel.EffectUnverified
                        ? string.Empty : "SAFETY_DISPATCH_VERIFICATION_INVALID";
                case SafetyRuntimeLifecycle.EffectConfirmed:
                    return fact.VerificationLevel == SafetyRuntimeVerificationLevel.EffectUnverified ||
                        fact.VerificationLevel == SafetyRuntimeVerificationLevel.EffectVerified
                        ? string.Empty : "SAFETY_EFFECT_VERIFICATION_INVALID";
                case SafetyRuntimeLifecycle.Rejected:
                case SafetyRuntimeLifecycle.Failed:
                case SafetyRuntimeLifecycle.Interrupted:
                    return string.IsNullOrWhiteSpace(fact.FailureReason)
                        ? "SAFETY_FAILURE_REASON_MISSING" : string.Empty;
                default:
                    return "SAFETY_RUNTIME_LIFECYCLE_UNSUPPORTED";
            }
        }

        private static SafetyRuntimeAdapterResult Result(
            SafetyControlRuntimeFact fact, SafetyRuntimeAdapterStatus status,
            SafetyControlResult result, bool accepted, bool effect,
            bool verified, string failure)
        {
            return new SafetyRuntimeAdapterResult(status,
                fact != null ? fact.Lifecycle : SafetyRuntimeLifecycle.RequestObserved,
                result, accepted, effect, verified, failure);
        }
    }
}
