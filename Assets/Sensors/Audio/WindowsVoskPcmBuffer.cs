// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

namespace SalieriAI.Sensors.Audio
{
    internal enum WindowsVoskPcmOverflowPolicy
    {
        DropOldest = 10
    }

    internal sealed class WindowsVoskPcmBuffer
    {
        private readonly object sync = new object();
        private readonly short[] samples;
        private int readIndex;
        private int count;
        private long droppedSampleCount;

        internal WindowsVoskPcmBuffer(int capacitySamples)
        {
            if (capacitySamples <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacitySamples));

            samples = new short[capacitySamples];
        }

        internal int CapacitySamples => samples.Length;
        internal WindowsVoskPcmOverflowPolicy OverflowPolicy =>
            WindowsVoskPcmOverflowPolicy.DropOldest;

        internal int Count
        {
            get
            {
                lock (sync)
                    return count;
            }
        }

        internal long DroppedSampleCount
        {
            get
            {
                lock (sync)
                    return droppedSampleCount;
            }
        }

        internal void Write(short[] source, int sourceCount)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (sourceCount < 0 || sourceCount > source.Length)
                throw new ArgumentOutOfRangeException(nameof(sourceCount));

            lock (sync)
            {
                int sourceOffset = 0;
                if (sourceCount > samples.Length)
                {
                    int discardedIncoming = sourceCount - samples.Length;
                    sourceOffset = discardedIncoming;
                    sourceCount = samples.Length;
                    droppedSampleCount += discardedIncoming;
                }

                int overflow = Math.Max(0, count + sourceCount - samples.Length);
                if (overflow > 0)
                {
                    readIndex = (readIndex + overflow) % samples.Length;
                    count -= overflow;
                    droppedSampleCount += overflow;
                }

                int writeIndex = (readIndex + count) % samples.Length;
                int first = Math.Min(sourceCount, samples.Length - writeIndex);
                Array.Copy(source, sourceOffset, samples, writeIndex, first);
                int remainder = sourceCount - first;
                if (remainder > 0)
                {
                    Array.Copy(
                        source,
                        sourceOffset + first,
                        samples,
                        0,
                        remainder);
                }

                count += sourceCount;
            }
        }

        internal bool TryRead(short[] destination, out int readCount)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            lock (sync)
            {
                readCount = Math.Min(destination.Length, count);
                if (readCount <= 0)
                    return false;

                int first = Math.Min(readCount, samples.Length - readIndex);
                Array.Copy(samples, readIndex, destination, 0, first);
                int remainder = readCount - first;
                if (remainder > 0)
                    Array.Copy(samples, 0, destination, first, remainder);

                readIndex = (readIndex + readCount) % samples.Length;
                count -= readCount;
                return true;
            }
        }

        internal void Clear()
        {
            lock (sync)
            {
                readIndex = 0;
                count = 0;
            }
        }
    }

    internal static class WindowsVoskPcmConverter
    {
        internal const int TargetSampleRate = 16000;

        internal static short[] ConvertToMonoPcm16(
            float[] interleaved,
            int channels,
            int sourceSampleRate)
        {
            if (interleaved == null || interleaved.Length == 0)
                return Array.Empty<short>();
            if (channels <= 0 || sourceSampleRate <= 0)
                throw new ArgumentOutOfRangeException();

            int sourceFrames = interleaved.Length / channels;
            if (sourceFrames <= 0)
                return Array.Empty<short>();

            int targetFrames = Math.Max(
                1,
                (int)Math.Floor(
                    sourceFrames *
                    (double)TargetSampleRate /
                    sourceSampleRate));
            short[] result = new short[targetFrames];

            for (int targetIndex = 0; targetIndex < targetFrames; targetIndex++)
            {
                double sourcePosition =
                    targetIndex *
                    (double)sourceSampleRate /
                    TargetSampleRate;
                int sourceFrame = Math.Min(
                    sourceFrames - 1,
                    (int)Math.Floor(sourcePosition));
                int offset = sourceFrame * channels;
                double mono = 0.0;
                for (int channel = 0; channel < channels; channel++)
                    mono += interleaved[offset + channel];
                mono /= channels;
                mono = Math.Max(-1.0, Math.Min(1.0, mono));
                result[targetIndex] = (short)Math.Round(
                    mono < 0.0 ? mono * 32768.0 : mono * 32767.0);
            }

            return result;
        }
    }
}
