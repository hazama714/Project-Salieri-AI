// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

/*
 * BODYLOBO Editor -> USB Serial -> Arduino Uno
 *
 * Unified PCA9685 bridge:
 *
 *   Neck:
 *     Unity Servo ID 0 -> PCA9685 CH8  (Yaw)
 *     Unity Servo ID 1 -> PCA9685 CH9  (Pitch)
 *
 *   Right arm:
 *     Unity Servo ID 2 -> PCA9685 CH4
 *     Unity Servo ID 3 -> PCA9685 CH5
 *     Unity Servo ID 4 -> PCA9685 CH6
 *     Unity Servo ID 5 -> PCA9685 CH7
 *
 *   Left arm:
 *     Unity Servo ID 6 -> PCA9685 CH0
 *     Unity Servo ID 7 -> PCA9685 CH1
 *     Unity Servo ID 8 -> PCA9685 CH2
 *     Unity Servo ID 9 -> PCA9685 CH3
 *
 * Serial format:
 *   #0 P90
 *   #6 P60
 *
 * Safety:
 *   - 115200 baud
 *   - No automatic servo movement at startup
 *   - PCA9685 CH0..CH9 output is disabled at startup
 *   - A channel begins output only after receiving its first command
 */

#include <Wire.h>
#include <Adafruit_PWMServoDriver.h>

Adafruit_PWMServoDriver pwm = Adafruit_PWMServoDriver(0x40);

constexpr unsigned long SERIAL_BAUD = 115200;
constexpr uint16_t PWM_FREQUENCY_HZ = 50;

constexpr int SERVO_MIN_US = 600;
constexpr int SERVO_MAX_US = 2400;
constexpr int MIN_INPUT_ANGLE = 0;
constexpr int MAX_INPUT_ANGLE = 180;

String inputLine;

uint16_t microsecondsToTicks(int microseconds)
{
    const float periodUs = 1000000.0f / PWM_FREQUENCY_HZ;
    return static_cast<uint16_t>((microseconds * 4096.0f) / periodUs);
}

int servoIdToPcaChannel(int servoId)
{
    switch (servoId)
    {
        // Neck
        case 0: return 8;  // Yaw
        case 1: return 9;  // Pitch

        // Right arm
        case 2: return 4;
        case 3: return 5;
        case 4: return 6;
        case 5: return 7;

        // Left arm
        case 6: return 0;
        case 7: return 1;
        case 8: return 2;
        case 9: return 3;

        default: return -1;
    }
}

const __FlashStringHelper* servoGroupName(int servoId)
{
    if (servoId == 0 || servoId == 1)
        return F("NECK");

    if (servoId >= 2 && servoId <= 9)
        return F("ARM");

    return F("UNKNOWN");
}

void writeServoAngle(int servoId, int angle)
{
    const int channel = servoIdToPcaChannel(servoId);

    if (channel < 0)
    {
        Serial.print(F("[IGNORE] unsupported servoId="));
        Serial.println(servoId);
        return;
    }

    angle = constrain(angle, MIN_INPUT_ANGLE, MAX_INPUT_ANGLE);

    const int pulseUs = map(
        angle,
        MIN_INPUT_ANGLE,
        MAX_INPUT_ANGLE,
        SERVO_MIN_US,
        SERVO_MAX_US
    );

    const uint16_t ticks = microsecondsToTicks(pulseUs);
    pwm.setPWM(channel, 0, ticks);

    Serial.print(F("[OK]["));
    Serial.print(servoGroupName(servoId));
    Serial.print(F("] servoId="));
    Serial.print(servoId);
    Serial.print(F(" ch="));
    Serial.print(channel);
    Serial.print(F(" angle="));
    Serial.print(angle);
    Serial.print(F(" pulseUs="));
    Serial.print(pulseUs);
    Serial.print(F(" ticks="));
    Serial.println(ticks);
}

void processCommand(String command)
{
    command.trim();

    if (command.length() == 0)
        return;

    if (!command.startsWith("#"))
    {
        Serial.print(F("[ERROR] invalid command: "));
        Serial.println(command);
        return;
    }

    const int pIndex = command.indexOf('P');

    if (pIndex < 0)
    {
        Serial.print(F("[ERROR] missing P: "));
        Serial.println(command);
        return;
    }

    String servoIdText = command.substring(1, pIndex);
    servoIdText.trim();

    String angleText = command.substring(pIndex + 1);
    angleText.trim();

    writeServoAngle(servoIdText.toInt(), angleText.toInt());
}

void setup()
{
    Serial.begin(SERIAL_BAUD);
    inputLine.reserve(32);

    Wire.begin();
    pwm.begin();
    pwm.setPWMFreq(PWM_FREQUENCY_HZ);
    delay(10);

    // No movement until an explicit command is received.
    for (uint8_t ch = 0; ch <= 9; ++ch)
    {
        pwm.setPWM(ch, 0, 4096);
    }

    Serial.println(F("[READY] Project Salieri AI BODYLOBO unified PCA9685 bridge"));
    Serial.println(F("[READY] Neck IDs 0..1 -> PCA9685 CH8..CH9"));
    Serial.println(F("[READY] Right IDs 2..5 -> PCA9685 CH4..CH7"));
    Serial.println(F("[READY] Left IDs 6..9 -> PCA9685 CH0..CH3"));
    Serial.print(F("[READY] baud="));
    Serial.println(SERIAL_BAUD);
}

void loop()
{
    while (Serial.available() > 0)
    {
        const char c = static_cast<char>(Serial.read());

        if (c == '\n' || c == '\r')
        {
            if (inputLine.length() > 0)
            {
                processCommand(inputLine);
                inputLine = "";
            }
        }
        else if (inputLine.length() < 63)
        {
            inputLine += c;
        }
    }
}
