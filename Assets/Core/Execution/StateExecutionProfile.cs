// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using SalieriAI.Core.State;

namespace SalieriAI.Core.Execution
{
    [System.Serializable]
    public sealed class StateExecutionProfile
    {
        public InteractionState state;

        public ExecutionLevel camera = ExecutionLevel.Normal;
        public ExecutionLevel faceTracking = ExecutionLevel.Normal;
        public ExecutionLevel servoTracking = ExecutionLevel.Normal;

        public ExecutionLevel llm = ExecutionLevel.Normal;
        public ExecutionLevel voice = ExecutionLevel.Normal;
        public ExecutionLevel expression = ExecutionLevel.Normal;
    }
}