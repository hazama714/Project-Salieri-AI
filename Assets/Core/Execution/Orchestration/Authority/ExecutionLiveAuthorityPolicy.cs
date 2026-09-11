// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

/* Project Salieri AI - Apache-2.0 */
using System.Collections.Generic;
using SalieriAI.Core.Execution.Orchestration.Contracts;
using SalieriAI.Core.Execution.Orchestration.Runtime;

namespace SalieriAI.Core.Execution.Orchestration.Authority
{
    public static class ExecutionLiveAuthorityPolicy
    {
        public const string PolicyVersion = "execution-live-authority-v0.1";
        public const string CapabilityVersion = "execution-runtime-capability-v0.1";

        private static readonly string[] LogicalResources =
        {
            ExecutionResourceIds.SpeechOutput,
            ExecutionResourceIds.LlmGeneration,
            ExecutionResourceIds.VisionLight,
            ExecutionResourceIds.VisionHeavy,
            ExecutionResourceIds.HeadMotion,
            ExecutionResourceIds.ArmMotion,
            ExecutionResourceIds.CrawlerMotion,
            ExecutionResourceIds.VirtualBody,
            ExecutionResourceIds.PhysicalTransport
        };

        /// <summary>Explicit immutable v0.1 policy. No runtime mutation.</summary>
        public static ExecutionAuthorityPolicySnapshot CreateDefault()
        {
            return new ExecutionAuthorityPolicySnapshot(
                PolicyVersion,
                new[]
                {
                    Rule(ExecutionAuthorityDomain.Permission,
                        ExecutionAuthorityMode.LiveReadOnly,
                        new[] { ExecutionCoordinatorEffectIntentType.CheckPermission }),
                    Rule(ExecutionAuthorityDomain.ResourceProfile,
                        ExecutionAuthorityMode.LiveReadOnly,
                        new[] { ExecutionCoordinatorEffectIntentType.None }),
                    Rule(ExecutionAuthorityDomain.LogicalResourceLease,
                        ExecutionAuthorityMode.LiveDispatch,
                        new[]
                        {
                            ExecutionCoordinatorEffectIntentType.AcquireResourceLease,
                            ExecutionCoordinatorEffectIntentType.ReleaseResourceLease
                        }, resources: LogicalResources),
                    Rule(ExecutionAuthorityDomain.Speech,
                        ExecutionAuthorityMode.ShadowOnly,
                        new[] { ExecutionCoordinatorEffectIntentType.StartExecutor }),
                    Rule(ExecutionAuthorityDomain.Body,
                        ExecutionAuthorityMode.ShadowOnly,
                        new[] { ExecutionCoordinatorEffectIntentType.StartExecutor }),
                    Rule(ExecutionAuthorityDomain.Safety,
                        ExecutionAuthorityMode.ShadowOnly,
                        new[]
                        {
                            ExecutionCoordinatorEffectIntentType.RequestSafetyPreempt,
                            ExecutionCoordinatorEffectIntentType.RequestExecutorInterrupt,
                            ExecutionCoordinatorEffectIntentType.RequestExecutorCancel
                        })
                },
                true, "Phase 3D-0 default: only logical lease dispatch is live");
        }

        /// <summary>
        /// Static statement of capabilities established by Phase 3C. It does
        /// not query or mutate runtime services.
        /// </summary>
        public static ExecutionRuntimeCapabilitySnapshot
            CreateCurrentCapabilityFixture()
        {
            return new ExecutionRuntimeCapabilitySnapshot(
                CapabilityVersion,
                new[]
                {
                    Capability(ExecutionAuthorityDomain.Permission,
                        true, true, false,
                        ExecutionAuthorityVerificationLevel.ReadOnly),
                    Capability(ExecutionAuthorityDomain.ResourceProfile,
                        true, true, false,
                        ExecutionAuthorityVerificationLevel.ReadOnly),
                    Capability(ExecutionAuthorityDomain.LogicalResourceLease,
                        true, true, true,
                        ExecutionAuthorityVerificationLevel.LogicalOnly,
                        resources: LogicalResources,
                        diagnostic: "Pure logical ownership only; no physical runtime side effect"),
                    Capability(ExecutionAuthorityDomain.Speech,
                        true, true, true,
                        ExecutionAuthorityVerificationLevel.EffectUnverified,
                        actions: new[] { "ACK_SHORT" }),
                    Capability(ExecutionAuthorityDomain.Body,
                        true, true, true,
                        ExecutionAuthorityVerificationLevel.EffectUnverified,
                        actions: new[] { "LOOK_RIGHT_TEST" }),
                    Capability(ExecutionAuthorityDomain.Safety,
                        true, true, true,
                        ExecutionAuthorityVerificationLevel.EffectUnverified,
                        scopes: new ExecutionControlScope[0],
                        actions: new[] { "crawler_stop" },
                        diagnostic: "Current runtime capability is CrawlerOnly; Plan/Step/Resource safety scope is not proven")
                });
        }

        public static ExecutionAuthorityDomainRule Rule(
            ExecutionAuthorityDomain domain, ExecutionAuthorityMode mode,
            IEnumerable<ExecutionCoordinatorEffectIntentType> effects,
            IEnumerable<string> actions = null,
            IEnumerable<string> skills = null,
            IEnumerable<string> resources = null,
            IEnumerable<ExecutionControlScope> scopes = null)
        {
            return new ExecutionAuthorityDomainRule(domain, mode, effects,
                actions, skills, resources, scopes);
        }

        public static ExecutionRuntimeDomainCapability Capability(
            ExecutionAuthorityDomain domain, bool supported,
            bool readOnly, bool dispatch,
            ExecutionAuthorityVerificationLevel verification,
            IEnumerable<ExecutionControlScope> scopes = null,
            IEnumerable<string> actions = null,
            IEnumerable<string> resources = null,
            string diagnostic = "")
        {
            return new ExecutionRuntimeDomainCapability(domain, supported,
                readOnly, dispatch, verification, scopes, actions,
                resources, diagnostic);
        }
    }
}
