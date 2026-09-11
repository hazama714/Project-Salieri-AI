// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

package com.studiohazama.salieri.usbserial;

import android.app.Activity;
import android.app.PendingIntent;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.hardware.usb.UsbDevice;
import android.hardware.usb.UsbDeviceConnection;
import android.hardware.usb.UsbManager;
import android.os.Build;
import android.util.Log;

import com.hoho.android.usbserial.driver.Ch34xSerialDriver;
import com.hoho.android.usbserial.driver.FtdiSerialDriver;
import com.hoho.android.usbserial.driver.UsbSerialDriver;
import com.hoho.android.usbserial.driver.UsbSerialPort;
import com.hoho.android.usbserial.driver.UsbSerialProber;
import com.unity3d.player.UnityPlayer;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.util.HashMap;
import java.util.List;

/**
 * Project Salieri Android USB Host bridge.
 *
 * Phase 1 discovery/permission/detach behavior remains here. Phase 2 adds
 * the supported USB serial open and write boundary. Body semantics stay in
 * Unity. FTDI and CH34x devices use usb-serial-for-android's default prober.
 */
public final class SalieriUsbSerialProbe {
    private static final String TAG = "AndroidUsbSerial";
    private static final String ACTION_USB_PERMISSION =
            "com.studiohazama.salieri.USB_SERIAL_PERMISSION";
    private static final Object LOCK = new Object();

    private static final int BAUD_RATE = 115200;
    private static final int WRITE_TIMEOUT_MILLIS = 250;

    private static BroadcastReceiver receiver;
    private static Context registeredContext;
    private static UsbManager usbManager;
    private static UsbSerialDriver selectedDriver;
    private static UsbSerialPort serialPort;
    private static UsbDeviceConnection deviceConnection;
    private static int selectedDeviceId = -1;
    private static boolean connected;
    private static long writeCount;
    private static volatile String unityObjectName;
    private static String lastEventPayload;

    private SalieriUsbSerialProbe() {
    }

    public static void initializeAndProbe(
            Activity activity,
            String receiverName) {
        unityObjectName = receiverName != null ? receiverName.trim() : null;
        if (activity == null) {
            log("PROBE_ERROR", "Activity=null");
            return;
        }

        Context context = activity.getApplicationContext();
        UsbManager manager = (UsbManager) context.getSystemService(
                Context.USB_SERVICE);
        if (manager == null) {
            log("PROBE_ERROR", "UsbManager=null");
            return;
        }

        synchronized (LOCK) {
            usbManager = manager;
        }

        try {
            ensureReceiver(context);
        } catch (RuntimeException exception) {
            log("PROBE_ERROR",
                    "ReceiverRegistrationFailed=" +
                    exception.getClass().getSimpleName() + ":" +
                    safe(exception.getMessage()));
            return;
        }

        HashMap<String, UsbDevice> deviceMap;
        try {
            deviceMap = manager.getDeviceList();
        } catch (RuntimeException exception) {
            log("PROBE_ERROR",
                    "DeviceEnumerationFailed=" +
                    exception.getClass().getSimpleName() + ":" +
                    safe(exception.getMessage()));
            return;
        }

        if (deviceMap.isEmpty()) {
            log("DISCONNECTED", "State=DeviceDisconnected DeviceCount=0");
            return;
        }

        for (UsbDevice device : deviceMap.values()) {
            logDevice(manager, device);
        }

        UsbSerialDriver driver = selectSupportedSerialDriver(manager);
        if (driver == null) {
            log("UNSUPPORTED_DEVICE",
                    "State=UnsupportedDevice " +
                    "Reason=NoSupportedSerialDriver " +
                    "DeviceCount=" + deviceMap.size());
            log("DISCONNECTED", "State=DeviceDisconnected");
            return;
        }

        UsbDevice device = driver.getDevice();
        log("SERIAL_DEVICE_FOUND",
                "Driver=" + driver.getClass().getSimpleName() +
                " VID=" + hex(device.getVendorId()) +
                " PID=" + hex(device.getProductId()) +
                " Manufacturer=" + safeManufacturer(device) +
                " Product=" + safeProduct(device));
        log("DRIVER",
                "Name=" + driver.getClass().getSimpleName() +
                " DeviceId=" + device.getDeviceId());

        synchronized (LOCK) {
            if (connected && selectedDeviceId == device.getDeviceId()
                    && serialPort != null && serialPort.isOpen()) {
                log("CONNECTED",
                        "State=Connected DeviceId=" + device.getDeviceId() +
                        " AlreadyOpen=true");
                return;
            }

            if (selectedDeviceId != -1
                    && selectedDeviceId != device.getDeviceId()) {
                closeLocked("DeviceSelectionChanged", true);
            }
            selectedDriver = driver;
            selectedDeviceId = device.getDeviceId();
        }

        if (manager.hasPermission(device)) {
            log("PERMISSION_GRANTED",
                    deviceIdentity("State=PermissionGranted", device));
            openSelectedDevice(manager, device);
        } else {
            requestPermission(context, manager, device);
        }
    }

    public static boolean isConnected() {
        synchronized (LOCK) {
            return connected && serialPort != null && serialPort.isOpen();
        }
    }

    public static boolean write(String command) {
        if (command == null || command.isEmpty()) {
            log("WRITE_ERROR", "Reason=EmptyCommand");
            return false;
        }

        synchronized (LOCK) {
            if (!connected || serialPort == null || !serialPort.isOpen()) {
                log("WRITE_ERROR", "Reason=NotConnected");
                return false;
            }

            byte[] payload = command.getBytes(StandardCharsets.UTF_8);
            try {
                serialPort.write(payload, WRITE_TIMEOUT_MILLIS);
                writeCount++;
                if (writeCount == 1L || writeCount % 100L == 0L) {
                    log("WRITE",
                            "Result=Success Bytes=" + payload.length +
                            " Count=" + writeCount);
                }
                return true;
            } catch (IOException | RuntimeException exception) {
                log("WRITE_ERROR",
                        "Result=Failed Type=" +
                        exception.getClass().getSimpleName() +
                        " Message=" + safe(exception.getMessage()));
                closeLocked("WriteFailed", true);
                return false;
            }
        }
    }

    public static void shutdown(Activity activity) {
        synchronized (LOCK) {
            closeLocked("Shutdown", true);
            try {
                if (registeredContext != null && receiver != null) {
                    registeredContext.unregisterReceiver(receiver);
                }
            } catch (RuntimeException exception) {
                log("SHUTDOWN_ERROR",
                        exception.getClass().getSimpleName() + ": " +
                        safe(exception.getMessage()));
            } finally {
                receiver = null;
                registeredContext = null;
                usbManager = null;
                selectedDriver = null;
                selectedDeviceId = -1;
                unityObjectName = null;
            }
        }
    }

    private static UsbSerialDriver selectSupportedSerialDriver(
            UsbManager manager) {
        List<UsbSerialDriver> allDrivers =
                UsbSerialProber.getDefaultProber().findAllDrivers(manager);
        UsbSerialDriver selected = null;
        for (UsbSerialDriver driver : allDrivers) {
            if (!isSupportedSerialDriver(driver)) {
                continue;
            }

            if (selected == null || driverPriority(driver)
                    < driverPriority(selected) ||
                    (driverPriority(driver) == driverPriority(selected) &&
                    driver.getDevice().getDeviceId()
                    < selected.getDevice().getDeviceId())) {
                selected = driver;
            }
        }
        return selected;
    }

    private static boolean isSupportedSerialDriver(
            UsbSerialDriver driver) {
        return driver instanceof FtdiSerialDriver ||
                driver instanceof Ch34xSerialDriver;
    }

    private static int driverPriority(UsbSerialDriver driver) {
        return driver instanceof FtdiSerialDriver ? 0 : 1;
    }

    private static void openSelectedDevice(
            UsbManager manager,
            UsbDevice device) {
        synchronized (LOCK) {
            if (device == null || device.getDeviceId() != selectedDeviceId) {
                log("OPEN_ERROR", "Reason=SelectedDeviceMismatch");
                closeLocked("SelectedDeviceMismatch", true);
                return;
            }
            if (!manager.hasPermission(device)) {
                log("PERMISSION_DENIED",
                        deviceIdentity("State=PermissionDenied", device));
                closeLocked("PermissionMissing", true);
                return;
            }

            UsbSerialDriver driver =
                    UsbSerialProber.getDefaultProber().probeDevice(device);
            if (!isSupportedSerialDriver(driver)) {
                log("UNSUPPORTED_DEVICE",
                        deviceIdentity(
                                "Reason=UnsupportedSerialDriver",
                                device));
                closeLocked("UnsupportedDevice", true);
                return;
            }
            if (driver.getPorts().isEmpty()) {
                log("OPEN_ERROR", "Reason=NoSerialPorts");
                closeLocked("NoSerialPorts", true);
                return;
            }

            closeLocked("Reopen", false);
            UsbDeviceConnection newConnection = manager.openDevice(device);
            if (newConnection == null) {
                log("OPEN_ERROR", "Reason=OpenDeviceReturnedNull");
                closeLocked("OpenDeviceFailed", true);
                return;
            }

            UsbSerialPort newPort = driver.getPorts().get(0);
            try {
                newPort.open(newConnection);
                log("OPEN",
                        deviceIdentity("Result=Success Port=0", device));
                newPort.setParameters(
                        BAUD_RATE,
                        UsbSerialPort.DATABITS_8,
                        UsbSerialPort.STOPBITS_1,
                        UsbSerialPort.PARITY_NONE);
                newPort.setFlowControl(UsbSerialPort.FlowControl.NONE);
                log("CONFIG", "115200 8N1 FlowControl=None");

                selectedDriver = driver;
                serialPort = newPort;
                deviceConnection = newConnection;
                selectedDeviceId = device.getDeviceId();
                connected = true;
                writeCount = 0L;
                log("CONNECTED",
                        deviceIdentity("State=Connected", device));
            } catch (IOException | RuntimeException exception) {
                try {
                    newPort.close();
                } catch (IOException ignored) {
                }
                newConnection.close();
                log("OPEN_ERROR",
                        "Result=Failed Type=" +
                        exception.getClass().getSimpleName() +
                        " Message=" + safe(exception.getMessage()));
                closeLocked("OpenFailed", true);
            }
        }
    }

    private static void ensureReceiver(final Context context) {
        synchronized (LOCK) {
            if (receiver != null) {
                return;
            }

            BroadcastReceiver newReceiver = new BroadcastReceiver() {
                @Override
                public void onReceive(Context receiverContext, Intent intent) {
                    if (intent == null) {
                        return;
                    }

                    String action = intent.getAction();
                    UsbDevice device = getUsbDevice(intent);
                    if (ACTION_USB_PERMISSION.equals(action)) {
                        if (device == null) {
                            device = findSelectedDevice();
                        }
                        boolean granted = intent.getBooleanExtra(
                                UsbManager.EXTRA_PERMISSION_GRANTED, false);
                        if (granted && device != null
                                && device.getDeviceId() == selectedDeviceId) {
                            log("PERMISSION_GRANTED",
                                    deviceIdentity(
                                            "State=PermissionGranted",
                                            device));
                            UsbManager manager;
                            synchronized (LOCK) {
                                manager = usbManager;
                            }
                            if (manager != null) {
                                openSelectedDevice(manager, device);
                            } else {
                                log("OPEN_ERROR", "Reason=UsbManagerUnavailable");
                            }
                        } else {
                            log("PERMISSION_DENIED",
                                    "State=PermissionDenied DeviceId=" +
                                    (device != null
                                            ? device.getDeviceId()
                                            : -1));
                            synchronized (LOCK) {
                                closeLocked("PermissionDenied", true);
                            }
                        }
                    } else if (UsbManager.ACTION_USB_DEVICE_DETACHED.equals(
                            action)) {
                        int detachedId = device != null
                                ? device.getDeviceId()
                                : -1;
                        log("DETACHED",
                                "State=DeviceDisconnected DeviceId=" +
                                detachedId);
                        synchronized (LOCK) {
                            if (detachedId == -1
                                    || detachedId == selectedDeviceId) {
                                closeLocked("DeviceDetached", true);
                                selectedDriver = null;
                                selectedDeviceId = -1;
                            }
                        }
                    }
                }
            };

            IntentFilter filter = new IntentFilter(ACTION_USB_PERMISSION);
            filter.addAction(UsbManager.ACTION_USB_DEVICE_DETACHED);
            if (Build.VERSION.SDK_INT >= 33) {
                context.registerReceiver(newReceiver, filter, 4);
            } else {
                context.registerReceiver(newReceiver, filter);
            }
            receiver = newReceiver;
            registeredContext = context;
        }
    }

    private static void requestPermission(
            Context context,
            UsbManager manager,
            UsbDevice device) {
        Intent intent = new Intent(ACTION_USB_PERMISSION);
        intent.setPackage(context.getPackageName());
        int flags = PendingIntent.FLAG_UPDATE_CURRENT;
        if (Build.VERSION.SDK_INT >= 31) {
            flags |= 0x02000000; // PendingIntent.FLAG_MUTABLE
        }
        PendingIntent permissionIntent = PendingIntent.getBroadcast(
                context, device.getDeviceId(), intent, flags);
        log("PERMISSION_REQUEST",
                deviceIdentity("State=PermissionPending", device));
        try {
            manager.requestPermission(device, permissionIntent);
        } catch (RuntimeException exception) {
            log("PROBE_ERROR",
                    "PermissionRequestFailed=" +
                    exception.getClass().getSimpleName() + ":" +
                    safe(exception.getMessage()));
        }
    }

    private static UsbDevice findSelectedDevice() {
        synchronized (LOCK) {
            if (usbManager == null || selectedDeviceId == -1) {
                return null;
            }
            for (UsbDevice device : usbManager.getDeviceList().values()) {
                if (device.getDeviceId() == selectedDeviceId) {
                    return device;
                }
            }
            return null;
        }
    }

    private static void closeLocked(String reason, boolean notify) {
        boolean hadResources = connected || serialPort != null
                || deviceConnection != null;
        UsbSerialPort oldPort = serialPort;
        UsbDeviceConnection oldConnection = deviceConnection;
        serialPort = null;
        deviceConnection = null;
        connected = false;
        writeCount = 0L;

        if (oldPort != null) {
            try {
                oldPort.close();
            } catch (IOException ignored) {
            }
        }
        if (oldConnection != null) {
            oldConnection.close();
        }
        if (notify && (hadResources || !"Shutdown".equals(reason))) {
            log("DISCONNECTED",
                    "State=DeviceDisconnected Reason=" + safe(reason));
        }
    }

    private static void logDevice(UsbManager manager, UsbDevice device) {
        log("DEVICE_FOUND",
                "DeviceName=" + safe(device.getDeviceName()) +
                " DeviceId=" + device.getDeviceId() +
                " VID=" + hex(device.getVendorId()) +
                " PID=" + hex(device.getProductId()) +
                " Manufacturer=" + safeManufacturer(device) +
                " Product=" + safeProduct(device) +
                " DeviceClass=" + device.getDeviceClass() +
                " InterfaceCount=" + device.getInterfaceCount() +
                " HasPermission=" + manager.hasPermission(device));
    }

    private static String safeManufacturer(UsbDevice device) {
        try {
            return safe(device.getManufacturerName());
        } catch (RuntimeException ignored) {
            return "<unavailable>";
        }
    }

    private static String safeProduct(UsbDevice device) {
        try {
            return safe(device.getProductName());
        } catch (RuntimeException ignored) {
            return "<unavailable>";
        }
    }

    private static String deviceIdentity(String prefix, UsbDevice device) {
        return prefix +
                " DeviceId=" + device.getDeviceId() +
                " VID=" + hex(device.getVendorId()) +
                " PID=" + hex(device.getProductId());
    }

    @SuppressWarnings("deprecation")
    private static UsbDevice getUsbDevice(Intent intent) {
        if (Build.VERSION.SDK_INT >= 33) {
            return intent.getParcelableExtra(
                    UsbManager.EXTRA_DEVICE, UsbDevice.class);
        }
        return intent.getParcelableExtra(UsbManager.EXTRA_DEVICE);
    }

    private static String hex(int value) {
        return String.format("0x%04X", value & 0xFFFF);
    }

    private static String safe(String value) {
        if (value == null || value.trim().isEmpty()) {
            return "<unknown>";
        }
        return value.trim().replace('\n', ' ').replace('\r', ' ');
    }

    private static void log(String event, String detail) {
        String safeDetail = detail != null ? detail : "";
        String eventPayload = event + "\n" + safeDetail;
        synchronized (LOCK) {
            if (eventPayload.equals(lastEventPayload)) {
                return;
            }
            lastEventPayload = eventPayload;
        }
        Log.i(TAG, "[AndroidUsbSerial][" + event + "] " + safeDetail);
        String receiverName = unityObjectName;
        if (receiverName != null && !receiverName.isEmpty()) {
            UnityPlayer.UnitySendMessage(
                    receiverName,
                    "OnAndroidUsbSerialEvent",
                    eventPayload);
        }
    }
}
