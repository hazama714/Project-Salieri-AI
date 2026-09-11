# Public v0.1 Firmware Manifest

## Public artifact

- Public path: `Firmware/Arduino/BODYLOBO/Salieri_BODYLOBO_Unified_PCA9685_10Servo_115200/Salieri_BODYLOBO_Unified_PCA9685_10Servo_115200.ino`
- Source Version: `0.1.0`
- Source Date: `2026-09-11`
- Origin: `Own-Dependent`
- Target License Category: `B`
- License: Project Salieri License v1.0
- Old Apache lineage: `DERIVED / MOVED / CHANGED`
- Old Repository reference: `Project-Salieri-AI-Android-Runtime`
- Original validated firmware SHA-256 before public-header migration: `411C37DB9A4B9EFD6EAE0A3B8ADFD7094B1BF8C62822C80F1312EFB5BC38B62D`
- Public copy SHA-256: `E24914F594D05EEEB19374C238C718365D23C56C61AD182BD2D440445A0D14EA`

Public copyではLicense migrationによりHeaderのみをProject Salieri License v1.0へ変更しています。Firmware機能コードは変更していません。

## Hardware / protocol contract

- Target: Arduino Uno / Nano classic equivalent
- Serial baud: `115200`
- Serial command: `#<ServoID> P<angle>` followed by newline
- PCA9685 I2C address: `0x40`
- PWM frequency: `50 Hz`
- Input angle clamp: `0..180 degrees`
- Pulse range: `600..2400 microseconds`

### Servo mapping

| Unity Servo ID | PCA9685 Channel | Meaning |
|---:|---:|---|
| 0 | 8 | Neck Yaw |
| 1 | 9 | Neck Pitch |
| 2 | 4 | Right Arm 1 |
| 3 | 5 | Right Arm 2 |
| 4 | 6 | Right Arm 3 |
| 5 | 7 | Right Arm 4 |
| 6 | 0 | Left Arm 1 |
| 7 | 1 | Left Arm 2 |
| 8 | 2 | Left Arm 3 |
| 9 | 3 | Left Arm 4 |

## Startup safety

- CH0..CH9: explicit PCA9685 Full-Off by `pwm.setPWM(ch, 0, 4096)`
- Automatic startup pose: none
- Neutral / Home write at startup: none
- First valid Serial command: existing `writeServoAngle` path restores normal PWM output for the addressed channel
- Unsupported Servo ID: ignored with existing diagnostic response

## External dependencies

- Arduino AVR Core / Wire
- Adafruit PWM Servo Driver Library 3.0.3-compatible
- Adafruit BusIO 1.17.4-compatible

## Verification

- Firmware safety gate: PASS
- Explicit Full-Off: PASS
- No automatic startup pose: PASS
- Firmware compile: NOT RUN（Arduino CLIが検証環境に存在しなかったため）
