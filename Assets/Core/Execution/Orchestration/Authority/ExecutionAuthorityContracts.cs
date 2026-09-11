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
using System.Collections.ObjectModel;
using SalieriAI.Core.Execution.Orchestration.Contracts;
using SalieriAI.Core.Execution.Orchestration.Runtime;

namespace SalieriAI.Core.Execution.Orchestration.Authority
{
    public enum ExecutionAuthorityMode
    {
        Disabled = 0,
        ShadowOnly = 1,
        LiveReadOnly = 2,
        LiveDispatch = 3
    }

    public enum ExecutionAuthorityDomain
    {
        Unknown = 0,
        Permission = 1,
        ResourceProfile = 2,
        LogicalResourceLease = 3,
        Speech = 4,
        Body = 5,
        Safety = 6
    }

    public enum ExecutionAuthorityDisposition
    {
        AllowedReadOnly = 0,
        AllowedDispatch = 1,
        ShadowOnly = 2,
        Denied = 3,
        Invalid = 4
    }

    public enum ExecutionAuthorityVerificationLevel
    {
        Unknown = 0,
        ReadOnly = 1,
        LogicalOnly = 2,
        EffectUnverified = 3,
        EffectVerified = 4
    }

    public sealed class ExecutionAuthorityDomainRule
    {
        public ExecutionAuthorityDomain Domain { get; }
        public ExecutionAuthorityMode Mode { get; }
        public IReadOnlyList<ExecutionCoordinatorEffectIntentType>
            AllowedEffectIntentTypes { get; }
        public IReadOnlyList<string> AllowedActionIds { get; }
        public IReadOnlyList<string> AllowedSkillIds { get; }
        public IReadOnlyList<string> AllowedResourceIds { get; }
        public IReadOnlyList<ExecutionControlScope> AllowedControlScopes { get; }

        public ExecutionAuthorityDomainRule(
            ExecutionAuthorityDomain domain, ExecutionAuthorityMode mode,
            IEnumerable<ExecutionCoordinatorEffectIntentType> effects,
            IEnumerable<string> actionIds, IEnumerable<string> skillIds,
            IEnumerable<string> resourceIds,
            IEnumerable<ExecutionControlScope> controlScopes)
        {
            Domain = domain;
            Mode = mode;
            AllowedEffectIntentTypes = Copy(effects);
            AllowedActionIds = TextCopy(actionIds);
            AllowedSkillIds = TextCopy(skillIds);
            AllowedResourceIds = TextCopy(resourceIds);
            AllowedControlScopes = Copy(controlScopes);
        }

        private static IReadOnlyList<T> Copy<T>(IEnumerable<T> source)
        {
            return new ReadOnlyCollection<T>(source != null
                ? new List<T>(source) : new List<T>());
        }

        private static IReadOnlyList<string> TextCopy(
            IEnumerable<string> source)
        {
            var values = new List<string>();
            if (source != null)
                foreach (string value in source)
                    values.Add(value ?? string.Empty);
            return new ReadOnlyCollection<string>(values);
        }
    }

    public sealed class ExecutionAuthorityPolicySnapshot
    {
        public string PolicyVersion { get; }
        public IReadOnlyList<ExecutionAuthorityDomainRule> DomainRules { get; }
        public bool Enabled { get; }
        public string DiagnosticLabel { get; }

        public ExecutionAuthorityPolicySnapshot(
            string policyVersion,
            IEnumerable<ExecutionAuthorityDomainRule> domainRules,
            bool enabled, string diagnosticLabel)
        {
            PolicyVersion = policyVersion ?? string.Empty;
            DomainRules = new ReadOnlyCollection<ExecutionAuthorityDomainRule>(
                domainRules != null
                    ? new List<ExecutionAuthorityDomainRule>(domainRules)
                    : new List<ExecutionAuthorityDomainRule>());
            Enabled = enabled;
            DiagnosticLabel = diagnosticLabel ?? string.Empty;
        }

        public ExecutionAuthorityDomainRule FindRule(
            ExecutionAuthorityDomain domain)
        {
            for (int i = 0; i < DomainRules.Count; i++)
                if (DomainRules[i] != null && DomainRules[i].Domain == domain)
                    return DomainRules[i];
            return null;
        }
    }

    public sealed class ExecutionRuntimeDomainCapability
    {
        public ExecutionAuthorityDomain Domain { get; }
        public bool Supported { get; }
        public bool ReadOnlySupported { get; }
        public bool DispatchSupported { get; }
        public ExecutionAuthorityVerificationLevel VerificationLevel { get; }
        public IReadOnlyList<ExecutionControlScope> SupportedScopes { get; }
        public IReadOnlyList<string> SupportedActionIds { get; }
        public IReadOnlyList<string> SupportedResourceIds { get; }
        public string Diagnostic { get; }

        public ExecutionRuntimeDomainCapability(
            ExecutionAuthorityDomain domain, bool supported,
            bool readOnlySupported, bool dispatchSupported,
            ExecutionAuthorityVerificationLevel verificationLevel,
            IEnumerable<ExecutionControlScope> supportedScopes,
            IEnumerable<string> supportedActionIds,
            IEnumerable<string> supportedResourceIds, string diagnostic)
        {
            Domain = domain;
            Supported = supported;
            ReadOnlySupported = readOnlySupported;
            DispatchSupported = dispatchSupported;
            VerificationLevel = verificationLevel;
            SupportedScopes = Copy(supportedScopes);
            SupportedActionIds = TextCopy(supportedActionIds);
            SupportedResourceIds = TextCopy(supportedResourceIds);
            Diagnostic = diagnostic ?? string.Empty;
        }

        private static IReadOnlyList<T> Copy<T>(IEnumerable<T> source)
        {
            return new ReadOnlyCollection<T>(source != null
                ? new List<T>(source) : new List<T>());
        }

        private static IReadOnlyList<string> TextCopy(
            IEnumerable<string> source)
        {
            var values = new List<string>();
            if (source != null)
                foreach (string value in source)
                    values.Add(value ?? string.Empty);
            return new ReadOnlyCollection<string>(values);
        }
    }

    public sealed class ExecutionRuntimeCapabilitySnapshot
    {
        public string SnapshotVersion { get; }
        public IReadOnlyList<ExecutionRuntimeDomainCapability> Domains { get; }

        public ExecutionRuntimeCapabilitySnapshot(
            string snapshotVersion,
            IEnumerable<ExecutionRuntimeDomainCapability> domains)
        {
            SnapshotVersion = snapshotVersion ?? string.Empty;
            Domains = new ReadOnlyCollection<ExecutionRuntimeDomainCapability>(
                domains != null
                    ? new List<ExecutionRuntimeDomainCapability>(domains)
                    : new List<ExecutionRuntimeDomainCapability>());
        }

        public ExecutionRuntimeDomainCapability Find(
            ExecutionAuthorityDomain domain)
        {
            for (int i = 0; i < Domains.Count; i++)
                if (Domains[i] != null && Domains[i].Domain == domain)
                    return Domains[i];
            return null;
        }
    }

    public sealed class ExecutionAuthorityRequest
    {
        public string PlanId { get; }
        public string StepId { get; }
        public string ExecutionAttemptId { get; }
        public string RequestId { get; }
        public ExecutionAuthorityDomain Domain { get; }
        public ExecutionCoordinatorEffectIntentType EffectIntentType { get; }
        public string ActionId { get; }
        public string SkillId { get; }
        public string ResourceId { get; }
        public ExecutionControlScope ControlScope { get; }
        public bool IsReadOnly { get; }
        public DateTime RequestedAtUtc { get; }

        public ExecutionAuthorityRequest(
            string planId, string stepId, string executionAttemptId,
            string requestId, ExecutionAuthorityDomain domain,
            ExecutionCoordinatorEffectIntentType effectIntentType,
            string actionId, string skillId, string resourceId,
            ExecutionControlScope controlScope, bool isReadOnly,
            DateTime requestedAtUtc)
        {
            PlanId = planId ?? string.Empty;
            StepId = stepId ?? string.Empty;
            ExecutionAttemptId = executionAttemptId ?? string.Empty;
            RequestId = requestId ?? string.Empty;
            Domain = domain;
            EffectIntentType = effectIntentType;
            ActionId = actionId ?? string.Empty;
            SkillId = skillId ?? string.Empty;
            ResourceId = resourceId ?? string.Empty;
            ControlScope = controlScope;
            IsReadOnly = isReadOnly;
            RequestedAtUtc = NormalizeUtc(requestedAtUtc);
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            if (value == default(DateTime) || value.Kind == DateTimeKind.Utc)
                return value;
            return value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }
    }

    public sealed class ExecutionAuthorityEvaluationContext
    {
        public bool TabooBlocked { get; }
        public bool AllowShadowDowngrade { get; }
        public DateTime EvaluatedAtUtc { get; }
        public string ContextDiagnostic { get; }

        public ExecutionAuthorityEvaluationContext(
            bool tabooBlocked, bool allowShadowDowngrade,
            DateTime evaluatedAtUtc, string contextDiagnostic)
        {
            TabooBlocked = tabooBlocked;
            AllowShadowDowngrade = allowShadowDowngrade;
            EvaluatedAtUtc = evaluatedAtUtc;
            ContextDiagnostic = contextDiagnostic ?? string.Empty;
        }
    }

    public sealed class ExecutionAuthorityDecision
    {
        public string RequestId { get; }
        public string PolicyVersion { get; }
        public ExecutionAuthorityDomain Domain { get; }
        public ExecutionAuthorityMode RequestedMode { get; }
        public ExecutionAuthorityMode EffectiveMode { get; }
        public ExecutionAuthorityDisposition Disposition { get; }
        public bool DispatchAllowed { get; }
        public bool ReadOnlyAllowed { get; }
        public string DenialReason { get; }
        public string Diagnostic { get; }
        public DateTime EvaluatedAtUtc { get; }

        public ExecutionAuthorityDecision(
            string requestId, string policyVersion,
            ExecutionAuthorityDomain domain,
            ExecutionAuthorityMode requestedMode,
            ExecutionAuthorityMode effectiveMode,
            ExecutionAuthorityDisposition disposition,
            bool dispatchAllowed, bool readOnlyAllowed,
            string denialReason, string diagnostic,
            DateTime evaluatedAtUtc)
        {
            RequestId = requestId ?? string.Empty;
            PolicyVersion = policyVersion ?? string.Empty;
            Domain = domain;
            RequestedMode = requestedMode;
            EffectiveMode = effectiveMode;
            Disposition = disposition;
            DispatchAllowed = dispatchAllowed;
            ReadOnlyAllowed = readOnlyAllowed;
            DenialReason = denialReason ?? string.Empty;
            Diagnostic = diagnostic ?? string.Empty;
            EvaluatedAtUtc = evaluatedAtUtc;
        }
    }
}
