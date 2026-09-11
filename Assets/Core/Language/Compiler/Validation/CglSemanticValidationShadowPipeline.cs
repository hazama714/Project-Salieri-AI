// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SalieriAI.Core.Language.Compiler.Validation
{
    /// <summary>
    /// Result of one shadow-only Set validation and store attempt.
    /// It never selects a Candidate or changes production routing.
    /// </summary>
    public sealed class CglSemanticValidationShadowBatchResult
    {
        public CglSemanticCandidateSetValidationResult SetResult
        {
            get;
        }
        public IReadOnlyList<CglSemanticValidationShadowSnapshot>
            Snapshots { get; }
        public IReadOnlyList<
            CglSemanticValidationShadowRegisterResult>
            RegisterResults { get; }

        internal CglSemanticValidationShadowBatchResult(
            CglSemanticCandidateSetValidationResult setResult,
            IEnumerable<CglSemanticValidationShadowSnapshot> snapshots,
            IEnumerable<CglSemanticValidationShadowRegisterResult>
                registerResults)
        {
            SetResult = setResult;
            Snapshots = new ReadOnlyCollection<
                CglSemanticValidationShadowSnapshot>(
                    snapshots != null
                        ? new List<
                            CglSemanticValidationShadowSnapshot>(
                                snapshots)
                        : new List<
                            CglSemanticValidationShadowSnapshot>()
                );
            RegisterResults = new ReadOnlyCollection<
                CglSemanticValidationShadowRegisterResult>(
                    registerResults != null
                        ? new List<
                            CglSemanticValidationShadowRegisterResult>(
                                registerResults)
                        : new List<
                            CglSemanticValidationShadowRegisterResult>()
                );
        }
    }

    /// <summary>
    /// Shadow-only composition of pure validation and observation storage.
    /// </summary>
    public static class CglSemanticValidationShadowPipeline
    {
        public static CglSemanticValidationShadowBatchResult Run(
            SemanticCandidateSet candidateSet)
        {
            DateTime checkedAtUtc = DateTime.UtcNow;
            CglSemanticValidationContext setContext =
                CreateContext("set", checkedAtUtc);

            return Run(
                candidateSet,
                setContext,
                candidate => CreateContext(
                    candidate != null
                        ? candidate.CandidateId
                        : "null-candidate",
                    checkedAtUtc
                ),
                CglSemanticValidator.ValidatorVersion
            );
        }

        public static CglSemanticValidationShadowBatchResult Run(
            SemanticCandidateSet candidateSet,
            CglSemanticValidationContext setContext,
            Func<SemanticIrCandidate, CglSemanticValidationContext>
                candidateContextFactory,
            string validatorVersion)
        {
            if (setContext == null)
                throw new ArgumentNullException("setContext");

            if (candidateContextFactory == null)
            {
                throw new ArgumentNullException(
                    "candidateContextFactory");
            }

            if (string.IsNullOrWhiteSpace(validatorVersion))
            {
                throw new ArgumentException(
                    "Validator version is required.",
                    "validatorVersion");
            }

            CglSemanticCandidateSetValidationResult setResult =
                CglSemanticValidator.ValidateSetDetailed(
                    candidateSet,
                    candidateContextFactory
                );

            List<CglSemanticValidationShadowSnapshot> snapshots =
                new List<CglSemanticValidationShadowSnapshot>();
            List<CglSemanticValidationShadowRegisterResult> results =
                new List<
                    CglSemanticValidationShadowRegisterResult>();

            CglSemanticValidationShadowSnapshot setSnapshot =
                CglSemanticValidationShadowSnapshot.FromCandidateSet(
                    candidateSet,
                    setResult,
                    setContext,
                    validatorVersion
                );
            Register(snapshots, results, setSnapshot);

            for (
                int i = 0;
                i < setResult.CandidateRecords.Count;
                i++)
            {
                CglSemanticValidationShadowSnapshot candidateSnapshot =
                    CglSemanticValidationShadowSnapshot.FromCandidate(
                        candidateSet,
                        setResult.CandidateRecords[i],
                        validatorVersion
                    );
                Register(snapshots, results, candidateSnapshot);
            }

            return new CglSemanticValidationShadowBatchResult(
                setResult,
                snapshots,
                results
            );
        }

        private static void Register(
            ICollection<CglSemanticValidationShadowSnapshot> snapshots,
            ICollection<CglSemanticValidationShadowRegisterResult>
                results,
            CglSemanticValidationShadowSnapshot snapshot)
        {
            snapshots.Add(snapshot);
            results.Add(
                CglSemanticValidationShadowStore.Register(snapshot)
            );
        }

        private static CglSemanticValidationContext CreateContext(
            string scope,
            DateTime checkedAtUtc)
        {
            return new CglSemanticValidationContext(
                "validation_" +
                scope +
                "_" +
                Guid.NewGuid().ToString("N"),
                checkedAtUtc
            );
        }
    }
}
