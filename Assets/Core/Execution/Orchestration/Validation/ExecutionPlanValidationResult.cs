// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using SalieriAI.Core.Execution.Orchestration.Contracts;

namespace SalieriAI.Core.Execution.Orchestration.Validation
{
    public sealed class ExecutionPlanValidationContext
    {
        public string ValidationId { get; }
        public DateTime CheckedAtUtc { get; }

        public ExecutionPlanValidationContext(
            string validationId,
            DateTime checkedAtUtc)
        {
            ValidationId = validationId ?? string.Empty;
            CheckedAtUtc = NormalizeUtc(checkedAtUtc);
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            if (value == default(DateTime) ||
                value.Kind == DateTimeKind.Utc)
            {
                return value;
            }

            return value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }
    }

    public sealed class ExecutionPlanValidationResult
    {
        public string ValidationId { get; }
        public DateTime CheckedAtUtc { get; }
        public string ValidatorVersion { get; }
        public bool IsValid { get; }
        public IReadOnlyList<string> FailureCodes { get; }
        public IReadOnlyList<string> InvalidFields { get; }
        public IReadOnlyList<string> Warnings { get; }
        public string DiagnosticMessage { get; }

        internal ExecutionPlanValidationResult(
            string validationId,
            DateTime checkedAtUtc,
            string validatorVersion,
            IEnumerable<string> failureCodes,
            IEnumerable<string> invalidFields,
            IEnumerable<string> warnings,
            string diagnosticMessage)
        {
            ValidationId = validationId ?? string.Empty;
            CheckedAtUtc = checkedAtUtc;
            ValidatorVersion = validatorVersion ?? string.Empty;
            FailureCodes = Copy(failureCodes);
            InvalidFields = Copy(invalidFields);
            Warnings = Copy(warnings);
            DiagnosticMessage = diagnosticMessage ?? string.Empty;
            IsValid = FailureCodes.Count == 0;
        }

        public bool ContainsFailure(string failureCode)
        {
            for (int i = 0; i < FailureCodes.Count; i++)
            {
                if (FailureCodes[i] == failureCode)
                    return true;
            }

            return false;
        }

        private static IReadOnlyList<string> Copy(
            IEnumerable<string> source)
        {
            return new ReadOnlyCollection<string>(
                source != null
                    ? new List<string>(source)
                    : new List<string>()
            );
        }
    }

    internal sealed class ExecutionValidationIssues
    {
        private readonly List<string> failureCodes =
            new List<string>();
        private readonly List<string> invalidFields =
            new List<string>();
        private readonly List<string> warnings =
            new List<string>();

        public IReadOnlyList<string> FailureCodes => failureCodes;
        public IReadOnlyList<string> InvalidFields => invalidFields;
        public IReadOnlyList<string> Warnings => warnings;

        public void AddFailure(string code, string field)
        {
            if (!failureCodes.Contains(code))
                failureCodes.Add(code);

            if (!string.IsNullOrEmpty(field) &&
                !invalidFields.Contains(field))
            {
                invalidFields.Add(field);
            }
        }

        public void AddWarning(string warning)
        {
            if (!string.IsNullOrEmpty(warning) &&
                !warnings.Contains(warning))
            {
                warnings.Add(warning);
            }
        }
    }

    /// <summary>
    /// Evidence that only structure and Policy v0.1 were validated. This
    /// token does not represent capability, grounding, safety, execution
    /// authorization, resource acquisition, Limbo permission, executor
    /// availability, or runtime success.
    /// </summary>
    public sealed class ValidatedExecutionPlan
    {
        public ReactionExecutionPlan SourcePlan { get; }
        public DateTime ValidatedAtUtc { get; }
        public string ValidatorVersion { get; }
        public ExecutionOrchestrationPolicyVersion PolicyVersion
        {
            get;
        }
        public string StructureValidationId { get; }
        public string PolicyValidationId { get; }

        internal ValidatedExecutionPlan(
            ReactionExecutionPlan sourcePlan,
            DateTime validatedAtUtc,
            string validatorVersion,
            ExecutionOrchestrationPolicyVersion policyVersion,
            string structureValidationId,
            string policyValidationId)
        {
            SourcePlan = sourcePlan;
            ValidatedAtUtc = validatedAtUtc;
            ValidatorVersion = validatorVersion ?? string.Empty;
            PolicyVersion = policyVersion;
            StructureValidationId =
                structureValidationId ?? string.Empty;
            PolicyValidationId = policyValidationId ?? string.Empty;
        }
    }
}
