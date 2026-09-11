# BODYLOBO Unified PCA9685 Firmware

Project Salieri AIのUnity Runtimeから送られるBody commandを、USB Serial経由でArduinoが受信し、PCA9685の10 Servo channelへ変換するFirmwareです。

```text
Unity BodyCommandCoordinator
  → Windows COM / Android USB-OTG FT232R
  → Arduino
  → PCA9685
  → Neck / Arm Servo
```

## 対象Hardware

- Arduino UnoまたはNano classic相当
- PCA9685 PWM Driver
- PCA9685 address: `0x40`
- PWM frequency: `50 Hz`
- Serial baud: `115200`

## Serial protocol

```text
#<ServoID> P<angle>\n
```

例：

```text
#0 P90
#6 P60
```

角度は`0..180 degrees`へclampされ、`600..2400 microseconds`へ変換されます。

## Servo mapping

| Servo ID | PCA9685 CH | Body part |
|---:|---:|---|
| 0 | 8 | Neck Yaw |
| 1 | 9 | Neck Pitch |
| 2..5 | 4..7 | Right Arm |
| 6..9 | 0..3 | Left Arm |

## Startup safety

起動時はCH0..CH9をPCA9685の明示的Full-Offへ設定します。

```cpp
pwm.setPWM(ch, 0, 4096);
```

- 自動Startup Poseはありません。
- Neutral / Home角を起動時に送信しません。
- 各channelは最初の正しいSerial commandを受信した後にのみ通常PWM出力へ戻ります。

## Required Libraries

Arduino IDEのBoards Manager / Library Manager等から導入してください。

- Arduino AVR Core / Wire
- Adafruit PWM Servo Driver Library
- Adafruit BusIO

## 電源・配線上の注意

- Servo電源をArduinoの5V pinから直接供給しないでください。
- Servo仕様に合った独立電源と十分な電流容量を使用してください。
- Arduino、PCA9685、Servo電源のGNDを共通化してください。
- 初回試験は機構負荷を外すか、安全な可動域で実施してください。
- 非常停止と物理電源遮断手段を確保してください。
- UnityからCommandが送信されたことは、物理位置到達を保証しません。

## Compile status

Public v0.1 Firmware Sourceの静的Safety GateはPASSしています。Arduino CLIが検証環境に存在しなかったため、Firmware compileは`NOT RUN`です。
