// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;

namespace SalieriAI.Core.Skills
{
    public sealed class SkillPlanValidationResult
    {
        public bool IsValid { get; }

        public ValidatedSkillPlan ValidatedPlan { get; }

        public IReadOnlyList<string> Errors { get; }

        public IReadOnlyList<string> Warnings { get; }

        internal SkillPlanValidationResult(
            ValidatedSkillPlan validatedPlan,
            List<string> errors,
            List<string> warnings)
        {
            ValidatedPlan = validatedPlan;
            Errors = (errors ?? new List<string>()).AsReadOnly();
            Warnings = (warnings ?? new List<string>()).AsReadOnly();
            IsValid = validatedPlan != null && Errors.Count == 0;
        }
    }

    /// <summary>
    /// Immutable-by-boundary validation token consumed by SkillPlanRunner.
    /// The mutable JSON DTO snapshot is kept internal.
    /// </summary>
    public sealed class ValidatedSkillPlan
    {
        private readonly Dictionary<string, SkillDescriptorData> descriptorsByStep;

        internal SkillPlanData Snapshot { get; }

        public string PlanId => Snapshot.planId;

        public int Version => Snapshot.version;

        public string Platform { get; }

        internal ValidatedSkillPlan(
            SkillPlanData snapshot,
            string platform,
            Dictionary<string, SkillDescriptorData> descriptorsByStep)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            Platform = platform ?? string.Empty;
            this.descriptorsByStep = descriptorsByStep ??
                new Dictionary<string, SkillDescriptorData>(StringComparer.Ordinal);
        }

        internal bool TryGetDescriptor(
            string stepId,
            out SkillDescriptorData descriptor)
        {
            return descriptorsByStep.TryGetValue(stepId, out descriptor);
        }
    }
}
