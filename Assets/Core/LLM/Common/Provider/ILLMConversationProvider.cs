// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System.Threading.Tasks;

namespace SalieriAI.Core.LLM.Common
{
    /// <summary>
    /// User conversation response provider.
    /// Cloud / Local implementations return the same speech + emotion DTO.
    /// </summary>
    public interface ILLMConversationProvider
    {
        Task<ConversationLLMResponse> GenerateConversationResponseAsync(
            string userText
        );

        Task<ConversationLLMResponse> GenerateConversationResponseAsync(
            ConversationGenerationRequest request
        );
    }
}
