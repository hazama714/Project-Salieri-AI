// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Core.Language.Compiler;
using SalieriAI.Core.Semantics.Referents;

namespace SalieriAI.Core.Reflex.Cognitive.Grounding
{
    /// <summary>
    /// Pure deterministic projection from an explicitly supplied referent
    /// snapshot into an immutable turn grounding.
    /// </summary>
    public sealed class ConversationGroundingService
    {
        public ConversationObjectGrounding Freeze(
            ConversationReferenceResolution resolution,
            string groundingId,
            string inputId,
            string interactionId,
            DateTime groundedAtUtc)
        {
            return Freeze(
                resolution,
                null,
                groundingId,
                inputId,
                interactionId,
                groundedAtUtc);
        }

        public ConversationObjectGrounding Freeze(
            ConversationReferenceResolution resolution,
            CurrentObjectReferent unspecifiedCurrentSnapshot,
            string groundingId,
            string inputId,
            string interactionId,
            DateTime groundedAtUtc)
        {
            ConversationReferenceResolution safe = resolution ??
                new ConversationReferenceResolution(
                    SemanticTargetRef.Unspecified,
                    ConversationReferenceResolutionStatus.Unspecified,
                    ConversationReferentSource.None,
                    null,
                    string.Empty,
                    "Reference resolution was not supplied.");

            ConversationObjectGroundingStatus status = MapStatus(safe.Status);
            if (safe.Status ==
                    ConversationReferenceResolutionStatus.ResolvedCurrent &&
                safe.SemanticTargetRef == SemanticTargetRef.This)
            {
                // Keep the frozen M5-C status contract for explicit
                // proximal 「これ／この」 while ReferentSource records v1.
                status = ConversationObjectGroundingStatus.ResolvedThis;
            }
            FrozenObjectReferentSnapshot frozen = safe.FrozenReferent;
            ConversationReferentSource source = safe.ReferentSource;
            string diagnostic = safe.Diagnostic;

            // Preserve the existing general-conversation prompt context only
            // for truly Unspecified text. It remains SnapshotAvailable rather
            // than a formal reference resolution. Missing/Ambiguous/Unsupported
            // results never receive this fallback.
            if (safe.Status ==
                    ConversationReferenceResolutionStatus.Unspecified &&
                unspecifiedCurrentSnapshot != null)
            {
                frozen = new FrozenObjectReferentSnapshot(
                    unspecifiedCurrentSnapshot);
                source = ConversationReferentSource.CurrentObjectReferent;
                status = ConversationObjectGroundingStatus.SnapshotAvailable;
                diagnostic =
                    "Current object snapshot is available but no linguistic " +
                    "reference was formally resolved.";
            }

            if (frozen != null && frozen.IsKnown &&
                !frozen.HasUsableKnownIdentity)
            {
                status =
                    ConversationObjectGroundingStatus.InvalidKnownProvenance;
                diagnostic =
                    "Known referent lacks KnownName or ExperienceRecordId.";
            }

            return new ConversationObjectGrounding(
                groundingId,
                inputId,
                interactionId,
                safe.SemanticTargetRef,
                status,
                source,
                frozen,
                groundedAtUtc,
                diagnostic);
        }

        public ConversationObjectGrounding Freeze(
            SemanticTargetRef targetRef,
            CurrentObjectReferent currentReferent,
            string groundingId,
            string inputId,
            string interactionId,
            DateTime groundedAtUtc)
        {
            FrozenObjectReferentSnapshot frozen = currentReferent != null
                ? new FrozenObjectReferentSnapshot(currentReferent)
                : null;

            ConversationObjectGroundingStatus status;
            string diagnostic = frozen != null
                ? frozen.DiagnosticError
                : "No current object referent was available at turn start.";

            if (targetRef == SemanticTargetRef.That)
            {
                status = ConversationObjectGroundingStatus.UnsupportedTarget;
                diagnostic =
                    "SemanticTargetRef.That has no distinct grounding evidence.";
            }
            else if (targetRef == SemanticTargetRef.This)
            {
                status = frozen != null
                    ? ConversationObjectGroundingStatus.ResolvedThis
                    : ConversationObjectGroundingStatus.UnresolvedThis;
            }
            else if (frozen == null)
            {
                status = ConversationObjectGroundingStatus.NoReferent;
            }
            else
            {
                status = ConversationObjectGroundingStatus.SnapshotAvailable;
            }

            if (frozen != null && frozen.IsKnown &&
                !frozen.HasUsableKnownIdentity)
            {
                status =
                    ConversationObjectGroundingStatus.InvalidKnownProvenance;
                diagnostic =
                    "Known referent lacks KnownName or ExperienceRecordId.";
            }

            return new ConversationObjectGrounding(
                groundingId,
                inputId,
                interactionId,
                targetRef,
                status,
                frozen,
                groundedAtUtc,
                diagnostic);
        }

        private static ConversationObjectGroundingStatus MapStatus(
            ConversationReferenceResolutionStatus status)
        {
            switch (status)
            {
                case ConversationReferenceResolutionStatus.ResolvedCurrent:
                    return ConversationObjectGroundingStatus.ResolvedCurrent;
                case ConversationReferenceResolutionStatus.ResolvedPrevious:
                    return ConversationObjectGroundingStatus.ResolvedPrevious;
                case ConversationReferenceResolutionStatus.MissingCurrent:
                    return ConversationObjectGroundingStatus.MissingCurrent;
                case ConversationReferenceResolutionStatus.MissingPrevious:
                    return ConversationObjectGroundingStatus.MissingPrevious;
                case ConversationReferenceResolutionStatus.Unsupported:
                    return ConversationObjectGroundingStatus.Unsupported;
                case ConversationReferenceResolutionStatus.Ambiguous:
                    return ConversationObjectGroundingStatus.Ambiguous;
                case ConversationReferenceResolutionStatus.NeedsClarification:
                    return ConversationObjectGroundingStatus.NeedsClarification;
                default:
                    return ConversationObjectGroundingStatus.Unspecified;
            }
        }
    }
}
