// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

using UnityEngine;

namespace SalieriAI.Sensors.Audio
{
    [Serializable]
    internal sealed class WindowsVoskModelManifest
    {
        public string modelId;
        public string modelVersion;
        public string archiveAssetPath;
        public string topLevelDirectory;
        public string archiveSha256;
        public long archiveBytes;
        public long expandedBytes;
        public int fileCount;
        public string license;
        public string[] requiredPaths;

        internal static WindowsVoskModelManifest Read(string manifestPath)
        {
            if (string.IsNullOrWhiteSpace(manifestPath) ||
                !File.Exists(manifestPath))
            {
                throw new FileNotFoundException(
                    "Windows Vosk model manifest is missing.",
                    manifestPath);
            }

            string json = File.ReadAllText(manifestPath, Encoding.UTF8);
            WindowsVoskModelManifest manifest =
                JsonUtility.FromJson<WindowsVoskModelManifest>(json);
            manifest?.Validate();
            return manifest;
        }

        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(modelId) ||
                string.IsNullOrWhiteSpace(modelVersion) ||
                string.IsNullOrWhiteSpace(archiveAssetPath) ||
                string.IsNullOrWhiteSpace(topLevelDirectory) ||
                string.IsNullOrWhiteSpace(archiveSha256) ||
                archiveSha256.Length != 64 ||
                archiveBytes <= 0L ||
                expandedBytes <= 0L ||
                fileCount <= 0 ||
                requiredPaths == null ||
                requiredPaths.Length == 0)
            {
                throw new InvalidDataException(
                    "Windows Vosk model manifest is incomplete.");
            }
        }
    }

    internal sealed class WindowsVoskModelInstallResult
    {
        internal string ModelDirectory { get; }
        internal bool Extracted { get; }

        internal WindowsVoskModelInstallResult(
            string modelDirectory,
            bool extracted)
        {
            ModelDirectory = modelDirectory;
            Extracted = extracted;
        }
    }

    internal interface IWindowsVoskModelInstaller
    {
        WindowsVoskModelInstallResult InstallAndVerify(
            CancellationToken cancellationToken);
    }

    internal sealed class WindowsVoskModelInstaller :
        IWindowsVoskModelInstaller
    {
        private const string InstallMarker = ".installed-model.txt";
        private const int CopyBufferSize = 64 * 1024;

        private readonly WindowsVoskModelManifest manifest;
        private readonly string archivePath;
        private readonly string modelsRoot;

        internal WindowsVoskModelInstaller(
            WindowsVoskModelManifest manifest,
            string archivePath,
            string modelsRoot)
        {
            this.manifest = manifest ??
                throw new ArgumentNullException(nameof(manifest));
            this.archivePath = archivePath ??
                throw new ArgumentNullException(nameof(archivePath));
            this.modelsRoot = modelsRoot ??
                throw new ArgumentNullException(nameof(modelsRoot));
            manifest.Validate();
        }

        public WindowsVoskModelInstallResult InstallAndVerify(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateArchive(cancellationToken);

            string finalDirectory = Path.Combine(
                modelsRoot,
                manifest.modelId);
            if (IsValidInstalledModel(finalDirectory))
            {
                return new WindowsVoskModelInstallResult(
                    finalDirectory,
                    false);
            }

            Directory.CreateDirectory(modelsRoot);
            string staging = Path.Combine(
                modelsRoot,
                "." + manifest.modelId + ".staging-" +
                Guid.NewGuid().ToString("N"));
            string backup = Path.Combine(
                modelsRoot,
                "." + manifest.modelId + ".backup");

            try
            {
                DeleteDirectoryQuietly(staging);
                Directory.CreateDirectory(staging);
                ExtractArchive(staging, cancellationToken);
                VerifyExtractedModel(staging, cancellationToken);
                WriteMarker(staging);

                DeleteDirectoryQuietly(backup);
                if (Directory.Exists(finalDirectory))
                    Directory.Move(finalDirectory, backup);

                try
                {
                    Directory.Move(staging, finalDirectory);
                    DeleteDirectoryQuietly(backup);
                }
                catch
                {
                    if (Directory.Exists(backup) &&
                        !Directory.Exists(finalDirectory))
                    {
                        Directory.Move(backup, finalDirectory);
                    }
                    throw;
                }
            }
            catch
            {
                DeleteDirectoryQuietly(staging);
                throw;
            }

            if (!IsValidInstalledModel(finalDirectory))
            {
                throw new InvalidDataException(
                    "Installed Windows Vosk model failed verification.");
            }

            return new WindowsVoskModelInstallResult(
                finalDirectory,
                true);
        }

        private void ValidateArchive(CancellationToken cancellationToken)
        {
            if (!File.Exists(archivePath))
            {
                throw new FileNotFoundException(
                    "Windows Vosk model archive is missing.",
                    archivePath);
            }

            var info = new FileInfo(archivePath);
            if (info.Length != manifest.archiveBytes)
            {
                throw new InvalidDataException(
                    "Windows Vosk archive size mismatch. expected=" +
                    manifest.archiveBytes + " actual=" + info.Length);
            }

            using (FileStream stream = File.OpenRead(archivePath))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] buffer = new byte[CopyBufferSize];
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    sha.TransformBlock(buffer, 0, read, buffer, 0);
                }
                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                string actual = ToHex(sha.Hash);
                if (!string.Equals(
                        actual,
                        manifest.archiveSha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "Windows Vosk archive SHA-256 mismatch. expected=" +
                        manifest.archiveSha256 + " actual=" + actual);
                }
            }
        }

        private void ExtractArchive(
            string staging,
            CancellationToken cancellationToken)
        {
            string canonicalRoot =
                Path.GetFullPath(staging) + Path.DirectorySeparatorChar;

            using (FileStream stream = File.OpenRead(archivePath))
            using (var archive = new ZipArchive(
                stream,
                ZipArchiveMode.Read,
                false))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string name = entry.FullName.Replace('\\', '/');
                    string prefix = manifest.topLevelDirectory + "/";
                    if (!name.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException(
                            "Unexpected Windows Vosk ZIP path: " + name);
                    }

                    string relative = name.Substring(prefix.Length);
                    if (string.IsNullOrEmpty(relative))
                        continue;

                    string outputPath = Path.GetFullPath(
                        Path.Combine(
                            staging,
                            relative.Replace('/', Path.DirectorySeparatorChar)));
                    if (!outputPath.StartsWith(
                            canonicalRoot,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException(
                            "Windows Vosk ZIP traversal rejected: " + name);
                    }

                    if (name.EndsWith("/", StringComparison.Ordinal))
                    {
                        Directory.CreateDirectory(outputPath);
                        continue;
                    }

                    string parent = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(parent))
                        Directory.CreateDirectory(parent);

                    using (Stream input = entry.Open())
                    using (FileStream output = new FileStream(
                        outputPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None))
                    {
                        byte[] buffer = new byte[CopyBufferSize];
                        int read;
                        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            output.Write(buffer, 0, read);
                        }
                    }

                    if (entry.Length >= 0L &&
                        new FileInfo(outputPath).Length != entry.Length)
                    {
                        throw new InvalidDataException(
                            "Windows Vosk extracted size mismatch: " + name);
                    }
                }
            }
        }

        private bool IsValidInstalledModel(string directory)
        {
            try
            {
                if (!Directory.Exists(directory))
                    return false;

                string markerPath = Path.Combine(directory, InstallMarker);
                if (!File.Exists(markerPath))
                    return false;

                string expectedMarker = BuildMarker();
                if (!string.Equals(
                        File.ReadAllText(markerPath, Encoding.UTF8),
                        expectedMarker,
                        StringComparison.Ordinal))
                {
                    return false;
                }

                VerifyExtractedModel(directory, CancellationToken.None);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void VerifyExtractedModel(
            string directory,
            CancellationToken cancellationToken)
        {
            string[] files = Directory.GetFiles(
                directory,
                "*",
                SearchOption.AllDirectories);
            var relativePaths = new HashSet<string>(
                StringComparer.Ordinal);
            long totalBytes = 0L;

            foreach (string file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string relative = file.Substring(directory.Length)
                    .TrimStart(Path.DirectorySeparatorChar)
                    .Replace(Path.DirectorySeparatorChar, '/');
                if (string.Equals(
                        relative,
                        InstallMarker,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                relativePaths.Add(relative);
                totalBytes += new FileInfo(file).Length;
            }

            if (relativePaths.Count != manifest.fileCount)
            {
                throw new InvalidDataException(
                    "Windows Vosk model file count mismatch. expected=" +
                    manifest.fileCount + " actual=" + relativePaths.Count);
            }
            if (totalBytes != manifest.expandedBytes)
            {
                throw new InvalidDataException(
                    "Windows Vosk expanded byte count mismatch. expected=" +
                    manifest.expandedBytes + " actual=" + totalBytes);
            }

            foreach (string required in manifest.requiredPaths)
            {
                if (!relativePaths.Contains(required))
                {
                    throw new InvalidDataException(
                        "Windows Vosk required model path is missing: " +
                        required);
                }
            }
        }

        private void WriteMarker(string directory)
        {
            File.WriteAllText(
                Path.Combine(directory, InstallMarker),
                BuildMarker(),
                new UTF8Encoding(false));
        }

        private string BuildMarker()
        {
            return manifest.modelId + "\n" +
                   manifest.modelVersion + "\n" +
                   manifest.archiveSha256.ToUpperInvariant() + "\n" +
                   manifest.fileCount + "\n" +
                   manifest.expandedBytes + "\n";
        }

        private static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes)
                builder.Append(value.ToString("X2"));
            return builder.ToString();
        }

        private static void DeleteDirectoryQuietly(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch
            {
                // A later validation reports any retained invalid directory.
            }
        }
    }
}
