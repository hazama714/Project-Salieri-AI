// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Core.Language.Contracts;
using SalieriAI.Core.Language.Runtime;

namespace SalieriAI.Core.Language.Cgl
{
    /// <summary>
    /// Phase 2B observe-only comparison between CGL shadow output and the
    /// already-selected legacy ReactionDecision.
    /// </summary>
    public static class CglLegacyComparisonService
    {
        public static bool TryCompareLegacyDecision(
            ReactionDecision legacyDecision)
        {
            if (legacyDecision == null)
                return false;

            try
            {
                CglInterpretation interpretation;
                CglShadowTakeResult takeResult =
                    CglShadowResultStore.TryTake(
                        legacyDecision.InteractionId,
                        out interpretation
                    );

                if (takeResult == CglShadowTakeResult.Duplicate)
                    return false;

                InteractionTraceLogger.LogCglLegacyComparisonStarted(
                    legacyDecision
                );

                if (takeResult != CglShadowTakeResult.Found ||
                    interpretation == null)
                {
                    InteractionTraceLogger.LogCglLegacyComparisonUnavailable(
                        legacyDecision,
                        "shadow-result-not-found"
                    );
                    return false;
                }

                CglLegacyComparisonRecord record = Compare(
                    interpretation,
                    legacyDecision
                );

                InteractionTraceLogger.LogCglLegacyComparison(record);
                return true;
            }
            catch (Exception exception)
            {
                InteractionTraceLogger.LogCglLegacyComparisonFailed(
                    legacyDecision,
                    exception
                );
                return false;
            }
        }

        public static CglLegacyComparisonRecord Compare(
            CglInterpretation interpretation,
            ReactionDecision legacyDecision)
        {
            if (interpretation == null)
                throw new ArgumentNullException(nameof(interpretation));
            if (legacyDecision == null)
                throw new ArgumentNullException(nameof(legacyDecision));

            LegacyComparable legacy = NormalizeLegacy(legacyDecision);
            string comparisonResult;
            string mismatchReason;

            if (interpretation.Unknown ||
                interpretation.Unresolved ||
                interpretation.RequestType == CglRequestTypes.Unknown)
            {
                comparisonResult = CglLegacyComparisonResults.CglUnresolved;
                mismatchReason = "CGL request type is unresolved.";
            }
            else if (legacy.Route == CglRequestTypes.Unknown)
            {
                comparisonResult = CglLegacyComparisonResults.LegacyUnresolved;
                mismatchReason =
                    "Legacy route cannot be normalized. selectedRoute=" +
                    legacyDecision.SelectedRoute;
            }
            else if (interpretation.RequestType != legacy.Route)
            {
                comparisonResult = CglLegacyComparisonResults.RouteMismatch;
                mismatchReason =
                    "CGL route=" + interpretation.RequestType +
                    " legacy route=" + legacy.Route;
            }
            else if (legacy.Route == CglRequestTypes.Action &&
                     (string.IsNullOrWhiteSpace(
                          interpretation.ActionCandidate) ||
                      string.IsNullOrWhiteSpace(
                          legacy.ActionCandidate)))
            {
                comparisonResult =
                    CglLegacyComparisonResults.ComparisonUnavailable;
                mismatchReason =
                    "Action comparison data is incomplete. CGL action=" +
                    interpretation.ActionCandidate +
                    " legacy action=" + legacy.ActionCandidate;
            }
            else if (legacy.Route == CglRequestTypes.Action &&
                     !SameValue(
                         interpretation.ActionCandidate,
                         legacy.ActionCandidate))
            {
                comparisonResult = CglLegacyComparisonResults.ActionMismatch;
                mismatchReason =
                    "CGL action=" + interpretation.ActionCandidate +
                    " legacy action=" + legacy.ActionCandidate;
            }
            else if (legacy.Route == CglRequestTypes.Action &&
                     HasOnlyOneValue(
                         interpretation.DirectionCandidate,
                         legacy.Direction))
            {
                comparisonResult =
                    CglLegacyComparisonResults.ComparisonUnavailable;
                mismatchReason =
                    "Direction comparison data is incomplete. CGL direction=" +
                    interpretation.DirectionCandidate +
                    " legacy direction=" + legacy.Direction;
            }
            else if (legacy.Route == CglRequestTypes.Action &&
                     !BothEmpty(
                         interpretation.DirectionCandidate,
                         legacy.Direction) &&
                     !SameValue(
                         interpretation.DirectionCandidate,
                         legacy.Direction))
            {
                comparisonResult =
                    CglLegacyComparisonResults.DirectionMismatch;
                mismatchReason =
                    "CGL direction=" + interpretation.DirectionCandidate +
                    " legacy direction=" + legacy.Direction;
            }
            else
            {
                comparisonResult = CglLegacyComparisonResults.Match;
                mismatchReason = string.Empty;
            }

            return new CglLegacyComparisonRecord(
                interpretation.InputId,
                interpretation.InteractionId,
                interpretation.NormalizedText,
                interpretation.RequestType,
                interpretation.ActionCandidate,
                interpretation.DirectionCandidate,
                legacy.Route,
                legacy.ActionId,
                legacy.Direction,
                comparisonResult,
                mismatchReason,
                DateTime.UtcNow,
                interpretation.ParserVersion
            );
        }

        private static LegacyComparable NormalizeLegacy(
            ReactionDecision decision)
        {
            string selectedRoute = decision.SelectedRoute ?? string.Empty;
            string actionId = decision.ActionId ?? string.Empty;

            if (selectedRoute == ReactionRouteIds.Conversation)
                return LegacyComparable.RouteOnly(
                    CglRequestTypes.Conversation,
                    actionId);

            if (selectedRoute == ReactionRouteIds.ActivationAck)
                return LegacyComparable.RouteOnly(
                    CglRequestTypes.Activation,
                    actionId);

            if (selectedRoute == ReactionRouteIds.Clarification)
                return LegacyComparable.RouteOnly(
                    CglRequestTypes.Clarification,
                    actionId);

            if (selectedRoute == ReactionRouteIds.FixedBodyAction)
                return NormalizeFixedBodyAction(actionId);

            if (selectedRoute == ReactionRouteIds.ProgrammedAction ||
                selectedRoute == ReactionRouteIds.VrmAction)
            {
                return new LegacyComparable(
                    CglRequestTypes.Action,
                    NormalizeActionCandidate(actionId),
                    NormalizeDirection(
                        decision.DirectionCandidate,
                        actionId),
                    actionId
                );
            }

            return LegacyComparable.RouteOnly(
                CglRequestTypes.Unknown,
                actionId);
        }

        private static LegacyComparable NormalizeFixedBodyAction(
            string actionId)
        {
            switch (actionId)
            {
                case "emergency_stop":
                    return LegacyComparable.RouteOnly(
                        CglRequestTypes.Emergency,
                        actionId);

                case "crawler_stop":
                case "stop":
                    return LegacyComparable.RouteOnly(
                        CglRequestTypes.Stop,
                        actionId);

                case "crawler_turn_right_short":
                    return new LegacyComparable(
                        CglRequestTypes.Action,
                        "LOOK_DIRECTION",
                        "RIGHT",
                        actionId);

                case "crawler_turn_left_short":
                    return new LegacyComparable(
                        CglRequestTypes.Action,
                        "LOOK_DIRECTION",
                        "LEFT",
                        actionId);

                case "crawler_forward_short":
                    return new LegacyComparable(
                        CglRequestTypes.Action,
                        "MOVE",
                        "FORWARD",
                        actionId);

                case "crawler_back_short":
                    return new LegacyComparable(
                        CglRequestTypes.Action,
                        "MOVE",
                        "BACKWARD",
                        actionId);

                default:
                    return new LegacyComparable(
                        CglRequestTypes.Action,
                        NormalizeActionCandidate(actionId),
                        string.Empty,
                        actionId);
            }
        }

        private static string NormalizeActionCandidate(string actionId)
        {
            switch (actionId)
            {
                case "ReturnCenter":
                case "returnCenter":
                    return "LOOK_DIRECTION";

                case "LookAtUser":
                case "LookAtUserOrSearch":
                case "LookAround":
                case "lookAtUser":
                case "lookAtUserOrSearch":
                case "lookAround":
                    return "LOOK_TARGET";

                case "crawler_forward_short":
                case "crawler_back_short":
                    return "MOVE";

                default:
                    return actionId ?? string.Empty;
            }
        }

        private static string NormalizeDirection(
            string directionCandidate,
            string actionId)
        {
            if (!string.IsNullOrWhiteSpace(directionCandidate))
                return directionCandidate.Trim().ToUpperInvariant();

            if (actionId == "ReturnCenter" || actionId == "returnCenter")
                return "FORWARD";

            return string.Empty;
        }

        private static bool SameValue(string cgl, string legacy)
        {
            return string.Equals(
                (cgl ?? string.Empty).Trim(),
                (legacy ?? string.Empty).Trim(),
                StringComparison.Ordinal
            );
        }

        private static bool HasOnlyOneValue(string first, string second)
        {
            return string.IsNullOrWhiteSpace(first) !=
                   string.IsNullOrWhiteSpace(second);
        }

        private static bool BothEmpty(string first, string second)
        {
            return string.IsNullOrWhiteSpace(first) &&
                   string.IsNullOrWhiteSpace(second);
        }

        private struct LegacyComparable
        {
            public string Route;
            public string ActionCandidate;
            public string Direction;
            public string ActionId;

            public LegacyComparable(
                string route,
                string actionCandidate,
                string direction,
                string actionId)
            {
                Route = route ?? string.Empty;
                ActionCandidate = actionCandidate ?? string.Empty;
                Direction = direction ?? string.Empty;
                ActionId = actionId ?? string.Empty;
            }

            public static LegacyComparable RouteOnly(
                string route,
                string actionId)
            {
                return new LegacyComparable(
                    route,
                    string.Empty,
                    string.Empty,
                    actionId
                );
            }
        }
    }
}

