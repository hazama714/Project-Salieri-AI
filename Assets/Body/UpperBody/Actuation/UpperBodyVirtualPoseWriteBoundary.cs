// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Body.UpperBody.Shadow;

namespace SalieriAI.Body.UpperBody.Actuation
{
    /// <summary>
    /// Explicit, immutable selection made by the future actuation
    /// composition boundary. A behavior may request a key, but may not
    /// mutate the selected profile.
    /// </summary>
    public sealed class UpperBodyActuationProfileSelection
    {
        public string ProfileId { get; }
        public string ProfileVersion { get; }
        public UpperBodyProfileAuthority Authority { get; }

        private UpperBodyActuationProfileSelection(
            string profileId,
            string profileVersion,
            UpperBodyProfileAuthority authority)
        {
            ProfileId = profileId;
            ProfileVersion = profileVersion;
            Authority = authority;
        }

        public static bool TryCreate(
            string profileId,
            string profileVersion,
            out UpperBodyActuationProfileSelection selection,
            out string error)
        {
            selection = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(profileId) ||
                string.IsNullOrWhiteSpace(profileVersion))
            {
                error = "Explicit profile ID and version are required.";
                return false;
            }

            selection = new UpperBodyActuationProfileSelection(
                profileId.Trim(),
                profileVersion.Trim(),
                UpperBodyActuationOwnershipContract.ProfileAuthority);
            return true;
        }
    }

    /// <summary>
    /// Future canonical boundary between a pure solved-pose IR and a VRM
    /// writer. U0-5 defines the boundary only and provides no implementation.
    /// Physical retargeting is deliberately outside this interface.
    /// </summary>
    public interface IUpperBodyVirtualPoseWriter
    {
        UpperBodyVirtualPoseWriteResult Apply(
            UpperBodyVirtualPoseWriteRequest request);
    }

    public sealed class UpperBodyVirtualPoseWriteRequest
    {
        public UpperBodyShadowSolution Solution { get; }
        public UpperBodyActuationProfileSelection ProfileSelection { get; }

        private UpperBodyVirtualPoseWriteRequest(
            UpperBodyShadowSolution solution,
            UpperBodyActuationProfileSelection profileSelection)
        {
            Solution = solution;
            ProfileSelection = profileSelection;
        }

        public static bool TryCreate(
            UpperBodyShadowSolution solution,
            UpperBodyActuationProfileSelection profileSelection,
            out UpperBodyVirtualPoseWriteRequest request,
            out string error)
        {
            request = null;
            error = string.Empty;
            if (solution == null || !solution.Valid)
            {
                error = "A valid solved-pose IR is required.";
                return false;
            }
            if (profileSelection == null)
            {
                error = "An explicit profile selection is required.";
                return false;
            }
            if (!string.Equals(
                    solution.SolverProfileId,
                    profileSelection.ProfileId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    solution.SolverProfileVersion,
                    profileSelection.ProfileVersion,
                    StringComparison.Ordinal))
            {
                error = "Solved-pose and selected profile identity must match.";
                return false;
            }

            request = new UpperBodyVirtualPoseWriteRequest(
                solution, profileSelection);
            return true;
        }
    }

    /// <summary>
    /// Result contract for a later production writer. No U0-5 code creates
    /// a successful result because production writing is not enabled.
    /// </summary>
    public sealed class UpperBodyVirtualPoseWriteResult
    {
        public bool Applied { get; }
        public string ContractVersion { get; }
        public string Message { get; }

        private UpperBodyVirtualPoseWriteResult(
            bool applied,
            string message)
        {
            Applied = applied;
            ContractVersion =
                UpperBodyActuationOwnershipContract.ContractVersion;
            Message = message ?? string.Empty;
        }

        public static UpperBodyVirtualPoseWriteResult CreateDisabled() =>
            new UpperBodyVirtualPoseWriteResult(
                false,
                "Upper-body production writing is disabled in U0-5.");

        public static UpperBodyVirtualPoseWriteResult CreateRejected(
            string message) =>
            new UpperBodyVirtualPoseWriteResult(false, message);

        public static UpperBodyVirtualPoseWriteResult CreateApplied(
            string message) =>
            new UpperBodyVirtualPoseWriteResult(true, message);
    }
}
