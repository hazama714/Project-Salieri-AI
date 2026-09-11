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
    public interface ISkillHandler
    {
        string SkillId { get; }

        int Version { get; }

        string InputPayloadTypeId { get; }

        string OutputPayloadTypeId { get; }

        Type InputPayloadType { get; }

        Type OutputPayloadType { get; }

        Task<SkillExecutionResult> ExecuteUntypedAsync(
            ISkillPayload input,
            SkillExecutionContext context
        );
    }
}
