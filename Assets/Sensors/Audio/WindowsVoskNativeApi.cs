// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace SalieriAI.Sensors.Audio
{
    internal interface IWindowsVoskNativeApi
    {
        void SetLogLevel(int level);
        IntPtr CreateModel(string modelPath);
        void FreeModel(IntPtr model);
        IntPtr CreateRecognizer(IntPtr model, float sampleRate);
        int AcceptWaveform(IntPtr recognizer, short[] samples, int sampleCount);
        string GetResult(IntPtr recognizer);
        string GetPartialResult(IntPtr recognizer);
        string GetFinalResult(IntPtr recognizer);
        void FreeRecognizer(IntPtr recognizer);
    }

    internal sealed class WindowsVoskNativeApi : IWindowsVoskNativeApi
    {
        private const string LibraryName = "libvosk";

        public void SetLogLevel(int level)
        {
            NativeMethods.vosk_set_log_level(level);
        }

        public IntPtr CreateModel(string modelPath)
        {
            return NativeMethods.vosk_model_new(modelPath);
        }

        public void FreeModel(IntPtr model)
        {
            if (model != IntPtr.Zero)
                NativeMethods.vosk_model_free(model);
        }

        public IntPtr CreateRecognizer(IntPtr model, float sampleRate)
        {
            return NativeMethods.vosk_recognizer_new(model, sampleRate);
        }

        public int AcceptWaveform(
            IntPtr recognizer,
            short[] samples,
            int sampleCount)
        {
            return NativeMethods.vosk_recognizer_accept_waveform_s(
                recognizer,
                samples,
                sampleCount);
        }

        public string GetResult(IntPtr recognizer)
        {
            return Utf8PointerToString(
                NativeMethods.vosk_recognizer_result(recognizer));
        }

        public string GetPartialResult(IntPtr recognizer)
        {
            return Utf8PointerToString(
                NativeMethods.vosk_recognizer_partial_result(recognizer));
        }

        public string GetFinalResult(IntPtr recognizer)
        {
            return Utf8PointerToString(
                NativeMethods.vosk_recognizer_final_result(recognizer));
        }

        public void FreeRecognizer(IntPtr recognizer)
        {
            if (recognizer != IntPtr.Zero)
                NativeMethods.vosk_recognizer_free(recognizer);
        }

        private static string Utf8PointerToString(IntPtr value)
        {
            if (value == IntPtr.Zero)
                return string.Empty;

            int length = 0;
            while (Marshal.ReadByte(value, length) != 0)
                length++;

            if (length == 0)
                return string.Empty;

            byte[] bytes = new byte[length];
            Marshal.Copy(value, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }

        private static class NativeMethods
        {
            [DllImport(
                LibraryName,
                CallingConvention = CallingConvention.Cdecl,
                CharSet = CharSet.Ansi)]
            internal static extern void vosk_set_log_level(int level);

            [DllImport(
                LibraryName,
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern IntPtr vosk_model_new(
                [MarshalAs(UnmanagedType.LPUTF8Str)] string modelPath);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void vosk_model_free(IntPtr model);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
            internal static extern IntPtr vosk_recognizer_new(
                IntPtr model,
                float sampleRate);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int vosk_recognizer_accept_waveform_s(
                IntPtr recognizer,
                [In] short[] samples,
                int sampleCount);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
            internal static extern IntPtr vosk_recognizer_result(
                IntPtr recognizer);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
            internal static extern IntPtr vosk_recognizer_partial_result(
                IntPtr recognizer);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
            internal static extern IntPtr vosk_recognizer_final_result(
                IntPtr recognizer);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void vosk_recognizer_free(
                IntPtr recognizer);
        }
    }
}
