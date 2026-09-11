// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using UnityEngine;

namespace SalieriAI.Sensors.Audio
{
    internal interface IWindowsVoskMicrophoneSource : IDisposable
    {
        bool IsCapturing { get; }
        bool HasProducedSamples { get; }
        bool TryStart(out string error);
        void Pump(WindowsVoskPcmBuffer destination);
        void Stop();
    }

    internal sealed class WindowsUnityMicrophoneSource :
        IWindowsVoskMicrophoneSource
    {
        private const int RequestedFrequency = 16000;
        private const int ClipLengthSeconds = 2;

        private readonly string deviceName;
        private AudioClip clip;
        private int lastPosition;
        private bool hasProducedSamples;

        internal WindowsUnityMicrophoneSource(string deviceName = null)
        {
            this.deviceName = string.IsNullOrWhiteSpace(deviceName)
                ? null
                : deviceName;
        }

        public bool IsCapturing =>
            clip != null && Microphone.IsRecording(deviceName);

        public bool HasProducedSamples => hasProducedSamples;

        public bool TryStart(out string error)
        {
            error = string.Empty;
            if (IsCapturing)
                return true;

            try
            {
                hasProducedSamples = false;
                lastPosition = 0;
                clip = Microphone.Start(
                    deviceName,
                    true,
                    ClipLengthSeconds,
                    RequestedFrequency);
                if (clip == null)
                {
                    error = "Unity Microphone.Start returned null.";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                clip = null;
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        public void Pump(WindowsVoskPcmBuffer destination)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            if (clip == null || !Microphone.IsRecording(deviceName))
                return;

            int currentPosition = Microphone.GetPosition(deviceName);
            if (currentPosition < 0 || currentPosition == lastPosition)
                return;

            int totalFrames = clip.samples;
            if (totalFrames <= 0)
                return;

            if (currentPosition > lastPosition)
            {
                ReadFrames(lastPosition, currentPosition - lastPosition, destination);
            }
            else
            {
                ReadFrames(lastPosition, totalFrames - lastPosition, destination);
                if (currentPosition > 0)
                    ReadFrames(0, currentPosition, destination);
            }

            lastPosition = currentPosition;
            hasProducedSamples = true;
        }

        public void Stop()
        {
            if (clip == null)
                return;

            try
            {
                if (Microphone.IsRecording(deviceName))
                    Microphone.End(deviceName);
            }
            catch (Exception)
            {
                // Managed ownership is still invalidated below.
            }

            UnityEngine.Object.Destroy(clip);
            clip = null;
            lastPosition = 0;
            hasProducedSamples = false;
        }

        public void Dispose()
        {
            Stop();
        }

        private void ReadFrames(
            int offsetFrames,
            int frameCount,
            WindowsVoskPcmBuffer destination)
        {
            if (frameCount <= 0 || clip == null)
                return;

            int channels = Math.Max(1, clip.channels);
            float[] interleaved = new float[frameCount * channels];
            if (!clip.GetData(interleaved, offsetFrames))
                return;

            short[] converted = WindowsVoskPcmConverter.ConvertToMonoPcm16(
                interleaved,
                channels,
                clip.frequency);
            if (converted.Length > 0)
                destination.Write(converted, converted.Length);
        }
    }
}
