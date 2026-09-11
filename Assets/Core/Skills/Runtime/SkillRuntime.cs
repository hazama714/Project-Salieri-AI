// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Threading.Tasks;

namespace SalieriAI.Core.Skills
{
    /// <summary>
    /// Resolves one explicitly identified Skill through the production registry
    /// and invokes its registered Handler.
    ///
    /// Multi-step control remains the responsibility of SkillPlanRunner.
    /// Physical side-effect admission remains outside this boundary.
    /// </summary>
    public sealed class SkillRuntime
    {
        private readonly SkillImplementationRegistry implementationRegistry;

        public int RegisteredSkillCount =>
            implementationRegistry != null
                ? implementationRegistry.Count
                : 0;

        public SkillRuntime(
            SkillImplementationRegistry implementationRegistry)
        {
            this.implementationRegistry = implementationRegistry;
        }

        public bool IsRegistered(string skillId, int version)
        {
            return implementationRegistry != null &&
                implementationRegistry.TryGet(
                    skillId,
                    version,
                    out _);
        }

        public async Task<SkillExecutionResult> ExecuteAsync(
            string skillId,
            int version,
            ISkillPayload input,
            SkillExecutionContext context)
        {
            DateTime startedAt = DateTime.UtcNow;

            if (implementationRegistry == null)
            {
                return SkillExecutionResult.Failed(
                    "skill_registry_unavailable",
                    "Skill Implementation Registry is unavailable.",
                    startedAt);
            }

            if (string.IsNullOrWhiteSpace(skillId) || version <= 0)
            {
                return SkillExecutionResult.InvalidInput(
                    "Skill ID and positive version are required.",
                    startedAt);
            }

            if (!implementationRegistry.TryGet(
                    skillId,
                    version,
                    out ISkillHandler handler))
            {
                return SkillExecutionResult.Failed(
                    "skill_not_registered",
                    "Skill Handler is not registered: " +
                    skillId.Trim() + "@" + version,
                    startedAt);
            }

            if (context == null)
            {
                return SkillExecutionResult.InvalidInput(
                    "Skill Execution Context is required.",
                    startedAt);
            }

            return await handler.ExecuteUntypedAsync(input, context);
        }
    }
}
