# Dependencies / Installation

Project Salieri AI Public v0.1はProject固有Sourceを公開し、第三者Asset・Native Binary・Model・Avatarを同梱しません。clone直後のProjectは、以下を利用者自身で導入・設定する必要があります。

## 1. Unity

- Unity Editor: `2022.3.62f3`
- 対象Platformに応じたWindows / Android Build Support
- `Packages/manifest.json`に記載されたUnity Package

`Packages/manifest.json`には`com.gilzoide.sqlite-net`のローカルfile dependencyが記録されています。第三者Package本体はPublic Repositoryへ収録していないため、互換版`1.3.2`を配布元から取得し、`Packages/com.gilzoide.sqlite-net`へ配置するか、利用環境のPackage管理方針に従って参照を解決してください。

## 2. OpenCV for Unity

OpenCV for UnityはEnox SoftwareのUnity Asset Store製品です。Repositoryには含まれません。

1. 利用者自身のAsset Store entitlementで取得します。
2. Unity Package Manager / Asset Storeの手順でProjectへimportします。
3. Project側参照を確認します。

購入Assetのファイルを第三者へ再配布しないでください。

## 3. VRM Avatar / UniVRM

- 利用者自身のVRM0 Avatarを用意してください。
- Production validationで使用したAsis3Dは収録されません。
- UniVRM / UniGLTF互換Packageを導入してください。Validation環境では0.131.0を使用しています。
- Avatar import後、Animator、Humanoid Bone、Arm IK、Leg IK、Head Look、Scene参照を利用者のAvatarへ設定してください。

Avatarごとの利用条件・再配布条件は、そのAvatarの権利者とLicenseに従います。

## 4. Cloud Conversation / OpenAI API Key

`Assets/Resources/Settings/LLM/CloudLLMSettings.asset`は収録しますが、`apiKey`は空です。

1. Unity Projectを開きます。
2. 各自のAPI KeyをProjectの設定Assetへ入力します。
3. Keyを含む差分をcommitしないでください。

API KeyはRepository、README、log、manifest、build artifactへ記録しないでください。共有前には必ずsecret scanを実行してください。

## 5. Speech

### Windows Vosk

- Vosk runtimeと日本語Modelを配布元から取得します。
- Validation環境のModelは`vosk-model-small-ja-0.22`です。
- Native DLLとModel archiveはRepositoryに含まれません。

### Android Vosk

- Android Vosk / JNAの互換AAR/native dependencyを導入します。
- AAR / SO / JARはRepositoryに含まれません。
- Public v0.1の`Assets/Plugins/Android/baseProjectTemplate.gradle`は、Validation環境で使用したR8 `8.13.19`を`Assets/Plugins/Android/BuildTools/r8-8.13.19.jar`から読む設定を保持しています。R8 JAR自体は非収録のため、Android buildを行う前に利用者が同版を正規の配布元から取得し、このpathへ配置してください。

### VOICEVOX / Open JTalk / UniDic

- 必要なVOICEVOX runtime、model、voice、Open JTalk / UniDic dictionaryを公式配布元の条件に従って導入します。
- Binary、Model、DictionaryはRepositoryに含まれません。
- 個々のvoice/modelには追加の利用条件がある場合があります。

## 6. Perception / ONNX

- ONNX Runtimeの対象Platform用Native runtimeを導入します。
- YOLOX ONNX modelはRepositoryに含まれません。
- 使用するexact model artifactの配布元・License・利用条件を確認してください。

## 7. Android USB Serial

Android USB-OTG Body接続では`usb-serial-for-android` 3.11.0互換Dependencyを使用します。

- FTDI FT232R: VID `0x0403`, PID `0x6001`で実機確認済み
- CH34x経路も既存Driver probeの対象として維持
- AARはRepositoryに含まれません
- AndroidX等のGradle dependencyもBuild環境で解決してください

Android Body TransportはUSB SerialとBluetoothを選択可能な構造です。Bluetooth実装をUSBへ置換・削除しないでください。

## 8. Arduino Firmware

対象はArduino UnoまたはNano classic相当です。

Arduino IDE / Arduino CLIで次を導入してください。

- Arduino AVR Boards（Arduino AVR Core / Wire）
- Adafruit PWM Servo Driver Library
- Adafruit BusIO

Firmwareの詳細は[`Firmware/.../README.md`](Firmware/Arduino/BODYLOBO/Salieri_BODYLOBO_Unified_PCA9685_10Servo_115200/README.md)を参照してください。Public v0.1ではArduino compileは未実行です。

## 9. 非収録データ

次をRepositoryへ追加しないでください。

- VRM / Avatar由来Asset
- API Key、token、credential
- Local LLM Source / GGUF Model
- Native DLL / SO / AAR / JAR
- ONNX / Vosk / VOICEVOX等のModel
- Open JTalk / UniDic dictionary
- Runtime Experience JSON / World Memory SQLite database
- `Library`、`Logs`、`Temp`、`obj`、Build、APK、cache

外部DependencyのLicense一覧は[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)を参照してください。
