// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Sensors.Audio
{
    public enum STTRuntimePlatform
    {
        Unsupported = 0,
        Android = 10,
        WindowsEditor = 20,
        WindowsStandalone = 30
    }

    public enum STTProviderKind
    {
        Unavailable = 0,
        Android = 10,
        WindowsDictation = 20,
        WindowsVosk = 30
    }

    public enum STTStartDisposition
    {
        Rejected = 0,
        ControlAccepted = 10,
        RecognizerStarted = 20,
        AlreadyRunning = 30
    }

    public sealed class STTStartResult
    {
        public STTStartDisposition Disposition { get; }
        public STTProviderKind ProviderKind { get; }
        public string Error { get; }

        public bool Accepted =>
            Disposition != STTStartDisposition.Rejected;

        public bool RecognizerActuallyStarted =>
            Disposition == STTStartDisposition.RecognizerStarted ||
            Disposition == STTStartDisposition.AlreadyRunning;

        private STTStartResult(
            STTStartDisposition disposition,
            STTProviderKind providerKind,
            string error)
        {
            Disposition = disposition;
            ProviderKind = providerKind;
            Error = error ?? string.Empty;
        }

        public static STTStartResult ControlAccepted(
            STTProviderKind providerKind)
        {
            return new STTStartResult(
                STTStartDisposition.ControlAccepted,
                providerKind,
                string.Empty);
        }

        public static STTStartResult RecognizerStarted(
            STTProviderKind providerKind)
        {
            return new STTStartResult(
                STTStartDisposition.RecognizerStarted,
                providerKind,
                string.Empty);
        }

        public static STTStartResult AlreadyRunning(
            STTProviderKind providerKind)
        {
            return new STTStartResult(
                STTStartDisposition.AlreadyRunning,
                providerKind,
                string.Empty);
        }

        public static STTStartResult Rejected(
            STTProviderKind providerKind,
            string error)
        {
            return new STTStartResult(
                STTStartDisposition.Rejected,
                providerKind,
                error);
        }
    }

    public static class STTProviderResolver
    {
        public static STTRuntimePlatform CurrentPlatform
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return STTRuntimePlatform.Android;
#elif UNITY_EDITOR_WIN
                return STTRuntimePlatform.WindowsEditor;
#elif UNITY_STANDALONE_WIN
                return STTRuntimePlatform.WindowsStandalone;
#else
                return STTRuntimePlatform.Unsupported;
#endif
            }
        }

        public static STTProviderKind ResolveProviderKind(
            STTRuntimePlatform platform)
        {
            switch (platform)
            {
                case STTRuntimePlatform.Android:
                    return STTProviderKind.Android;

                case STTRuntimePlatform.WindowsEditor:
                case STTRuntimePlatform.WindowsStandalone:
                    return STTProviderKind.WindowsVosk;

                case STTRuntimePlatform.Unsupported:
                default:
                    return STTProviderKind.Unavailable;
            }
        }

        public static STTService CreateForCurrentPlatform()
        {
            return CreateForProvider(ResolveProviderKind(CurrentPlatform));
        }

        public static STTService CreateForProvider(STTProviderKind providerKind)
        {
            switch (providerKind)
            {
                case STTProviderKind.Android:
                    return new AndroidSTTService();

                case STTProviderKind.WindowsVosk:
                    return new WindowsVoskSTTService();

                case STTProviderKind.WindowsDictation:
                    return new WindowsEditorSTTService();

                default:
                    return null;
            }
        }
    }
}
