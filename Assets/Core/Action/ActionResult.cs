// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Action
{
    public sealed class ActionResult
    {
        public bool Success { get; }
        public string ActionId { get; }
        public string Message { get; }

        public ActionResult(bool success, string actionId, string message)
        {
            Success = success;
            ActionId = actionId;
            Message = message;
        }

        public static ActionResult Ok(string actionId, string message)
        {
            return new ActionResult(true, actionId, message);
        }

        public static ActionResult Failed(string actionId, string message)
        {
            return new ActionResult(false, actionId, message);
        }
    }
}