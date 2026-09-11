// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace SalieriAI.Expression.Voice.Synthesis
{
    /// <summary>
    /// Production-only owner of the AR1-B synthesis orchestrator. It adds no
    /// queue beyond the admission service's one active and one pending slot,
    /// and it owns neither playback nor WAV deletion after artifact handoff.
    /// </summary>
    public sealed class TTSSynthesisProductionVoiceRuntime
    {
        private readonly TTSSynthesisWaveOrchestrator orchestrator;

        public event Action<string, string, TTSSynthesisAudioArtifact>
            ArtifactHandedOff;

        public event Action<string, string, TTSSynthesisAudioArtifact>
            ShutdownArtifactCleanupRequired;

        public TTSSynthesisProductionVoiceRuntime(
            ITTSSynthesisBackendFactory backendFactory,
            Func<DateTime> utcNow,
            int designatedMainThreadId)
        {
            TTSSynthesisPendingPolicy policy =
                new TTSSynthesisPendingPolicy(
                    1,
                    1,
                    TTSSynthesisQueueDiscipline.BoundedFifo,
                    TTSSynthesisOverflowBehavior.RejectIncoming);
            TTSSynthesisWaveAdmission admission =
                new TTSSynthesisWaveAdmission(policy);

            orchestrator = new TTSSynthesisWaveOrchestrator(
                admission,
                backendFactory,
                utcNow,
                designatedMainThreadId,
                OnArtifactHandedOff,
                OnShutdownArtifactCleanupRequired);
        }

        public TTSSynthesisWaveOrchestrator Orchestrator => orchestrator;
        public TTSSynthesisWaveAdmission Admission => orchestrator.Admission;
        public Task<TTSSynthesisBackgroundWorkResult> ActiveCompletionTask =>
            orchestrator.ActiveCompletionTask;

        public TTSSynthesisAdmissionResult Submit(
            TTSSynthesisWaveRequest request,
            DateTime admittedAtUtc,
            CancellationToken managedCancellation)
        {
            return orchestrator.Submit(
                request,
                admittedAtUtc,
                managedCancellation);
        }

        public TTSSynthesisOrchestrationCycleResult PumpCompletedActive(
            DateTime terminalAtUtc,
            DateTime releasedAtUtc)
        {
            return orchestrator.PumpCompletedActive(
                terminalAtUtc,
                releasedAtUtc);
        }

        public TTSSynthesisShutdownRequestResult RequestShutdown(
            DateTime requestedAtUtc)
        {
            return orchestrator.RequestShutdown(requestedAtUtc);
        }

        public bool TryDispose()
        {
            return orchestrator.TryDispose();
        }

        private void OnArtifactHandedOff(
            string waveId,
            string requestId,
            TTSSynthesisAudioArtifact artifact)
        {
            ArtifactHandedOff?.Invoke(waveId, requestId, artifact);
        }

        private void OnShutdownArtifactCleanupRequired(
            string waveId,
            string requestId,
            TTSSynthesisAudioArtifact artifact)
        {
            ShutdownArtifactCleanupRequired?.Invoke(
                waveId,
                requestId,
                artifact);
        }
    }
}
