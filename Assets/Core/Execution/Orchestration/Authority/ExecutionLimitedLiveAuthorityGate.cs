// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

/* Project Salieri AI - Apache-2.0 */
using System;
using System.Collections.Generic;
using SalieriAI.Core.Execution.Orchestration.Contracts;
using SalieriAI.Core.Execution.Orchestration.Runtime;

namespace SalieriAI.Core.Execution.Orchestration.Authority
{
    public static class ExecutionAuthorityDenialCodes
    {
        public const string PolicyMissing = "AUTHORITY_POLICY_MISSING";
        public const string PolicyDisabled = "AUTHORITY_POLICY_DISABLED";
        public const string PolicyVersionInvalid = "AUTHORITY_POLICY_VERSION_INVALID";
        public const string RequestInvalid = "AUTHORITY_REQUEST_INVALID";
        public const string DomainUnsupported = "AUTHORITY_DOMAIN_UNSUPPORTED";
        public const string EffectUnsupported = "AUTHORITY_EFFECT_UNSUPPORTED";
        public const string EffectDomainMismatch = "AUTHORITY_EFFECT_DOMAIN_MISMATCH";
        public const string TabooBlocked = "AUTHORITY_TABOO_BLOCKED";
        public const string ModeDenied = "AUTHORITY_MODE_DENIED";
        public const string CapabilityMissing = "AUTHORITY_CAPABILITY_MISSING";
        public const string CapabilityUnsupported = "AUTHORITY_CAPABILITY_UNSUPPORTED";
        public const string WhitelistMismatch = "AUTHORITY_WHITELIST_MISMATCH";
        public const string ScopeMismatch = "AUTHORITY_SCOPE_MISMATCH";
    }

    public static class ExecutionAuthorityIntentDomainMapper
    {
        public static bool IsCompatible(
            ExecutionCoordinatorEffectIntentType effect,
            ExecutionAuthorityDomain domain)
        {
            switch (effect)
            {
                case ExecutionCoordinatorEffectIntentType.None:
                    return domain == ExecutionAuthorityDomain.ResourceProfile;
                case ExecutionCoordinatorEffectIntentType.CheckPermission:
                    return domain == ExecutionAuthorityDomain.Permission;
                case ExecutionCoordinatorEffectIntentType.AcquireResourceLease:
                case ExecutionCoordinatorEffectIntentType.ReleaseResourceLease:
                    return domain == ExecutionAuthorityDomain.LogicalResourceLease;
                case ExecutionCoordinatorEffectIntentType.StartExecutor:
                    return domain == ExecutionAuthorityDomain.Speech ||
                        domain == ExecutionAuthorityDomain.Body;
                case ExecutionCoordinatorEffectIntentType.RequestSafetyPreempt:
                case ExecutionCoordinatorEffectIntentType.RequestExecutorInterrupt:
                case ExecutionCoordinatorEffectIntentType.RequestExecutorCancel:
                    return domain == ExecutionAuthorityDomain.Safety;
                default:
                    return false;
            }
        }

        public static bool TryMap(
            ExecutionCoordinatorEffectIntentType effect,
            ExecutionDomain stepDomain,
            out ExecutionAuthorityDomain authorityDomain)
        {
            authorityDomain = ExecutionAuthorityDomain.Unknown;
            switch (effect)
            {
                case ExecutionCoordinatorEffectIntentType.CheckPermission:
                    authorityDomain = ExecutionAuthorityDomain.Permission;
                    return true;
                case ExecutionCoordinatorEffectIntentType.AcquireResourceLease:
                case ExecutionCoordinatorEffectIntentType.ReleaseResourceLease:
                    authorityDomain = ExecutionAuthorityDomain.LogicalResourceLease;
                    return true;
                case ExecutionCoordinatorEffectIntentType.StartExecutor:
                    if (stepDomain == ExecutionDomain.Speech)
                    {
                        authorityDomain = ExecutionAuthorityDomain.Speech;
                        return true;
                    }
                    if (stepDomain == ExecutionDomain.PhysicalBody ||
                        stepDomain == ExecutionDomain.VirtualBody ||
                        stepDomain == ExecutionDomain.Vrm ||
                        stepDomain == ExecutionDomain.Skill)
                    {
                        authorityDomain = ExecutionAuthorityDomain.Body;
                        return true;
                    }
                    return false;
                case ExecutionCoordinatorEffectIntentType.RequestSafetyPreempt:
                case ExecutionCoordinatorEffectIntentType.RequestExecutorInterrupt:
                case ExecutionCoordinatorEffectIntentType.RequestExecutorCancel:
                    authorityDomain = ExecutionAuthorityDomain.Safety;
                    return true;
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// Pure deterministic authority decision. An AllowedDispatch result is
    /// still only an additional gate; this class never invokes a runtime.
    /// </summary>
    public sealed class ExecutionLimitedLiveAuthorityGate
    {
        public ExecutionAuthorityDecision Evaluate(
            ExecutionAuthorityPolicySnapshot policy,
            ExecutionAuthorityRequest request,
            ExecutionRuntimeCapabilitySnapshot capability,
            ExecutionAuthorityEvaluationContext context)
        {
            DateTime evaluated = context != null
                ? context.EvaluatedAtUtc : default(DateTime);
            if (policy == null)
                return Decision(request, "", ExecutionAuthorityMode.Disabled,
                    ExecutionAuthorityMode.Disabled,
                    ExecutionAuthorityDisposition.Invalid, false, false,
                    ExecutionAuthorityDenialCodes.PolicyMissing, "", evaluated);
            if (policy.PolicyVersion != ExecutionLiveAuthorityPolicy.PolicyVersion)
                return Decision(request, policy.PolicyVersion,
                    ExecutionAuthorityMode.Disabled,
                    ExecutionAuthorityMode.Disabled,
                    ExecutionAuthorityDisposition.Invalid, false, false,
                    ExecutionAuthorityDenialCodes.PolicyVersionInvalid,
                    policy.DiagnosticLabel, evaluated);
            if (!policy.Enabled)
                return Decision(request, policy.PolicyVersion,
                    ExecutionAuthorityMode.Disabled,
                    ExecutionAuthorityMode.Disabled,
                    ExecutionAuthorityDisposition.Denied, false, false,
                    ExecutionAuthorityDenialCodes.PolicyDisabled,
                    policy.DiagnosticLabel, evaluated);

            string invalid = ValidateRequest(request, context);
            if (invalid.Length > 0)
                return Decision(request, policy.PolicyVersion,
                    ExecutionAuthorityMode.Disabled,
                    ExecutionAuthorityMode.Disabled,
                    ExecutionAuthorityDisposition.Invalid, false, false,
                    invalid, policy.DiagnosticLabel, evaluated);

            ExecutionAuthorityDomainRule rule = policy.FindRule(request.Domain);
            if (rule == null)
                return Decision(request, policy.PolicyVersion,
                    ExecutionAuthorityMode.Disabled,
                    ExecutionAuthorityMode.Disabled,
                    ExecutionAuthorityDisposition.Denied, false, false,
                    ExecutionAuthorityDenialCodes.DomainUnsupported,
                    policy.DiagnosticLabel, evaluated);

            if (context.TabooBlocked)
                return Decision(request, policy.PolicyVersion, rule.Mode,
                    ExecutionAuthorityMode.Disabled,
                    ExecutionAuthorityDisposition.Denied, false, false,
                    ExecutionAuthorityDenialCodes.TabooBlocked,
                    policy.DiagnosticLabel, evaluated);

            if (!ExecutionAuthorityIntentDomainMapper.IsCompatible(
                    request.EffectIntentType, request.Domain))
                return Decision(request, policy.PolicyVersion, rule.Mode,
                    ExecutionAuthorityMode.Disabled,
                    ExecutionAuthorityDisposition.Invalid, false, false,
                    ExecutionAuthorityDenialCodes.EffectDomainMismatch,
                    policy.DiagnosticLabel, evaluated);

            if (rule.Mode == ExecutionAuthorityMode.Disabled)
                return Decision(request, policy.PolicyVersion, rule.Mode,
                    ExecutionAuthorityMode.Disabled,
                    ExecutionAuthorityDisposition.Denied, false, false,
                    ExecutionAuthorityDenialCodes.ModeDenied,
                    policy.DiagnosticLabel, evaluated);
            if (rule.Mode == ExecutionAuthorityMode.ShadowOnly)
                return Decision(request, policy.PolicyVersion, rule.Mode,
                    ExecutionAuthorityMode.ShadowOnly,
                    ExecutionAuthorityDisposition.ShadowOnly, false, false,
                    "", policy.DiagnosticLabel, evaluated);

            ExecutionRuntimeDomainCapability available = capability != null
                ? capability.Find(request.Domain) : null;
            if (available == null)
                return Decision(request, policy.PolicyVersion, rule.Mode,
                    ExecutionAuthorityMode.Disabled,
                    ExecutionAuthorityDisposition.Denied, false, false,
                    ExecutionAuthorityDenialCodes.CapabilityMissing,
                    policy.DiagnosticLabel, evaluated);
            if (!available.Supported)
                return Decision(request, policy.PolicyVersion, rule.Mode,
                    ExecutionAuthorityMode.Disabled,
                    ExecutionAuthorityDisposition.Denied, false, false,
                    ExecutionAuthorityDenialCodes.CapabilityUnsupported,
                    available.Diagnostic, evaluated);

            if (request.IsReadOnly)
            {
                if (!available.ReadOnlySupported)
                    return CapabilityShortage(policy, request, context,
                        rule, available, evaluated, false);
                if (!Contains(rule.AllowedEffectIntentTypes,
                        request.EffectIntentType))
                    return Denied(policy, request, rule,
                        ExecutionAuthorityDenialCodes.WhitelistMismatch,
                        evaluated);
                return Decision(request, policy.PolicyVersion, rule.Mode,
                    ExecutionAuthorityMode.LiveReadOnly,
                    ExecutionAuthorityDisposition.AllowedReadOnly,
                    false, true, "", available.Diagnostic, evaluated);
            }

            if (rule.Mode != ExecutionAuthorityMode.LiveDispatch)
                return Denied(policy, request, rule,
                    ExecutionAuthorityDenialCodes.ModeDenied, evaluated);

            string whitelist = ValidateWhitelist(rule, request);
            if (whitelist.Length > 0)
                return Denied(policy, request, rule, whitelist, evaluated);

            string capabilityFailure = ValidateCapability(available, request);
            if (capabilityFailure.Length > 0)
            {
                if (request.Domain == ExecutionAuthorityDomain.Safety)
                    return Denied(policy, request, rule,
                        capabilityFailure, evaluated);
                return CapabilityShortage(policy, request, context,
                    rule, available, evaluated, true);
            }

            return Decision(request, policy.PolicyVersion, rule.Mode,
                ExecutionAuthorityMode.LiveDispatch,
                ExecutionAuthorityDisposition.AllowedDispatch,
                true, false, "", available.Diagnostic, evaluated);
        }

        private static string ValidateRequest(
            ExecutionAuthorityRequest request,
            ExecutionAuthorityEvaluationContext context)
        {
            if (request == null || context == null ||
                string.IsNullOrWhiteSpace(request.RequestId) ||
                request.Domain == ExecutionAuthorityDomain.Unknown ||
                !Enum.IsDefined(typeof(ExecutionAuthorityDomain), request.Domain) ||
                !Enum.IsDefined(typeof(ExecutionCoordinatorEffectIntentType),
                    request.EffectIntentType) ||
                request.RequestedAtUtc == default(DateTime) ||
                context.EvaluatedAtUtc == default(DateTime))
                return ExecutionAuthorityDenialCodes.RequestInvalid;
            if (!request.IsReadOnly && string.IsNullOrWhiteSpace(request.PlanId))
                return ExecutionAuthorityDenialCodes.RequestInvalid;
            return string.Empty;
        }

        private static string ValidateWhitelist(
            ExecutionAuthorityDomainRule rule,
            ExecutionAuthorityRequest request)
        {
            if (!Contains(rule.AllowedEffectIntentTypes, request.EffectIntentType))
                return ExecutionAuthorityDenialCodes.WhitelistMismatch;
            if (request.Domain == ExecutionAuthorityDomain.Speech ||
                request.Domain == ExecutionAuthorityDomain.Body)
            {
                if (string.IsNullOrWhiteSpace(request.ActionId) ||
                    !Contains(rule.AllowedActionIds, request.ActionId))
                    return ExecutionAuthorityDenialCodes.WhitelistMismatch;
            }
            else if (!string.IsNullOrWhiteSpace(request.ActionId) &&
                !Contains(rule.AllowedActionIds, request.ActionId))
                return ExecutionAuthorityDenialCodes.WhitelistMismatch;

            if (!string.IsNullOrWhiteSpace(request.SkillId) &&
                !Contains(rule.AllowedSkillIds, request.SkillId))
                return ExecutionAuthorityDenialCodes.WhitelistMismatch;

            if (request.Domain == ExecutionAuthorityDomain.LogicalResourceLease)
            {
                if (string.IsNullOrWhiteSpace(request.ResourceId) ||
                    !Contains(rule.AllowedResourceIds, request.ResourceId))
                    return ExecutionAuthorityDenialCodes.WhitelistMismatch;
            }
            else if (!string.IsNullOrWhiteSpace(request.ResourceId) &&
                !Contains(rule.AllowedResourceIds, request.ResourceId))
                return ExecutionAuthorityDenialCodes.WhitelistMismatch;

            if (request.Domain == ExecutionAuthorityDomain.Safety)
            {
                if (request.ControlScope == ExecutionControlScope.None ||
                    !Contains(rule.AllowedControlScopes, request.ControlScope))
                    return ExecutionAuthorityDenialCodes.ScopeMismatch;
            }
            return string.Empty;
        }

        private static string ValidateCapability(
            ExecutionRuntimeDomainCapability capability,
            ExecutionAuthorityRequest request)
        {
            if (!capability.DispatchSupported)
                return ExecutionAuthorityDenialCodes.CapabilityUnsupported;
            if ((request.Domain == ExecutionAuthorityDomain.Speech ||
                    request.Domain == ExecutionAuthorityDomain.Body) &&
                !Contains(capability.SupportedActionIds, request.ActionId))
                return ExecutionAuthorityDenialCodes.CapabilityUnsupported;
            if (request.Domain == ExecutionAuthorityDomain.LogicalResourceLease &&
                !Contains(capability.SupportedResourceIds, request.ResourceId))
                return ExecutionAuthorityDenialCodes.CapabilityUnsupported;
            if (request.Domain == ExecutionAuthorityDomain.Safety &&
                !Contains(capability.SupportedScopes, request.ControlScope))
                return ExecutionAuthorityDenialCodes.ScopeMismatch;
            return string.Empty;
        }

        private static ExecutionAuthorityDecision CapabilityShortage(
            ExecutionAuthorityPolicySnapshot policy,
            ExecutionAuthorityRequest request,
            ExecutionAuthorityEvaluationContext context,
            ExecutionAuthorityDomainRule rule,
            ExecutionRuntimeDomainCapability capability,
            DateTime evaluated, bool dispatchRequest)
        {
            if (context.AllowShadowDowngrade &&
                request.Domain != ExecutionAuthorityDomain.Safety &&
                dispatchRequest)
                return Decision(request, policy.PolicyVersion, rule.Mode,
                    ExecutionAuthorityMode.ShadowOnly,
                    ExecutionAuthorityDisposition.ShadowOnly,
                    false, false,
                    ExecutionAuthorityDenialCodes.CapabilityUnsupported,
                    capability.Diagnostic, evaluated);
            return Denied(policy, request, rule,
                ExecutionAuthorityDenialCodes.CapabilityUnsupported,
                evaluated);
        }

        private static ExecutionAuthorityDecision Denied(
            ExecutionAuthorityPolicySnapshot policy,
            ExecutionAuthorityRequest request,
            ExecutionAuthorityDomainRule rule, string reason,
            DateTime evaluated)
        {
            return Decision(request, policy.PolicyVersion, rule.Mode,
                ExecutionAuthorityMode.Disabled,
                ExecutionAuthorityDisposition.Denied,
                false, false, reason, policy.DiagnosticLabel, evaluated);
        }

        private static ExecutionAuthorityDecision Decision(
            ExecutionAuthorityRequest request, string policyVersion,
            ExecutionAuthorityMode requested,
            ExecutionAuthorityMode effective,
            ExecutionAuthorityDisposition disposition,
            bool dispatch, bool readOnly, string reason,
            string diagnostic, DateTime evaluated)
        {
            return new ExecutionAuthorityDecision(
                request != null ? request.RequestId : string.Empty,
                policyVersion,
                request != null ? request.Domain : ExecutionAuthorityDomain.Unknown,
                requested, effective, disposition, dispatch, readOnly,
                reason, diagnostic, evaluated);
        }

        private static bool Contains<T>(IReadOnlyList<T> values, T value)
        {
            if (values == null) return false;
            EqualityComparer<T> comparer = EqualityComparer<T>.Default;
            for (int i = 0; i < values.Count; i++)
                if (comparer.Equals(values[i], value)) return true;
            return false;
        }
    }

    public sealed class ExecutionAuthorityGateFakeHarness
    {
        private readonly ExecutionLimitedLiveAuthorityGate gate =
            new ExecutionLimitedLiveAuthorityGate();
        public int RuntimeDispatchCount { get; private set; }

        public ExecutionAuthorityDecision Evaluate(
            ExecutionAuthorityPolicySnapshot policy,
            ExecutionAuthorityRequest request,
            ExecutionRuntimeCapabilitySnapshot capability,
            ExecutionAuthorityEvaluationContext context)
        {
            // Intentionally no dispatch, even when decision allows it.
            return gate.Evaluate(policy, request, capability, context);
        }
    }
}
