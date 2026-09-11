// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;

using SalieriAI.Core.Execution.Orchestration.Contracts;

namespace SalieriAI.Core.Execution.Orchestration.Integration
{
    public enum ExecutionActionDescriptorLookupStatus
    {
        Found = 0,
        NotRegistered = 1
    }

    public sealed class ExecutionActionDescriptorLookupResult
    {
        public ExecutionActionDescriptorLookupStatus Status { get; }
        public ExecutionActionDescriptor Descriptor { get; }
        public bool IsFound =>
            Status == ExecutionActionDescriptorLookupStatus.Found &&
            Descriptor != null;

        internal ExecutionActionDescriptorLookupResult(
            ExecutionActionDescriptorLookupStatus status,
            ExecutionActionDescriptor descriptor)
        {
            Status = status;
            Descriptor = descriptor;
        }
    }

    public enum ExecutionActionDescriptorRegistrationStatus
    {
        Registered = 0,
        DuplicateActionId = 1,
        InvalidDescriptor = 2
    }

    public sealed class ExecutionActionDescriptorRegistrationResult
    {
        public ExecutionActionDescriptorRegistrationStatus Status { get; }
        public string FailureReason { get; }
        public bool IsRegistered =>
            Status == ExecutionActionDescriptorRegistrationStatus.Registered;

        internal ExecutionActionDescriptorRegistrationResult(
            ExecutionActionDescriptorRegistrationStatus status,
            string failureReason)
        {
            Status = status;
            FailureReason = failureReason ?? string.Empty;
        }
    }

    /// <summary>
    /// Deterministic ActionId to ExecutionActionDescriptor lookup.
    ///
    /// The registry contains no inferred defaults and has no Production
    /// registrations until an action has a complete, evidence-backed
    /// descriptor. Lookup performs no runtime query or execution side effect.
    /// </summary>
    public sealed class ExecutionActionDescriptorRegistry
    {
        private readonly Dictionary<string, ExecutionActionDescriptor>
            descriptors =
                new Dictionary<string, ExecutionActionDescriptor>(
                    StringComparer.Ordinal);

        public int Count => descriptors.Count;

        public ExecutionActionDescriptorRegistrationResult Register(
            ExecutionActionDescriptor descriptor)
        {
            string failureReason;
            if (!TryValidate(descriptor, out failureReason))
            {
                return new ExecutionActionDescriptorRegistrationResult(
                    ExecutionActionDescriptorRegistrationStatus
                        .InvalidDescriptor,
                    failureReason);
            }

            if (descriptors.ContainsKey(descriptor.ActionId))
            {
                return new ExecutionActionDescriptorRegistrationResult(
                    ExecutionActionDescriptorRegistrationStatus
                        .DuplicateActionId,
                    "ActionId is already registered.");
            }

            descriptors.Add(descriptor.ActionId, descriptor);
            return new ExecutionActionDescriptorRegistrationResult(
                ExecutionActionDescriptorRegistrationStatus.Registered,
                string.Empty);
        }

        public ExecutionActionDescriptorLookupResult Lookup(string actionId)
        {
            ExecutionActionDescriptor descriptor;
            string key = actionId ?? string.Empty;
            if (descriptors.TryGetValue(key, out descriptor))
            {
                return new ExecutionActionDescriptorLookupResult(
                    ExecutionActionDescriptorLookupStatus.Found,
                    descriptor);
            }

            return new ExecutionActionDescriptorLookupResult(
                ExecutionActionDescriptorLookupStatus.NotRegistered,
                null);
        }

        private static bool TryValidate(
            ExecutionActionDescriptor descriptor,
            out string failureReason)
        {
            if (descriptor == null)
                return Fail("Descriptor is null.", out failureReason);
            if (string.IsNullOrWhiteSpace(descriptor.ActionId))
                return Fail("ActionId is required.", out failureReason);
            if (!Defined(descriptor.Domain) ||
                descriptor.Domain == ExecutionDomain.None)
                return Fail("ExecutionDomain is invalid.", out failureReason);
            if (!Defined(descriptor.StepType) ||
                descriptor.StepType == ExecutionStepType.None)
                return Fail("StepType is invalid.", out failureReason);
            if (!Defined(descriptor.StepRole))
                return Fail("StepRole is invalid.", out failureReason);
            if (!Defined(descriptor.PlanType) ||
                descriptor.PlanType == ExecutionPlanType.None)
                return Fail("PlanType is invalid.", out failureReason);
            if (!Defined(descriptor.PolicyKind))
                return Fail("PolicyKind is invalid.", out failureReason);
            if (descriptor.PolicyVersion == null ||
                string.IsNullOrWhiteSpace(
                    descriptor.PolicyVersion.PolicyId) ||
                descriptor.PolicyVersion.Major < 0 ||
                descriptor.PolicyVersion.Minor < 0)
                return Fail("PolicyVersion is required.", out failureReason);
            if (string.IsNullOrWhiteSpace(
                    descriptor.PayloadSchemaVersion))
                return Fail(
                    "PayloadSchemaVersion is required.",
                    out failureReason);

            if (descriptor.CompletionPolicy == null)
                return Fail("CompletionPolicy is required.", out failureReason);
            if (descriptor.PermissionWaitPolicy == null)
                return Fail(
                    "PermissionWaitPolicy is required.",
                    out failureReason);
            if (descriptor.TimeoutPolicy == null)
                return Fail("TimeoutPolicy is required.", out failureReason);
            if (descriptor.ResourceWaitTimeoutPolicy == null)
                return Fail(
                    "ResourceWaitTimeoutPolicy is required.",
                    out failureReason);
            if (descriptor.RetryPolicy == null)
                return Fail("RetryPolicy is required.", out failureReason);
            if (descriptor.FailurePolicy == null)
                return Fail("FailurePolicy is required.", out failureReason);
            if (descriptor.CancellationPolicy == null)
                return Fail(
                    "CancellationPolicy is required.",
                    out failureReason);
            if (descriptor.SafetyPolicy == null)
                return Fail("SafetyPolicy is required.", out failureReason);

            bool noOpDomain = descriptor.Domain == ExecutionDomain.NoOp;
            bool noOpType = descriptor.StepType == ExecutionStepType.NoOp;
            if (noOpDomain != noOpType)
                return Fail(
                    "NoOp Domain and StepType must be paired.",
                    out failureReason);

            var resourceIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < descriptor.ResourceRequirements.Count; i++)
            {
                ExecutionResourceRequirement requirement =
                    descriptor.ResourceRequirements[i];
                if (requirement == null ||
                    string.IsNullOrWhiteSpace(requirement.ResourceId))
                    return Fail(
                        "Resource requirement is incomplete.",
                        out failureReason);
                if (!Defined(requirement.AccessMode) ||
                    !Defined(requirement.Requiredness) ||
                    !Defined(requirement.LeaseScope) ||
                    !Defined(requirement.ReleasePolicy))
                    return Fail(
                        "Resource requirement contains an invalid value.",
                        out failureReason);
                if (!resourceIds.Add(requirement.ResourceId))
                    return Fail(
                        "ResourceId is duplicated.", out failureReason);
            }

            if (noOpDomain &&
                (descriptor.StepRole != ExecutionNoOpContract.StepRole ||
                 descriptor.ResourceRequirements.Count != 0 ||
                 descriptor.PlanType != ExecutionNoOpContract.PlanType ||
                 descriptor.PolicyKind != ExecutionNoOpContract.PolicyKind ||
                 !ExecutionNoOpContract.MatchesCompletionPolicy(
                     descriptor.CompletionPolicy) ||
                 !ExecutionNoOpContract.MatchesPermissionPolicy(
                     descriptor.PermissionWaitPolicy) ||
                 !ExecutionNoOpContract.MatchesTimeoutPolicy(
                     descriptor.TimeoutPolicy) ||
                 !ExecutionNoOpContract.MatchesResourceWaitPolicy(
                     descriptor.ResourceWaitTimeoutPolicy) ||
                 !ExecutionNoOpContract.MatchesRetryPolicy(
                     descriptor.RetryPolicy) ||
                 !ExecutionNoOpContract.MatchesFailurePolicy(
                     descriptor.FailurePolicy) ||
                 !ExecutionNoOpContract.MatchesCancellationPolicy(
                     descriptor.CancellationPolicy) ||
                 !ExecutionNoOpContract.MatchesSafetyPolicy(
                     descriptor.SafetyPolicy)))
            {
                return Fail(
                    "NoOp descriptor semantics are invalid.",
                    out failureReason);
            }

            if (descriptor.ActionId ==
                    ExecutionLookAroundContract.ActionId &&
                !ExecutionLookAroundContract.MatchesDescriptor(descriptor))
            {
                return Fail(
                    "lookAround descriptor semantics are invalid.",
                    out failureReason);
            }

            failureReason = string.Empty;
            return true;
        }

        private static bool Fail(string reason, out string failureReason)
        {
            failureReason = reason;
            return false;
        }

        private static bool Defined<T>(T value) where T : struct
        {
            return Enum.IsDefined(typeof(T), value);
        }
    }
}
