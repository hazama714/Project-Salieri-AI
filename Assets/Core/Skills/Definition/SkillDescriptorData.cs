// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

namespace SalieriAI.Core.Skills
{
    [Serializable]
    public sealed class SkillDescriptorData
    {
        public int schemaVersion = 1;
        public string skillId = string.Empty;
        public int version = 1;
        public string displayName = string.Empty;
        public string description = string.Empty;
        public string category = string.Empty;
        public string inputType = string.Empty;
        public string outputType = string.Empty;
        public string[] preconditions = Array.Empty<string>();
        public string[] effects = Array.Empty<string>();
        public string[] requiredPermissions = Array.Empty<string>();
        public string[] requiredResources = Array.Empty<string>();
        public string riskLevel = string.Empty;
        public float timeoutSeconds;
        public bool interruptible = true;
        public string[] platformSupport = Array.Empty<string>();
        public string implementationHandlerId = string.Empty;
    }
}
