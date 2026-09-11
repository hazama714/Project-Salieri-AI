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
    /// <summary>
    /// Load-time structural validator for sequential v0.1 Skill Plans.
    /// Runtime Limbo, BODY and Scene safety checks are outside this version.
    /// </summary>
    public sealed class SkillPlanValidator
    {
        public const int HardMaximumSteps = 128;
        public const float HardMaximumExecutionSeconds = 3600f;

        public const string CompleteTerminal = "$complete";
        public const string FailedTerminal = "$failed";

        private readonly SkillPayloadTypeRegistry payloadTypeRegistry;
        private readonly SkillImplementationRegistry implementationRegistry;

        public SkillPlanValidator(
            SkillPayloadTypeRegistry payloadTypeRegistry,
            SkillImplementationRegistry implementationRegistry)
        {
            this.payloadTypeRegistry = payloadTypeRegistry;
            this.implementationRegistry = implementationRegistry;
        }

        public SkillPlanValidationResult Validate(
            SkillPlanData plan,
            SkillCatalogData catalog,
            string platformId)
        {
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();

            if (payloadTypeRegistry == null)
                errors.Add("Payload Type Registry is null.");

            if (implementationRegistry == null)
                errors.Add("Implementation Registry is null.");

            if (plan == null)
                errors.Add("Plan is null.");

            if (catalog == null)
                errors.Add("Catalog is null.");

            if (errors.Count > 0)
            {
                return new SkillPlanValidationResult(
                    null,
                    errors,
                    warnings
                );
            }

            string platform = string.IsNullOrWhiteSpace(platformId)
                ? "Unknown"
                : platformId.Trim();

            ValidatePlanHeader(plan, errors);

            Dictionary<string, SkillDescriptorData> descriptorMap =
                BuildDescriptorMap(catalog, errors);

            Dictionary<string, SkillPlanStepData> stepMap =
                BuildStepMap(plan, errors);

            if (stepMap.Count > 0 &&
                !stepMap.ContainsKey(plan.entryStepId ?? string.Empty))
            {
                errors.Add(
                    "entryStepId does not exist: " +
                    (plan.entryStepId ?? string.Empty)
                );
            }

            Dictionary<string, SkillDescriptorData> descriptorsByStep =
                new Dictionary<string, SkillDescriptorData>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, SkillPlanStepData> pair in stepMap)
            {
                ValidateStep(
                    pair.Value,
                    stepMap,
                    descriptorMap,
                    descriptorsByStep,
                    platform,
                    errors
                );
            }

            ValidateReachabilityAndCycles(plan, stepMap, errors);

            if (errors.Count > 0)
            {
                return new SkillPlanValidationResult(
                    null,
                    errors,
                    warnings
                );
            }

            SkillPlanData planSnapshot = ClonePlan(plan);
            Dictionary<string, SkillDescriptorData> descriptorSnapshot =
                new Dictionary<string, SkillDescriptorData>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, SkillDescriptorData> pair in descriptorsByStep)
                descriptorSnapshot.Add(pair.Key, CloneDescriptor(pair.Value));

            return new SkillPlanValidationResult(
                new ValidatedSkillPlan(
                    planSnapshot,
                    platform,
                    descriptorSnapshot
                ),
                errors,
                warnings
            );
        }

        private static void ValidatePlanHeader(
            SkillPlanData plan,
            List<string> errors)
        {
            if (plan.schemaVersion != SkillCatalogLoader.SupportedSchemaVersion)
                errors.Add("Unsupported plan schemaVersion: " + plan.schemaVersion);

            if (string.IsNullOrWhiteSpace(plan.planId))
                errors.Add("planId is required.");

            if (plan.version <= 0)
                errors.Add("Plan version must be greater than zero.");

            if (string.IsNullOrWhiteSpace(plan.entryStepId))
                errors.Add("entryStepId is required.");

            if (plan.maximumSteps <= 0)
                errors.Add("maximumSteps must be greater than zero.");

            if (plan.maximumSteps > HardMaximumSteps)
            {
                errors.Add(
                    "maximumSteps exceeds runner hard limit: " +
                    HardMaximumSteps
                );
            }

            if (plan.maximumExecutionSeconds <= 0f)
                errors.Add("maximumExecutionSeconds must be greater than zero.");

            if (plan.maximumExecutionSeconds > HardMaximumExecutionSeconds)
            {
                errors.Add(
                    "maximumExecutionSeconds exceeds runner hard limit: " +
                    HardMaximumExecutionSeconds
                );
            }

            SkillPlanStepData[] steps =
                plan.steps ?? Array.Empty<SkillPlanStepData>();

            if (steps.Length == 0)
                errors.Add("Plan must contain at least one Step.");

            if (plan.maximumSteps > 0 && steps.Length > plan.maximumSteps)
            {
                errors.Add(
                    "Plan Step count exceeds maximumSteps. count=" +
                    steps.Length + " maximum=" + plan.maximumSteps
                );
            }
        }

        private Dictionary<string, SkillDescriptorData> BuildDescriptorMap(
            SkillCatalogData catalog,
            List<string> errors)
        {
            Dictionary<string, SkillDescriptorData> map =
                new Dictionary<string, SkillDescriptorData>(StringComparer.Ordinal);

            SkillDescriptorData[] descriptors =
                catalog.skills ?? Array.Empty<SkillDescriptorData>();

            for (int i = 0; i < descriptors.Length; i++)
            {
                SkillDescriptorData descriptor = descriptors[i];
                if (descriptor == null)
                {
                    errors.Add("Catalog descriptor is null at index " + i + ".");
                    continue;
                }

                string key = BuildSkillKey(
                    descriptor.skillId,
                    descriptor.version
                );

                if (map.ContainsKey(key))
                {
                    errors.Add("Duplicate Catalog Skill: " + key);
                    continue;
                }

                map.Add(key, descriptor);
            }

            return map;
        }

        private static Dictionary<string, SkillPlanStepData> BuildStepMap(
            SkillPlanData plan,
            List<string> errors)
        {
            Dictionary<string, SkillPlanStepData> map =
                new Dictionary<string, SkillPlanStepData>(StringComparer.Ordinal);

            SkillPlanStepData[] steps =
                plan.steps ?? Array.Empty<SkillPlanStepData>();

            for (int i = 0; i < steps.Length; i++)
            {
                SkillPlanStepData step = steps[i];

                if (step == null)
                {
                    errors.Add("Plan Step is null at index " + i + ".");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(step.stepId))
                {
                    errors.Add("steps[" + i + "].stepId is required.");
                    continue;
                }

                if (map.ContainsKey(step.stepId))
                {
                    errors.Add("Duplicate Step ID: " + step.stepId);
                    continue;
                }

                map.Add(step.stepId, step);
            }

            return map;
        }

        private void ValidateStep(
            SkillPlanStepData step,
            Dictionary<string, SkillPlanStepData> stepMap,
            Dictionary<string, SkillDescriptorData> descriptorMap,
            Dictionary<string, SkillDescriptorData> descriptorsByStep,
            string platform,
            List<string> errors)
        {
            string prefix = "Step '" + step.stepId + "'";

            if (string.IsNullOrWhiteSpace(step.skillId))
                errors.Add(prefix + " skillId is required.");

            if (step.skillVersion <= 0)
                errors.Add(prefix + " skillVersion must be greater than zero.");

            string skillKey = BuildSkillKey(step.skillId, step.skillVersion);

            if (!descriptorMap.TryGetValue(
                    skillKey,
                    out SkillDescriptorData descriptor))
            {
                errors.Add(prefix + " references an unknown Skill: " + skillKey);
                ValidateTransitions(step, stepMap, errors);
                return;
            }

            descriptorsByStep[step.stepId] = descriptor;

            if (!implementationRegistry.TryGet(
                    step.skillId,
                    step.skillVersion,
                    out ISkillHandler handler))
            {
                errors.Add(prefix + " Handler is not registered: " + skillKey);
            }
            else
            {
                ValidateHandlerContract(
                    prefix,
                    descriptor,
                    handler,
                    errors
                );
            }

            if (!SupportsPlatform(descriptor.platformSupport, platform))
            {
                errors.Add(
                    prefix + " does not support platform '" +
                    platform + "'."
                );
            }

            if (!string.IsNullOrWhiteSpace(step.inputFrom))
            {
                if (!stepMap.TryGetValue(
                        step.inputFrom,
                        out SkillPlanStepData sourceStep))
                {
                    errors.Add(
                        prefix + " inputFrom does not exist: " +
                        step.inputFrom
                    );
                }
                else
                {
                    string sourceKey = BuildSkillKey(
                        sourceStep.skillId,
                        sourceStep.skillVersion
                    );

                    if (descriptorMap.TryGetValue(
                            sourceKey,
                            out SkillDescriptorData sourceDescriptor) &&
                        !string.Equals(
                            sourceDescriptor.outputType,
                            descriptor.inputType,
                            StringComparison.Ordinal))
                    {
                        errors.Add(
                            prefix + " input type mismatch. inputFrom=" +
                            step.inputFrom +
                            " outputType=" + sourceDescriptor.outputType +
                            " inputType=" + descriptor.inputType
                        );
                    }
                }
            }

            ValidateTransitions(step, stepMap, errors);
        }

        private void ValidateHandlerContract(
            string prefix,
            SkillDescriptorData descriptor,
            ISkillHandler handler,
            List<string> errors)
        {
            if (!string.Equals(
                    descriptor.inputType,
                    handler.InputPayloadTypeId,
                    StringComparison.Ordinal))
            {
                errors.Add(prefix + " inputType contradicts C# Handler.");
            }

            if (!string.Equals(
                    descriptor.outputType,
                    handler.OutputPayloadTypeId,
                    StringComparison.Ordinal))
            {
                errors.Add(prefix + " outputType contradicts C# Handler.");
            }

            if (!payloadTypeRegistry.IsRegistered(
                    descriptor.inputType,
                    handler.InputPayloadType))
            {
                errors.Add(prefix + " input Payload C# type is inconsistent.");
            }

            if (!payloadTypeRegistry.IsRegistered(
                    descriptor.outputType,
                    handler.OutputPayloadType))
            {
                errors.Add(prefix + " output Payload C# type is inconsistent.");
            }
        }

        private static void ValidateTransitions(
            SkillPlanStepData step,
            Dictionary<string, SkillPlanStepData> stepMap,
            List<string> errors)
        {
            ValidateTransition(
                step.stepId,
                "onSuccess",
                step.onSuccess,
                stepMap,
                errors
            );

            ValidateTransition(
                step.stepId,
                "onFailure",
                step.onFailure,
                stepMap,
                errors
            );
        }

        private static void ValidateTransition(
            string stepId,
            string fieldName,
            string target,
            Dictionary<string, SkillPlanStepData> stepMap,
            List<string> errors)
        {
            if (target == CompleteTerminal || target == FailedTerminal)
                return;

            if (string.IsNullOrWhiteSpace(target))
            {
                errors.Add("Step '" + stepId + "' " + fieldName + " is required.");
                return;
            }

            if (!stepMap.ContainsKey(target))
            {
                errors.Add(
                    "Step '" + stepId + "' " + fieldName +
                    " target does not exist: " + target
                );
            }
        }

        private static void ValidateReachabilityAndCycles(
            SkillPlanData plan,
            Dictionary<string, SkillPlanStepData> stepMap,
            List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(plan.entryStepId) ||
                !stepMap.ContainsKey(plan.entryStepId))
            {
                return;
            }

            HashSet<string> reachable =
                new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> visiting =
                new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> visited =
                new HashSet<string>(StringComparer.Ordinal);

            bool hasCycle = Visit(
                plan.entryStepId,
                stepMap,
                reachable,
                visiting,
                visited
            );

            if (hasCycle)
                errors.Add("Cyclic Skill Plans are not supported in v0.1.");

            foreach (string stepId in stepMap.Keys)
            {
                if (!reachable.Contains(stepId))
                    errors.Add("Unreachable Step: " + stepId);
            }
        }

        private static bool Visit(
            string stepId,
            Dictionary<string, SkillPlanStepData> stepMap,
            HashSet<string> reachable,
            HashSet<string> visiting,
            HashSet<string> visited)
        {
            if (visited.Contains(stepId))
                return false;

            if (!visiting.Add(stepId))
                return true;

            reachable.Add(stepId);
            bool hasCycle = false;

            SkillPlanStepData step = stepMap[stepId];

            foreach (string next in EnumerateStepTargets(step))
            {
                if (!stepMap.ContainsKey(next))
                    continue;

                if (Visit(next, stepMap, reachable, visiting, visited))
                    hasCycle = true;
            }

            visiting.Remove(stepId);
            visited.Add(stepId);
            return hasCycle;
        }

        private static IEnumerable<string> EnumerateStepTargets(
            SkillPlanStepData step)
        {
            if (!IsTerminal(step.onSuccess))
                yield return step.onSuccess;

            if (!IsTerminal(step.onFailure) &&
                !string.Equals(
                    step.onFailure,
                    step.onSuccess,
                    StringComparison.Ordinal))
            {
                yield return step.onFailure;
            }
        }

        private static bool SupportsPlatform(
            string[] platforms,
            string platform)
        {
            if (platforms == null || platforms.Length == 0)
                return false;

            for (int i = 0; i < platforms.Length; i++)
            {
                string candidate = platforms[i];

                if (string.Equals(candidate, "Any", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(candidate, platform, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsTerminal(string value)
        {
            return value == CompleteTerminal || value == FailedTerminal;
        }

        private static string BuildSkillKey(string skillId, int version)
        {
            return (skillId ?? string.Empty) + "@" + version;
        }

        private static SkillPlanData ClonePlan(SkillPlanData source)
        {
            SkillPlanStepData[] sourceSteps =
                source.steps ?? Array.Empty<SkillPlanStepData>();
            SkillPlanStepData[] steps =
                new SkillPlanStepData[sourceSteps.Length];

            for (int i = 0; i < sourceSteps.Length; i++)
            {
                SkillPlanStepData step = sourceSteps[i];

                steps[i] = step == null
                    ? null
                    : new SkillPlanStepData
                    {
                        stepId = step.stepId,
                        skillId = step.skillId,
                        skillVersion = step.skillVersion,
                        inputFrom = step.inputFrom,
                        onSuccess = step.onSuccess,
                        onFailure = step.onFailure
                    };
            }

            return new SkillPlanData
            {
                schemaVersion = source.schemaVersion,
                planId = source.planId,
                version = source.version,
                modeId = source.modeId,
                displayName = source.displayName,
                goal = source.goal,
                source = source.source,
                entryStepId = source.entryStepId,
                maximumSteps = source.maximumSteps,
                maximumExecutionSeconds = source.maximumExecutionSeconds,
                steps = steps
            };
        }

        private static SkillDescriptorData CloneDescriptor(
            SkillDescriptorData source)
        {
            return new SkillDescriptorData
            {
                schemaVersion = source.schemaVersion,
                skillId = source.skillId,
                version = source.version,
                displayName = source.displayName,
                description = source.description,
                category = source.category,
                inputType = source.inputType,
                outputType = source.outputType,
                preconditions = CloneArray(source.preconditions),
                effects = CloneArray(source.effects),
                requiredPermissions = CloneArray(source.requiredPermissions),
                requiredResources = CloneArray(source.requiredResources),
                riskLevel = source.riskLevel,
                timeoutSeconds = source.timeoutSeconds,
                interruptible = source.interruptible,
                platformSupport = CloneArray(source.platformSupport),
                implementationHandlerId = source.implementationHandlerId
            };
        }

        private static string[] CloneArray(string[] values)
        {
            if (values == null || values.Length == 0)
                return Array.Empty<string>();

            string[] clone = new string[values.Length];
            Array.Copy(values, clone, values.Length);
            return clone;
        }
    }
}
