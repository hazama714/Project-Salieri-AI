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
    public static class ExecutionPlanCompositionPolicyFailureCodes
    {
        public const string NullPolicy = "NullCompositionPolicy";
        public const string InvalidRequiredness = "InvalidRequiredness";
        public const string MissingPlanCompletionPolicy =
            "MissingPlanCompletionPolicy";
        public const string InvalidPlanCompletionPolicy =
            "InvalidPlanCompletionPolicy";
        public const string MissingPlanFailurePolicy =
            "MissingPlanFailurePolicy";
        public const string InvalidPlanFailurePolicy =
            "InvalidPlanFailurePolicy";
        public const string MissingPlanCancellationPolicy =
            "MissingPlanCancellationPolicy";
        public const string InvalidPlanCancellationPolicy =
            "InvalidPlanCancellationPolicy";
    }

    public sealed class ExecutionPlanCompositionPolicyValidationResult
    {
        public bool IsValid => FailureCodes.Count == 0;
        public IReadOnlyList<string> FailureCodes { get; }
        public IReadOnlyList<string> InvalidFields { get; }

        internal ExecutionPlanCompositionPolicyValidationResult(
            IEnumerable<string> failureCodes,
            IEnumerable<string> invalidFields)
        {
            FailureCodes = Copy(failureCodes);
            InvalidFields = Copy(invalidFields);
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
                    : new List<string>());
        }
    }

    /// <summary>
    /// Pure completeness check for the values that the existing construction
    /// spec and structure validation require from plan composition. It does
    /// not validate a plan and does not infer or create any policy value.
    /// </summary>
    public static class ExecutionPlanCompositionPolicyValidator
    {
        public const string ValidatorVersion =
            "execution-plan-composition-policy-validator-v0.1";

        public static ExecutionPlanCompositionPolicyValidationResult Validate(
            ExecutionPlanCompositionPolicy policy)
        {
            var failures = new List<string>();
            var fields = new List<string>();

            if (policy == null)
            {
                Add(
                    failures,
                    fields,
                    ExecutionPlanCompositionPolicyFailureCodes.NullPolicy,
                    "policy");
                return new ExecutionPlanCompositionPolicyValidationResult(
                    failures, fields);
            }

            if (!Enum.IsDefined(
                    typeof(ExecutionRequiredness),
                    policy.Requiredness))
            {
                Add(
                    failures,
                    fields,
                    ExecutionPlanCompositionPolicyFailureCodes
                        .InvalidRequiredness,
                    "requiredness");
            }

            if (policy.PlanCompletionPolicy == null)
            {
                Add(
                    failures,
                    fields,
                    ExecutionPlanCompositionPolicyFailureCodes
                        .MissingPlanCompletionPolicy,
                    "planCompletionPolicy");
            }
            else if (!Enum.IsDefined(
                typeof(ExecutionOptionalFailureBehavior),
                policy.PlanCompletionPolicy.OptionalFailureBehavior))
            {
                Add(
                    failures,
                    fields,
                    ExecutionPlanCompositionPolicyFailureCodes
                        .InvalidPlanCompletionPolicy,
                    "planCompletionPolicy.optionalFailureBehavior");
            }

            if (policy.PlanFailurePolicy == null)
            {
                Add(
                    failures,
                    fields,
                    ExecutionPlanCompositionPolicyFailureCodes
                        .MissingPlanFailurePolicy,
                    "planFailurePolicy");
            }
            else if (!Enum.IsDefined(
                typeof(ExecutionFailureAction),
                policy.PlanFailurePolicy.Action))
            {
                Add(
                    failures,
                    fields,
                    ExecutionPlanCompositionPolicyFailureCodes
                        .InvalidPlanFailurePolicy,
                    "planFailurePolicy.action");
            }

            if (policy.PlanCancellationPolicy == null)
            {
                Add(
                    failures,
                    fields,
                    ExecutionPlanCompositionPolicyFailureCodes
                        .MissingPlanCancellationPolicy,
                    "planCancellationPolicy");
            }
            else if (!Enum.IsDefined(
                typeof(ExecutionCancellationScope),
                policy.PlanCancellationPolicy.Scope))
            {
                Add(
                    failures,
                    fields,
                    ExecutionPlanCompositionPolicyFailureCodes
                        .InvalidPlanCancellationPolicy,
                    "planCancellationPolicy.scope");
            }

            return new ExecutionPlanCompositionPolicyValidationResult(
                failures, fields);
        }

        private static void Add(
            ICollection<string> failures,
            ICollection<string> fields,
            string failureCode,
            string field)
        {
            failures.Add(failureCode);
            fields.Add(field);
        }
    }
}
