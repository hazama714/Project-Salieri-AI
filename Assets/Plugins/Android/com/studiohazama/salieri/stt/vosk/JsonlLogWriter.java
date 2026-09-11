// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

package com.studiohazama.salieri.stt.vosk;

import android.content.Context;
import android.os.SystemClock;
import android.util.Log;

import org.json.JSONException;
import org.json.JSONObject;

import java.io.BufferedWriter;
import java.io.Closeable;
import java.io.File;
import java.io.FileWriter;
import java.io.IOException;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;
import java.util.TimeZone;

final class JsonlLogWriter implements Closeable {
    private static final String TAG = "SalieriVoskStt";

    private final File logFile;
    private BufferedWriter writer;
    private boolean closed;

    JsonlLogWriter(Context context) throws IOException {
        File directory = new File(context.getFilesDir(), "logs");
        if (!directory.exists() && !directory.mkdirs()) {
            throw new IOException("Unable to create log directory: " + directory);
        }

        SimpleDateFormat formatter = new SimpleDateFormat("yyyyMMdd_HHmmss_SSS", Locale.US);
        formatter.setTimeZone(TimeZone.getTimeZone("UTC"));
        logFile = new File(directory, "salieri_vosk_stt_" + formatter.format(new Date()) + ".jsonl");
        writer = new BufferedWriter(new FileWriter(logFile, true));
    }

    synchronized File getLogFile() {
        return logFile;
    }

    synchronized void write(String eventType, JSONObject details) {
        if (closed || writer == null) {
            return;
        }

        try {
            JSONObject entry = new JSONObject();
            entry.put("timestamp_utc", isoUtcNow());
            entry.put("elapsed_realtime_ms", SystemClock.elapsedRealtime());
            entry.put("event_type", eventType);
            if (details != null) {
                java.util.Iterator<String> keys = details.keys();
                while (keys.hasNext()) {
                    String key = keys.next();
                    entry.put(key, details.get(key));
                }
            }
            writer.write(entry.toString());
            writer.newLine();
            writer.flush();
        } catch (IOException | JSONException ex) {
            Log.e(TAG, "Failed to append JSONL log.", ex);
        }
    }

    @Override
    public synchronized void close() {
        if (closed) {
            return;
        }
        closed = true;
        if (writer != null) {
            try {
                writer.flush();
                writer.close();
            } catch (IOException ex) {
                Log.w(TAG, "Failed to close JSONL log.", ex);
            } finally {
                writer = null;
            }
        }
    }

    private static String isoUtcNow() {
        SimpleDateFormat formatter =
                new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'", Locale.US);
        formatter.setTimeZone(TimeZone.getTimeZone("UTC"));
        return formatter.format(new Date());
    }
}
