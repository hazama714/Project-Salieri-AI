// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;

namespace SalieriAI.Core.Language.Compiler
{
    /// <summary>
    /// Immutable final SCC output passed to an execution runtime.
    /// Runtime begins at the point where this request is accepted or rejected.
    /// Constructing a SkillRequestIr does not invoke a Skill Handler.
    /// </summary>
    public sealed class SkillRequestIr
    {
        public string SkillRequestId { get; }
        public string GroundedIntentId { get; }
        public string InteractionId { get; }
        public string RequestedSkillId { get; }
        public int RequestedSkillVersion { get; }
        public IReadOnlyList<SemanticParameter> Parameters { get; }
        public DateTime RequestedAtUtc { get; }
        public DateTime ExpireAtUtc { get; }
        public string RequiredSectionId { get; }
        public bool RequiresPhysicalBody { get; }
        public string AuthorizationReferenceId { get; }

        public SkillRequestIr(
            string skillRequestId,
            string groundedIntentId,
            string interactionId,
            string requestedSkillId,
            int requestedSkillVersion,
            IEnumerable<SemanticParameter> parameters,
            DateTime requestedAtUtc,
            DateTime expireAtUtc,
            string requiredSectionId,
            bool requiresPhysicalBody,
            string authorizationReferenceId)
        {
            SkillRequestId = SccContractUtility.Text(skillRequestId);
            GroundedIntentId =
                SccContractUtility.Text(groundedIntentId);
            InteractionId = SccContractUtility.Text(interactionId);
            RequestedSkillId =
                SccContractUtility.Text(requestedSkillId);
            RequestedSkillVersion = requestedSkillVersion;
            Parameters = SccContractUtility.ReadOnlyCopy(parameters);
            RequestedAtUtc = SccContractUtility.Utc(requestedAtUtc);
            ExpireAtUtc = SccContractUtility.Utc(expireAtUtc);
            RequiredSectionId =
                SccContractUtility.Text(requiredSectionId);
            RequiresPhysicalBody = requiresPhysicalBody;
            AuthorizationReferenceId =
                SccContractUtility.Text(authorizationReferenceId);
        }
    }
}
