// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Execution.Orchestration.Contracts
{
    public sealed class ExecutionResourceRequirement
    {
        public string ResourceId { get; }
        public ExecutionResourceAccessMode AccessMode { get; }
        public ExecutionRequiredness Requiredness { get; }
        public ExecutionResourceLeaseScope LeaseScope { get; }
        public int AcquisitionOrderHint { get; }
        public ExecutionResourceReleasePolicy ReleasePolicy { get; }

        public ExecutionResourceRequirement(
            string resourceId,
            ExecutionResourceAccessMode accessMode,
            ExecutionRequiredness requiredness,
            ExecutionResourceLeaseScope leaseScope,
            int acquisitionOrderHint,
            ExecutionResourceReleasePolicy releasePolicy)
        {
            ResourceId = ExecutionContractUtility.Text(resourceId);
            AccessMode = accessMode;
            Requiredness = requiredness;
            LeaseScope = leaseScope;
            AcquisitionOrderHint = acquisitionOrderHint;
            ReleasePolicy = releasePolicy;
        }
    }
}
