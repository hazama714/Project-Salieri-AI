// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.IO;
using System.Threading;

using UnityEngine;

namespace SalieriAI.App.Communication.Bluetooth
{
    public sealed class AndroidBluetoothConnectionBackend :
        IBluetoothConnectionOperationBackend
    {
        public BluetoothConnectionOperationResult Connect(
            BluetoothConnectionRequest request)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            int jniThreadId = Thread.CurrentThread.ManagedThreadId;
            bool attached = AndroidJNI.AttachCurrentThread() == 0;
            AndroidJavaObject socket = null;
            AndroidJavaObject stream = null;

            try
            {
                using (var adapterClass = new AndroidJavaClass(
                    "android.bluetooth.BluetoothAdapter"))
                using (AndroidJavaObject adapter =
                    adapterClass.CallStatic<AndroidJavaObject>(
                        "getDefaultAdapter"))
                {
                    if (adapter == null)
                    {
                        return Failure(
                            request,
                            BluetoothConnectionFailureKind.AdapterUnavailable,
                            "AdapterUnavailable",
                            "Bluetooth adapter is null.",
                            jniThreadId,
                            attached);
                    }

                    if (!adapter.Call<bool>("isEnabled"))
                    {
                        return Failure(
                            request,
                            BluetoothConnectionFailureKind.BluetoothDisabled,
                            "BluetoothDisabled",
                            "Bluetooth adapter is disabled.",
                            jniThreadId,
                            attached);
                    }

                    using (AndroidJavaObject bondedDevices =
                        adapter.Call<AndroidJavaObject>("getBondedDevices"))
                    {
                        if (bondedDevices == null)
                        {
                            return Failure(
                                request,
                                BluetoothConnectionFailureKind.BondedDevicesUnavailable,
                                "BondedDevicesUnavailable",
                                "Bonded device set is null.",
                                jniThreadId,
                                attached);
                        }

                        using (AndroidJavaObject iterator =
                            bondedDevices.Call<AndroidJavaObject>("iterator"))
                        {
                            AndroidJavaObject target = null;
                            try
                            {
                                while (iterator.Call<bool>("hasNext"))
                                {
                                    AndroidJavaObject candidate =
                                        iterator.Call<AndroidJavaObject>("next");
                                    string name = candidate.Call<string>("getName");
                                    if (name == request.DeviceName)
                                    {
                                        target = candidate;
                                        break;
                                    }
                                    candidate.Dispose();
                                }

                                if (target == null)
                                {
                                    return Failure(
                                        request,
                                        BluetoothConnectionFailureKind.DeviceNotFound,
                                        "DeviceNotFound",
                                        "Bluetooth target was not found.",
                                        jniThreadId,
                                        attached);
                                }

                                using (var uuidClass = new AndroidJavaClass(
                                    "java.util.UUID"))
                                using (AndroidJavaObject uuid =
                                    uuidClass.CallStatic<AndroidJavaObject>(
                                        "fromString",
                                        request.SppUuid))
                                {
                                    socket = target.Call<AndroidJavaObject>(
                                        "createRfcommSocketToServiceRecord",
                                        uuid);
                                }
                            }
                            finally
                            {
                                target?.Dispose();
                            }
                        }
                    }

                    if (socket == null)
                    {
                        return Failure(
                            request,
                            BluetoothConnectionFailureKind.SocketCreationFailed,
                            "SocketCreationFailed",
                            "Bluetooth socket is null.",
                            jniThreadId,
                            attached);
                    }

                    adapter.Call<bool>("cancelDiscovery");
                    socket.Call("connect");
                    stream = socket.Call<AndroidJavaObject>("getOutputStream");
                    if (stream == null)
                    {
                        CloseAndroidResources(socket, null);
                        socket = null;
                        return Failure(
                            request,
                            BluetoothConnectionFailureKind.OutputStreamUnavailable,
                            "OutputStreamUnavailable",
                            "Bluetooth output stream is null.",
                            jniThreadId,
                            attached);
                    }

                    var resources = new BluetoothConnectionResources(
                        socket,
                        stream);
                    socket = null;
                    stream = null;
                    return BluetoothConnectionOperationResult.Success(
                        request,
                        resources,
                        jniThreadId,
                        attached);
                }
            }
            catch (AndroidJavaException exception)
            {
                CloseAndroidResources(socket, stream);
                return Failure(
                    request,
                    BluetoothConnectionFailureKind.AndroidJavaException,
                    exception.GetType().Name,
                    exception.Message,
                    jniThreadId,
                    attached);
            }
            catch (IOException exception)
            {
                CloseAndroidResources(socket, stream);
                return Failure(
                    request,
                    BluetoothConnectionFailureKind.IOException,
                    exception.GetType().Name,
                    exception.Message,
                    jniThreadId,
                    attached);
            }
            catch (Exception exception)
            {
                CloseAndroidResources(socket, stream);
                return Failure(
                    request,
                    BluetoothConnectionFailureKind.UnexpectedException,
                    exception.GetType().Name,
                    exception.Message,
                    jniThreadId,
                    attached);
            }
            finally
            {
                AndroidJNI.DetachCurrentThread();
            }
#else
            return BluetoothConnectionOperationResult.Failure(
                request,
                BluetoothConnectionFailureKind.PlatformUnavailable,
                "PlatformUnavailable",
                "Android Bluetooth backend is unavailable in Editor.");
#endif
        }

        public void Release(BluetoothConnectionResources resources)
        {
            if (resources == null)
                return;

#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidJNI.AttachCurrentThread();
            try
            {
                CloseAndroidResources(
                    resources.SocketHandle as AndroidJavaObject,
                    resources.OutputStreamHandle as AndroidJavaObject);
            }
            finally
            {
                AndroidJNI.DetachCurrentThread();
            }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void CloseAndroidResources(
            AndroidJavaObject socket,
            AndroidJavaObject stream)
        {
            try
            {
                stream?.Call("close");
            }
            catch
            {
            }
            finally
            {
                stream?.Dispose();
            }

            try
            {
                socket?.Call("close");
            }
            catch
            {
            }
            finally
            {
                socket?.Dispose();
            }
        }
#endif

        private static BluetoothConnectionOperationResult Failure(
            BluetoothConnectionRequest request,
            BluetoothConnectionFailureKind kind,
            string failureType,
            string failureMessage,
            int jniThreadId,
            bool attached)
        {
            return BluetoothConnectionOperationResult.Failure(
                request,
                kind,
                failureType,
                failureMessage,
                jniThreadId,
                attached);
        }
    }
}
