// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

package com.studiohazama.salieri.stt.vosk;

import android.os.Debug;

import org.json.JSONException;
import org.json.JSONObject;

final class MemorySampler {
    private MemorySampler() {
    }

    static Snapshot capture(String label) {
        Debug.MemoryInfo memoryInfo = new Debug.MemoryInfo();
        Debug.getMemoryInfo(memoryInfo);

        Runtime runtime = Runtime.getRuntime();
        long javaHeapUsed = runtime.totalMemory() - runtime.freeMemory();
        return new Snapshot(
                label,
                memoryInfo.getTotalPss(),
                javaHeapUsed,
                Debug.getNativeHeapAllocatedSize(),
                runtime.maxMemory());
    }

    static final class Snapshot {
        final String label;
        final int pssKb;
        final long javaHeapUsedBytes;
        final long nativeHeapAllocatedBytes;
        final long javaHeapMaxBytes;

        Snapshot(
                String label,
                int pssKb,
                long javaHeapUsedBytes,
                long nativeHeapAllocatedBytes,
                long javaHeapMaxBytes) {
            this.label = label;
            this.pssKb = pssKb;
            this.javaHeapUsedBytes = javaHeapUsedBytes;
            this.nativeHeapAllocatedBytes = nativeHeapAllocatedBytes;
            this.javaHeapMaxBytes = javaHeapMaxBytes;
        }

        JSONObject toJson() throws JSONException {
            return new JSONObject()
                    .put("memory_label", label)
                    .put("pss_kb", pssKb)
                    .put("java_heap_used_bytes", javaHeapUsedBytes)
                    .put("native_heap_allocated_bytes", nativeHeapAllocatedBytes)
                    .put("java_heap_max_bytes", javaHeapMaxBytes);
        }

        String toDisplayString() {
            return "PSS: " + pssKb + " KB\n"
                    + "Java heap used: " + javaHeapUsedBytes + " bytes\n"
                    + "Native heap: " + nativeHeapAllocatedBytes + " bytes\n"
                    + "Java heap max: " + javaHeapMaxBytes + " bytes";
        }
    }
}
