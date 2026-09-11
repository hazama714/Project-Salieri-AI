// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SalieriAI.Core.Skills
{
    public sealed class SkillCatalogLoadResult
    {
        public bool Succeeded { get; }

        public SkillCatalogData Catalog { get; }

        public IReadOnlyList<string> Errors { get; }

        public IReadOnlyList<string> Warnings { get; }

        internal SkillCatalogLoadResult(
            SkillCatalogData catalog,
            List<string> errors,
            List<string> warnings)
        {
            Catalog = catalog;
            Errors = (errors ?? new List<string>()).AsReadOnly();
            Warnings = (warnings ?? new List<string>()).AsReadOnly();
            Succeeded = catalog != null && Errors.Count == 0;
        }
    }

    public sealed class SkillPlanLoadResult
    {
        public bool Succeeded { get; }

        public SkillPlanData Plan { get; }

        public IReadOnlyList<string> Errors { get; }

        public IReadOnlyList<string> Warnings { get; }

        internal SkillPlanLoadResult(
            SkillPlanData plan,
            List<string> errors,
            List<string> warnings)
        {
            Plan = plan;
            Errors = (errors ?? new List<string>()).AsReadOnly();
            Warnings = (warnings ?? new List<string>()).AsReadOnly();
            Succeeded = plan != null && Errors.Count == 0;
        }
    }

    /// <summary>
    /// Parses and normalizes v0.1 Skill JSON supplied as a string or TextAsset.
    /// It intentionally has no StreamingAssets or persistentDataPath dependency.
    /// </summary>
    public sealed class SkillCatalogLoader
    {
        public const int SupportedSchemaVersion = 1;

        private readonly SkillPayloadTypeRegistry payloadTypeRegistry;
        private readonly SkillImplementationRegistry implementationRegistry;

        public SkillCatalogLoader(
            SkillPayloadTypeRegistry payloadTypeRegistry,
            SkillImplementationRegistry implementationRegistry)
        {
            this.payloadTypeRegistry = payloadTypeRegistry;
            this.implementationRegistry = implementationRegistry;
        }

        public SkillCatalogLoadResult LoadCatalog(TextAsset textAsset)
        {
            return LoadCatalogFromJson(
                textAsset != null ? textAsset.text : string.Empty
            );
        }

        public SkillCatalogLoadResult LoadCatalogFromJson(string json)
        {
            List<string> errors = new List<string>();
            List<string> warnings = CreateJsonUtilityWarning();

            if (string.IsNullOrWhiteSpace(json))
            {
                errors.Add("Catalog JSON is empty.");
                return new SkillCatalogLoadResult(null, errors, warnings);
            }

            if (payloadTypeRegistry == null)
                errors.Add("Payload Type Registry is null.");

            if (implementationRegistry == null)
                errors.Add("Implementation Registry is null.");

            if (errors.Count > 0)
                return new SkillCatalogLoadResult(null, errors, warnings);

            SkillCatalogData catalog;

            try
            {
                catalog = JsonUtility.FromJson<SkillCatalogData>(json);
            }
            catch (Exception ex)
            {
                errors.Add(
                    "Catalog JSON parse failed: " +
                    ex.GetType().Name + ": " + ex.Message
                );
                return new SkillCatalogLoadResult(null, errors, warnings);
            }

            if (catalog == null)
            {
                errors.Add("Catalog JSON produced a null object.");
                return new SkillCatalogLoadResult(null, errors, warnings);
            }

            if (catalog.schemaVersion != SupportedSchemaVersion)
            {
                errors.Add(
                    "Unsupported catalog schemaVersion: " +
                    catalog.schemaVersion
                );
            }

            catalog.skills = catalog.skills ?? Array.Empty<SkillDescriptorData>();

            HashSet<string> descriptorKeys =
                new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < catalog.skills.Length; i++)
            {
                SkillDescriptorData descriptor = catalog.skills[i];
                string prefix = "skills[" + i + "]";

                if (descriptor == null)
                {
                    errors.Add(prefix + " is null.");
                    continue;
                }

                NormalizeDescriptor(descriptor);
                ValidateDescriptor(
                    descriptor,
                    prefix,
                    descriptorKeys,
                    errors
                );
            }

            return new SkillCatalogLoadResult(
                errors.Count == 0 ? catalog : null,
                errors,
                warnings
            );
        }

        public SkillPlanLoadResult LoadPlan(TextAsset textAsset)
        {
            return LoadPlanFromJson(
                textAsset != null ? textAsset.text : string.Empty
            );
        }

        public SkillPlanLoadResult LoadPlanFromJson(string json)
        {
            List<string> errors = new List<string>();
            List<string> warnings = CreateJsonUtilityWarning();

            if (string.IsNullOrWhiteSpace(json))
            {
                errors.Add("Plan JSON is empty.");
                return new SkillPlanLoadResult(null, errors, warnings);
            }

            SkillPlanData plan;

            try
            {
                plan = JsonUtility.FromJson<SkillPlanData>(json);
            }
            catch (Exception ex)
            {
                errors.Add(
                    "Plan JSON parse failed: " +
                    ex.GetType().Name + ": " + ex.Message
                );
                return new SkillPlanLoadResult(null, errors, warnings);
            }

            if (plan == null)
            {
                errors.Add("Plan JSON produced a null object.");
                return new SkillPlanLoadResult(null, errors, warnings);
            }

            if (plan.schemaVersion != SupportedSchemaVersion)
            {
                errors.Add(
                    "Unsupported plan schemaVersion: " +
                    plan.schemaVersion
                );
            }

            NormalizePlan(plan);

            return new SkillPlanLoadResult(
                errors.Count == 0 ? plan : null,
                errors,
                warnings
            );
        }

        private void ValidateDescriptor(
            SkillDescriptorData descriptor,
            string prefix,
            HashSet<string> descriptorKeys,
            List<string> errors)
        {
            if (descriptor.schemaVersion != SupportedSchemaVersion)
                errors.Add(prefix + ".schemaVersion is unsupported.");

            Require(descriptor.skillId, prefix + ".skillId", errors);
            Require(descriptor.displayName, prefix + ".displayName", errors);
            Require(descriptor.description, prefix + ".description", errors);
            Require(descriptor.category, prefix + ".category", errors);
            Require(descriptor.inputType, prefix + ".inputType", errors);
            Require(descriptor.outputType, prefix + ".outputType", errors);
            Require(descriptor.riskLevel, prefix + ".riskLevel", errors);
            Require(
                descriptor.implementationHandlerId,
                prefix + ".implementationHandlerId",
                errors
            );

            if (descriptor.version <= 0)
                errors.Add(prefix + ".version must be greater than zero.");

            if (descriptor.timeoutSeconds < 0f)
                errors.Add(prefix + ".timeoutSeconds must not be negative.");

            if (descriptor.platformSupport.Length == 0)
                errors.Add(prefix + ".platformSupport must not be empty.");

            string descriptorKey =
                descriptor.skillId + "@" + descriptor.version;

            if (!descriptorKeys.Add(descriptorKey))
                errors.Add("Duplicate Skill descriptor: " + descriptorKey);

            if (!payloadTypeRegistry.TryResolve(
                    descriptor.inputType,
                    out Type registeredInputType))
            {
                errors.Add(
                    prefix + ".inputType is not registered: " +
                    descriptor.inputType
                );
            }

            if (!payloadTypeRegistry.TryResolve(
                    descriptor.outputType,
                    out Type registeredOutputType))
            {
                errors.Add(
                    prefix + ".outputType is not registered: " +
                    descriptor.outputType
                );
            }

            if (!implementationRegistry.TryGet(
                    descriptor.skillId,
                    descriptor.version,
                    out ISkillHandler handler))
            {
                errors.Add(
                    prefix + " has no registered C# Handler: " +
                    descriptorKey
                );
                return;
            }

            if (!string.Equals(
                    descriptor.inputType,
                    handler.InputPayloadTypeId,
                    StringComparison.Ordinal))
            {
                errors.Add(prefix + ".inputType contradicts Handler definition.");
            }

            if (!string.Equals(
                    descriptor.outputType,
                    handler.OutputPayloadTypeId,
                    StringComparison.Ordinal))
            {
                errors.Add(prefix + ".outputType contradicts Handler definition.");
            }

            if (registeredInputType != null &&
                registeredInputType != handler.InputPayloadType)
            {
                errors.Add(prefix + ".inputType C# type does not match Handler.");
            }

            if (registeredOutputType != null &&
                registeredOutputType != handler.OutputPayloadType)
            {
                errors.Add(prefix + ".outputType C# type does not match Handler.");
            }
        }

        private static void NormalizeDescriptor(SkillDescriptorData descriptor)
        {
            descriptor.skillId = Normalize(descriptor.skillId);
            descriptor.displayName = Normalize(descriptor.displayName);
            descriptor.description = Normalize(descriptor.description);
            descriptor.category = Normalize(descriptor.category);
            descriptor.inputType = Normalize(descriptor.inputType);
            descriptor.outputType = Normalize(descriptor.outputType);
            descriptor.riskLevel = Normalize(descriptor.riskLevel);
            descriptor.implementationHandlerId =
                Normalize(descriptor.implementationHandlerId);
            descriptor.preconditions = descriptor.preconditions ?? Array.Empty<string>();
            descriptor.effects = descriptor.effects ?? Array.Empty<string>();
            descriptor.requiredPermissions =
                descriptor.requiredPermissions ?? Array.Empty<string>();
            descriptor.requiredResources =
                descriptor.requiredResources ?? Array.Empty<string>();
            descriptor.platformSupport =
                descriptor.platformSupport ?? Array.Empty<string>();
        }

        private static void NormalizePlan(SkillPlanData plan)
        {
            plan.planId = Normalize(plan.planId);
            plan.modeId = Normalize(plan.modeId);
            plan.displayName = Normalize(plan.displayName);
            plan.goal = Normalize(plan.goal);
            plan.source = Normalize(plan.source);
            plan.entryStepId = Normalize(plan.entryStepId);
            plan.steps = plan.steps ?? Array.Empty<SkillPlanStepData>();

            for (int i = 0; i < plan.steps.Length; i++)
            {
                SkillPlanStepData step = plan.steps[i];
                if (step == null)
                    continue;

                step.stepId = Normalize(step.stepId);
                step.skillId = Normalize(step.skillId);
                step.inputFrom = Normalize(step.inputFrom);
                step.onSuccess = Normalize(step.onSuccess);
                step.onFailure = Normalize(step.onFailure);
            }
        }

        private static List<string> CreateJsonUtilityWarning()
        {
            return new List<string>
            {
                "JsonUtility ignores unknown JSON fields. " +
                "This v0.1 loader must not be treated as strict-schema validation."
            };
        }

        private static void Require(
            string value,
            string fieldName,
            List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
                errors.Add(fieldName + " is required.");
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }
    }
}
