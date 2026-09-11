// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

/* Project Salieri AI - Apache-2.0 */
using System;

/// <summary>
/// Immutable response transport envelope. Empty correlation fields represent
/// a legacy/non-Conversation response and remain fully supported.
/// </summary>
public sealed class ResponseEnvelope
{
    public string Text { get; }
    public string OutputId { get; }
    public string TurnId { get; }
    public string InteractionId { get; }

    public bool HasConversationCorrelation =>
        OutputId.Length > 0 &&
        TurnId.Length > 0 &&
        InteractionId.Length > 0;

    public ResponseEnvelope(
        string text,
        string outputId,
        string turnId,
        string interactionId)
    {
        Text = text == null ? string.Empty : text.Trim();
        OutputId = Normalize(outputId);
        TurnId = Normalize(turnId);
        InteractionId = Normalize(interactionId);
    }

    public static ResponseEnvelope Uncorrelated(string text)
    {
        return new ResponseEnvelope(
            text,
            string.Empty,
            string.Empty,
            string.Empty);
    }

    /// <summary>
    /// Carries a non-Conversation runtime correlation in OutputId.  Empty
    /// TurnId/InteractionId deliberately keep this outside normal Conversation
    /// turn lifecycle and working memory.
    /// </summary>
    public static ResponseEnvelope OneShotAcknowledgement(
        string text,
        string outcomeId)
    {
        return new ResponseEnvelope(
            text,
            outcomeId,
            string.Empty,
            string.Empty);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }
}
