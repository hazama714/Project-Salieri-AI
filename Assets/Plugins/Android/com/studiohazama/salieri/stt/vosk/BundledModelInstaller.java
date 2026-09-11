// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

package com.studiohazama.salieri.stt.vosk;

import android.content.Context;
import android.content.res.AssetManager;
import android.os.StatFs;
import android.os.SystemClock;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import java.io.BufferedInputStream;
import java.io.BufferedOutputStream;
import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.nio.charset.StandardCharsets;
import java.nio.file.AtomicMoveNotSupportedException;
import java.nio.file.Files;
import java.nio.file.StandardCopyOption;
import java.security.DigestInputStream;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Locale;
import java.util.Set;
import java.util.UUID;
import java.util.zip.CRC32;
import java.util.zip.ZipEntry;
import java.util.zip.ZipException;
import java.util.zip.ZipInputStream;

final class BundledModelInstaller {
    private static final String INSTALL_MARKER = ".installed-model.json";
    private static final long STORAGE_MARGIN_BYTES = 32L * 1024L * 1024L;
    private static final int BUFFER_SIZE = 64 * 1024;

    private final Context appContext;
    private final AssetManager assets;
    private final String manifestAssetPath;

    BundledModelInstaller(Context context, String manifestAssetPath) {
        this.appContext = context.getApplicationContext();
        this.assets = appContext.getAssets();
        this.manifestAssetPath = manifestAssetPath;
    }

    Result installAndVerify() throws ModelInstallException {
        Manifest manifest = readManifest();
        File modelsRoot = new File(appContext.getFilesDir(), "vosk/models");
        File finalDirectory = new File(modelsRoot, manifest.modelId);

        long verifyStart = SystemClock.elapsedRealtime();
        String actualArchiveSha = sha256Asset(manifest.archiveAssetPath);
        long archiveVerifyMs = SystemClock.elapsedRealtime() - verifyStart;
        if (!manifest.archiveSha256.equalsIgnoreCase(actualArchiveSha)) {
            throw new ModelInstallException(
                    "model_archive_hash_mismatch",
                    "Expected " + manifest.archiveSha256 + " but found " + actualArchiveSha);
        }

        if (isValidInstalledModel(finalDirectory, manifest)) {
            return new Result(
                    finalDirectory,
                    manifest,
                    false,
                    archiveVerifyMs,
                    0L);
        }

        ensureAvailableStorage(modelsRoot, manifest.expandedBytes);
        if (!modelsRoot.exists() && !modelsRoot.mkdirs()) {
            throw new ModelInstallException(
                    "model_extract_failed",
                    "Unable to create model root: " + modelsRoot);
        }

        File stagingDirectory =
                new File(modelsRoot, "." + manifest.modelId + ".staging-" + UUID.randomUUID());
        long extractStart = SystemClock.elapsedRealtime();
        try {
            deleteRecursively(stagingDirectory);
            if (!stagingDirectory.mkdirs()) {
                throw new IOException("Unable to create staging directory.");
            }
            extractArchiveToStaging(manifest, stagingDirectory);
            verifyExtractedModel(stagingDirectory, manifest);
            writeInstallMarker(stagingDirectory, manifest);
            replaceInstalledDirectory(modelsRoot, stagingDirectory, finalDirectory);
        } catch (ModelInstallException ex) {
            deleteRecursivelyQuietly(stagingDirectory);
            throw ex;
        } catch (ZipException ex) {
            deleteRecursivelyQuietly(stagingDirectory);
            throw new ModelInstallException("model_integrity_failed", ex.getMessage(), ex);
        } catch (IOException ex) {
            deleteRecursivelyQuietly(stagingDirectory);
            throw new ModelInstallException("model_extract_failed", ex.getMessage(), ex);
        }

        long extractionMs = SystemClock.elapsedRealtime() - extractStart;
        if (!isValidInstalledModel(finalDirectory, manifest)) {
            throw new ModelInstallException(
                    "model_integrity_failed",
                    "Installed model failed final integrity validation.");
        }

        return new Result(
                finalDirectory,
                manifest,
                true,
                archiveVerifyMs,
                extractionMs);
    }

    private Manifest readManifest() throws ModelInstallException {
        try (InputStream input = assets.open(manifestAssetPath)) {
            JSONObject json = new JSONObject(readUtf8(input));
            JSONArray requiredJson = json.getJSONArray("requiredPaths");
            List<String> requiredPaths = new ArrayList<>();
            for (int i = 0; i < requiredJson.length(); i++) {
                requiredPaths.add(requiredJson.getString(i));
            }
            return new Manifest(
                    json.getString("modelId"),
                    json.getString("modelVersion"),
                    json.getString("archiveAssetPath"),
                    json.getString("topLevelDirectory"),
                    json.getString("archiveSha256").toUpperCase(Locale.US),
                    json.getLong("archiveBytes"),
                    json.getLong("expandedBytes"),
                    json.getInt("fileCount"),
                    requiredPaths);
        } catch (IOException ex) {
            throw new ModelInstallException("model_asset_missing", manifestAssetPath, ex);
        } catch (JSONException ex) {
            throw new ModelInstallException("model_integrity_failed", "Invalid model manifest.", ex);
        }
    }

    private String sha256Asset(String assetPath) throws ModelInstallException {
        try (InputStream raw = assets.open(assetPath);
             BufferedInputStream input = new BufferedInputStream(raw)) {
            MessageDigest digest = MessageDigest.getInstance("SHA-256");
            byte[] buffer = new byte[BUFFER_SIZE];
            long bytesRead = 0L;
            int count;
            while ((count = input.read(buffer)) != -1) {
                digest.update(buffer, 0, count);
                bytesRead += count;
            }
            Manifest manifest = readManifestForSizeOnly();
            if (bytesRead != manifest.archiveBytes) {
                throw new ModelInstallException(
                        "model_integrity_failed",
                        "Archive byte count mismatch: " + bytesRead);
            }
            return toHex(digest.digest());
        } catch (ModelInstallException ex) {
            throw ex;
        } catch (IOException ex) {
            throw new ModelInstallException("model_asset_missing", assetPath, ex);
        } catch (NoSuchAlgorithmException ex) {
            throw new ModelInstallException("unknown", "SHA-256 unavailable.", ex);
        }
    }

    private Manifest readManifestForSizeOnly() throws ModelInstallException {
        return readManifest();
    }

    private void ensureAvailableStorage(File targetRoot, long expandedBytes)
            throws ModelInstallException {
        File probe = targetRoot;
        while (probe != null && !probe.exists()) {
            probe = probe.getParentFile();
        }
        if (probe == null) {
            probe = appContext.getFilesDir();
        }
        StatFs statFs = new StatFs(probe.getAbsolutePath());
        long required = expandedBytes + STORAGE_MARGIN_BYTES;
        if (statFs.getAvailableBytes() < required) {
            throw new ModelInstallException(
                    "model_storage_insufficient",
                    "Required at least " + required + " bytes; available "
                            + statFs.getAvailableBytes());
        }
    }

    private void extractArchiveToStaging(Manifest manifest, File stagingDirectory)
            throws IOException, ModelInstallException {
        String canonicalRoot = stagingDirectory.getCanonicalPath() + File.separator;
        byte[] buffer = new byte[BUFFER_SIZE];

        try (InputStream raw = assets.open(manifest.archiveAssetPath);
             BufferedInputStream buffered = new BufferedInputStream(raw);
             ZipInputStream zip = new ZipInputStream(buffered)) {
            ZipEntry entry;
            while ((entry = zip.getNextEntry()) != null) {
                String name = entry.getName().replace('\\', '/');
                if (!name.startsWith(manifest.topLevelDirectory + "/")) {
                    throw new ModelInstallException(
                            "model_integrity_failed",
                            "Unexpected ZIP path: " + name);
                }

                String relative = name.substring(manifest.topLevelDirectory.length() + 1);
                if (relative.isEmpty()) {
                    zip.closeEntry();
                    continue;
                }

                File output = new File(stagingDirectory, relative);
                String canonicalOutput = output.getCanonicalPath();
                if (!canonicalOutput.startsWith(canonicalRoot)) {
                    throw new ModelInstallException(
                            "model_integrity_failed",
                            "Zip Slip path rejected: " + name);
                }

                if (entry.isDirectory()) {
                    if (!output.exists() && !output.mkdirs()) {
                        throw new IOException("Unable to create directory: " + output);
                    }
                } else {
                    File parent = output.getParentFile();
                    if (parent == null || (!parent.exists() && !parent.mkdirs())) {
                        throw new IOException("Unable to create directory: " + parent);
                    }
                    CRC32 crc = new CRC32();
                    try (BufferedOutputStream out =
                                 new BufferedOutputStream(new FileOutputStream(output))) {
                        int count;
                        while ((count = zip.read(buffer)) != -1) {
                            out.write(buffer, 0, count);
                            crc.update(buffer, 0, count);
                        }
                    }
                    if (entry.getSize() >= 0L && output.length() != entry.getSize()) {
                        throw new ZipException("Size mismatch for " + name);
                    }
                    if (entry.getCrc() >= 0L && crc.getValue() != entry.getCrc()) {
                        throw new ZipException("CRC mismatch for " + name);
                    }
                }
                zip.closeEntry();
            }
        }
    }

    private boolean isValidInstalledModel(File directory, Manifest manifest) {
        try {
            if (!directory.isDirectory()) {
                return false;
            }
            File marker = new File(directory, INSTALL_MARKER);
            if (!marker.isFile()) {
                return false;
            }
            JSONObject markerJson;
            try (InputStream input = new FileInputStream(marker)) {
                markerJson = new JSONObject(readUtf8(input));
            }
            if (!manifest.modelId.equals(markerJson.optString("modelId"))
                    || !manifest.modelVersion.equals(markerJson.optString("modelVersion"))
                    || !manifest.archiveSha256.equalsIgnoreCase(
                    markerJson.optString("archiveSha256"))) {
                return false;
            }
            verifyExtractedModel(directory, manifest);
            return true;
        } catch (Exception ignored) {
            return false;
        }
    }

    private void verifyExtractedModel(File directory, Manifest manifest)
            throws ModelInstallException {
        Set<String> files = new HashSet<>();
        long totalBytes = collectFiles(directory, directory, files);
        files.remove(INSTALL_MARKER);

        if (files.size() != manifest.fileCount) {
            throw new ModelInstallException(
                    "model_integrity_failed",
                    "Expected " + manifest.fileCount + " files but found " + files.size());
        }
        if (totalBytes != manifest.expandedBytes) {
            throw new ModelInstallException(
                    "model_integrity_failed",
                    "Expected " + manifest.expandedBytes + " bytes but found " + totalBytes);
        }
        for (String required : manifest.requiredPaths) {
            if (!files.contains(required)) {
                throw new ModelInstallException(
                        "model_integrity_failed",
                        "Required model path missing: " + required);
            }
        }
    }

    private long collectFiles(File root, File current, Set<String> paths)
            throws ModelInstallException {
        File[] children = current.listFiles();
        if (children == null) {
            throw new ModelInstallException(
                    "model_integrity_failed",
                    "Unable to enumerate: " + current);
        }

        long total = 0L;
        for (File child : children) {
            if (child.isDirectory()) {
                total += collectFiles(root, child, paths);
            } else {
                String relative = root.toPath().relativize(child.toPath())
                        .toString()
                        .replace(File.separatorChar, '/');
                paths.add(relative);
                if (!INSTALL_MARKER.equals(relative)) {
                    total += child.length();
                }
            }
        }
        return total;
    }

    private void writeInstallMarker(File directory, Manifest manifest) throws IOException {
        JSONObject json = new JSONObject();
        try {
            json.put("modelId", manifest.modelId);
            json.put("modelVersion", manifest.modelVersion);
            json.put("archiveSha256", manifest.archiveSha256);
            json.put("fileCount", manifest.fileCount);
            json.put("expandedBytes", manifest.expandedBytes);
        } catch (JSONException impossible) {
            throw new IOException("Unable to create install marker.", impossible);
        }
        File marker = new File(directory, INSTALL_MARKER);
        try (FileOutputStream output = new FileOutputStream(marker)) {
            output.write(json.toString().getBytes(StandardCharsets.UTF_8));
            output.flush();
            output.getFD().sync();
        }
    }

    private void replaceInstalledDirectory(
            File modelsRoot,
            File staging,
            File destination) throws IOException {
        File backup = new File(modelsRoot, "." + destination.getName() + ".backup");
        deleteRecursively(backup);

        boolean hadDestination = destination.exists();
        if (hadDestination) {
            moveDirectory(destination, backup);
        }

        try {
            moveDirectory(staging, destination);
            deleteRecursivelyQuietly(backup);
        } catch (IOException ex) {
            if (hadDestination && backup.exists() && !destination.exists()) {
                try {
                    moveDirectory(backup, destination);
                } catch (IOException restoreError) {
                    ex.addSuppressed(restoreError);
                }
            }
            throw ex;
        }
    }

    private static void moveDirectory(File source, File destination) throws IOException {
        try {
            Files.move(
                    source.toPath(),
                    destination.toPath(),
                    StandardCopyOption.ATOMIC_MOVE);
        } catch (AtomicMoveNotSupportedException ex) {
            Files.move(source.toPath(), destination.toPath());
        }
    }

    private static void deleteRecursively(File file) throws IOException {
        if (file == null || !file.exists()) {
            return;
        }
        if (file.isDirectory()) {
            File[] children = file.listFiles();
            if (children == null) {
                throw new IOException("Unable to enumerate: " + file);
            }
            for (File child : children) {
                deleteRecursively(child);
            }
        }
        if (!file.delete()) {
            throw new IOException("Unable to delete: " + file);
        }
    }

    private static void deleteRecursivelyQuietly(File file) {
        try {
            deleteRecursively(file);
        } catch (IOException ignored) {
        }
    }

    private static String readUtf8(InputStream input) throws IOException {
        ByteArrayOutputStream output = new ByteArrayOutputStream();
        byte[] buffer = new byte[8192];
        int count;
        while ((count = input.read(buffer)) != -1) {
            output.write(buffer, 0, count);
        }
        return output.toString(StandardCharsets.UTF_8.name());
    }

    private static String toHex(byte[] bytes) {
        StringBuilder builder = new StringBuilder(bytes.length * 2);
        for (byte value : bytes) {
            builder.append(String.format(Locale.US, "%02X", value));
        }
        return builder.toString();
    }

    static final class Result {
        final File modelDirectory;
        final Manifest manifest;
        final boolean extracted;
        final long archiveVerifyMs;
        final long extractionMs;

        Result(
                File modelDirectory,
                Manifest manifest,
                boolean extracted,
                long archiveVerifyMs,
                long extractionMs) {
            this.modelDirectory = modelDirectory;
            this.manifest = manifest;
            this.extracted = extracted;
            this.archiveVerifyMs = archiveVerifyMs;
            this.extractionMs = extractionMs;
        }
    }

    static final class Manifest {
        final String modelId;
        final String modelVersion;
        final String archiveAssetPath;
        final String topLevelDirectory;
        final String archiveSha256;
        final long archiveBytes;
        final long expandedBytes;
        final int fileCount;
        final List<String> requiredPaths;

        Manifest(
                String modelId,
                String modelVersion,
                String archiveAssetPath,
                String topLevelDirectory,
                String archiveSha256,
                long archiveBytes,
                long expandedBytes,
                int fileCount,
                List<String> requiredPaths) {
            this.modelId = modelId;
            this.modelVersion = modelVersion;
            this.archiveAssetPath = archiveAssetPath;
            this.topLevelDirectory = topLevelDirectory;
            this.archiveSha256 = archiveSha256;
            this.archiveBytes = archiveBytes;
            this.expandedBytes = expandedBytes;
            this.fileCount = fileCount;
            this.requiredPaths = requiredPaths;
        }
    }

    static final class ModelInstallException extends Exception {
        private static final long serialVersionUID = 1L;

        final String errorCode;

        ModelInstallException(String errorCode, String message) {
            super(message);
            this.errorCode = errorCode;
        }

        ModelInstallException(String errorCode, String message, Throwable cause) {
            super(message, cause);
            this.errorCode = errorCode;
        }
    }
}
