# Dependencies / Installation

Project Salieri AI Public v0.1はProject固有SourceとStudio Hazama 714制作のVirtualBody FBXを公開し、第三者Asset・Native Binary・Model・Avatarを同梱しません。clone直後は完成状態ではなく、以下を利用者自身で導入・設定する必要があります。

## 1. Unity

- Unity Editor: `2022.3.62f3`
- 対象Platformに応じたWindows / Android Build Support
- `Packages/manifest.json`に記載されたUnity Package

## 2. UniVRM / UniGLTFと利用者Avatar

Validation環境はUniVRM / UniGLTF `0.131.0`を使用しています。Unity 2022.3以上を使用し、Unity Package Managerの「Add package from git URL」で次を追加してください。

```text
https://github.com/vrm-c/UniVRM.git?path=/Packages/UniGLTF#v0.131.0
https://github.com/vrm-c/UniVRM.git?path=/Packages/VRM#v0.131.0
```

利用者自身が再配布条件を確認したVRM0 Avatarをimportしてください。Production validationで使用したAsis3D Avatarは収録されません。MainSceneでは少なくとも次のAvatar依存参照を利用者のAvatarへ再設定します。

- `SimpleCameraOrbit`のChest target
- `AttentionTargetArmPointingController`のAnimator
- `VRMArmTargetIKController`のAvatar / Animator
- `UpperBodyAnimatorIKCoordinator`のAnimator
- `VRMLegTargetIKController`のAvatar / Animator
- Elbow HintのAnimator参照
- `VrmHeadLookAtVisual`のHead / LookAt関連参照

Humanoid Bone、Animator Controller、Arm / Leg IKがMissingでないことを確認してください。

## 3. VirtualBody

次の2 AssetはRepositoryに収録されるStudio Hazama 714制作のRobot Body / VirtualBody用FBXです。

```text
Assets/LobBodyModels/RobotBody/Asis3D_3_Vbody.fbx
Assets/LobBodyModels/RobotBody/AILob.fbx
```

VirtualBodyはVRM Animator IKの解やcanonicalなRobot joint表現を受け、Physical Retargetへ渡すためのBody representationです。利用者Avatarとは別責任であり、FBXだけでAvatar表示・会話・外部Dependencyが自動設定されるものではありません。

## 4. sqlite-net

使用版は`com.gilzoide.sqlite-net` `1.3.2`です。公式UPM URLは次です。

```text
https://github.com/gilzoide/unity-sqlite-net.git#1.3.2
```

現在の`Packages/manifest.json`は`file:com.gilzoide.sqlite-net`を参照します。Git URLを使用しない場合は同版を`Packages/com.gilzoide.sqlite-net`へ配置してください。Package payloadはRepositoryに含みません。

## 5. OpenCV for Unity

OpenCV for UnityはEnox SoftwareのUnity Asset Store製品です。

1. 利用者自身のAsset Store entitlementで取得します。
2. Unity Package Manager / Asset Storeの正規手順でimportします。
3. Project側のOpenCV参照を解決します。

Production treeから使用Versionを一意に確定できなかったため、exact versionは`REVIEW REQUIRED`です。購入Assetのファイルを第三者へ再配布しないでください。

## 6. Cloud Conversation / OpenAI API Key

`Assets/Resources/Settings/LLM/CloudLLMSettings.asset`は収録しますが、`apiKey`は空です。

1. Unity Projectを開きます。
2. 各自のAPI Keyを設定Assetへ入力します。
3. Keyを含む差分をcommitしません。

API KeyをRepository、README、log、manifest、build artifactへ記録しないでください。

## 7. Vosk Speech Recognition

### Windows

- Vosk native runtime（`libvosk`としてP/Invoke可能なWindows DLLとそのruntime dependency）を、UnityのWindows Pluginとして導入します。
- 日本語Modelは`vosk-model-small-ja-0.22`です。
- Model archiveを`Assets/StreamingAssets/Vosk/models/vosk-model-small-ja-0.22.zip`へ配置します。
- Model manifestを`Assets/StreamingAssets/Vosk/models/vosk-model-small-ja-0.22.manifest.json`へ配置します。

RuntimeはmanifestのSHA-256、展開後file countとrequired pathsを検証し、`Application.persistentDataPath/Vosk/models/vosk-model-small-ja-0.22`へ展開します。Public RepositoryにはDLL、zip、manifestを収録しません。使用するWindows native runtimeのexact versionは`REVIEW REQUIRED`です。

日本語Modelの公式一覧とLicenseは次で確認できます。

```text
https://alphacephei.com/vosk/models
```

### Android

Android sourceは`org.vosk` / `org.vosk.android` APIを使用し、runtime識別子は`vosk_android_0.3.75`です。Vosk公式Android demoが使用しているGradle dependencyは次です。

```gradle
implementation 'com.alphacephei:vosk-android:0.3.75@aar'
implementation 'net.java.dev.jna:jna:5.18.1@aar'
```

この2 artifactをGradle側で解決してください。Vosk AndroidはApache-2.0、JNA 5.18.1はLGPL-2.1-or-laterまたはApache-2.0のdual licenseです。Public RepositoryにはAAR / SO / JARを収録しません。

Windowsと同じModel archive / manifestをAndroid assetsへ含めると、端末上の`filesDir/vosk/models/vosk-model-small-ja-0.22`へ検証付きで展開されます。Model archive自体もRepositoryには含みません。

## 8. VOICEVOX / Open JTalk

Windows側は次を必要とします。

- `voicevox_unity_bridge`としてP/Invoke可能なnative bridgeとその依存runtime
- VOICEVOX Core runtime
- Open JTalk dictionary
- `.vvm` voice model

配置Path:

```text
Assets/StreamingAssets/VoiceVox/open_jtalk_dic_utf_8-1.11/
Assets/StreamingAssets/VoiceVox/models/1.vvm
```

Android側sourceはassetsの同じ相対Pathからdictionary / modelを端末の`filesDir`へコピーし、`onnxruntime`、`voicevox_core`、`voicevox_runtime` native libraryを読み込みます。

Public treeからCore、bridge、ONNX Runtime、voice modelのexact versionを一意に確定できません。VOICEVOX Coreの版、各voice/modelの利用条件、native bridge build provenanceはすべて`REVIEW REQUIRED`です。Binary、Model、Dictionaryは非収録です。

## 9. Perception / ONNX / YOLOX

- OpenCV for Unityのruntimeを導入します。
- Projectが期待するYOLOX model filenameは`yolox_tiny.onnx`です。
- 配置Pathは`Assets/StreamingAssets/OpenCVForUnityExamples/dnn/yolox_tiny.onnx`です。

ProductionのOpenCV example assetにはMegvii YOLOX release `0.1.1rc0`のdownload URLが記録されていますが、利用するmodel artifactのSHA・provider・再配布条件の独立確認は`REVIEW REQUIRED`です。ONNX modelとONNX/native runtimeはRepositoryに含みません。

## 10. Android Build / USB Serial

Android USB-OTG Body接続は`usb-serial-for-android` `3.11.0`を使用します。GradleでJitPack repositoryを有効にし、次のDependencyを追加してください。

```gradle
implementation 'com.github.mik3y:usb-serial-for-android:3.11.0'
```

- FTDI FT232R: VID `0x0403`, PID `0x6001`で実機確認済み
- CH34xも既存default proberの対象として維持
- AndroidX dependencyは選択したUnity / Android Gradle Plugin構成で解決
- `Assets/Plugins/Android/baseProjectTemplate.gradle`はR8 `8.13.19`を`Assets/Plugins/Android/BuildTools/r8-8.13.19.jar`から読む設定

R8 JARはRepositoryに収録しません。R8 upstreamはversion指定prebuiltをGoogle Mavenまたは公式`r8-releases` bucketから取得でき、未処理版JARの公式URL形式は次です。

```text
https://storage.googleapis.com/r8-releases/raw/<version>/r8.jar
```

このProjectでは`<version>`に`8.13.19`を使用し、取得したJARを次へ配置する構成です。

```text
Assets/Plugins/Android/BuildTools/r8-8.13.19.jar
```

R8 upstream LicenseはBSD 3-Clause形式です。Public v0.1ではJARを再配布しないため、取得したexact artifactのchecksum固定はまだ行っておらず`REVIEW REQUIRED`です。Android Body TransportはUSB SerialとBluetoothを選択可能であり、Bluetoothを削除・USBへ置換しないでください。

## 11. Arduino Firmware

対象はArduino UnoまたはNano classic相当です。Arduino IDE / Arduino CLIで次を導入してください。

- Arduino AVR Boards（Arduino AVR Core / Wire）
- Adafruit PWM Servo Driver Library（Validation側確認版`3.0.3`）
- Adafruit BusIO（Validation側確認版`1.17.4`）

Firmwareの詳細は[`Firmware/.../README.md`](Firmware/Arduino/BODYLOBO/Salieri_BODYLOBO_Unified_PCA9685_10Servo_115200/README.md)を参照してください。Public v0.1のArduino compileは`NOT RUN`です。

## 12. Maintenance / Optional Scenes

Maintenance用途のSceneは`ProjectSettings/EditorBuildSettings.asset`の有効Build Sceneには含めていません。既知のMaintenance側NecoMaid参照およびDebug `LogToFile`参照はPublic v0.1 Runtimeの必須依存ではなく、Build対象外のためOptional / Maintenance扱いです。

これらのMaintenance Sceneを利用する場合は、対応する外部Asset / Debug構成を利用者側で復元する必要があります。Public v0.1の通常セットアップでは復元不要です。将来の公開整理としてScene除外または参照cleanupを`REVIEW REQUIRED`とします。

## 13. 非収録データ

次をRepositoryへ追加しないでください。

- VRM / Avatar由来Asset
- API Key、token、credential
- Local LLM Source / GGUF Model
- OpenCV for Unity Asset Store package contents
- Native DLL / SO / dylib / AAR / JAR
- ONNX / Vosk / VOICEVOX等のModel archive
- Open JTalk / UniDic dictionary
- Runtime Experience JSON / World Memory SQLite database
- `Library`、`Logs`、`Temp`、`obj`、Build、APK、cache

外部DependencyのLicense一覧は[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)を参照してください。
