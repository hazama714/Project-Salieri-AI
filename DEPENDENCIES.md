# Dependencies / Installation

Project Salieri AI Public v0.1はProject固有SourceとStudio Hazama 714制作のVirtualBody FBXを公開し、第三者Asset・Native Binary・Model・Avatarを同梱しません。clone直後は完成状態ではなく、以下を利用者自身で導入・設定する必要があります。

Validationで確認した実体のfingerprintと、未確定Dependencyの確認手順は[`docs/PUBLIC_DEPENDENCY_VERIFICATION.md`](docs/PUBLIC_DEPENDENCY_VERIFICATION.md)を参照してください。

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

Validation環境はEnox Software OpenCV for Unity `3.0.2`、内包OpenCV `4.13.0`です。Enox Softwareの3.0.2 release noteでもOpenCV 4.13.0への更新が確認できます。

1. 利用者自身のAsset Store entitlementでOpenCV for Unity `3.0.2`を取得します。
2. Unity Package Manager / Asset Storeの正規手順でimportします。
3. Project側のOpenCV参照を解決します。

Validation fingerprint:

```text
Windows x64 opencvforunity.dll
SHA-256 4B6D8BF9E6B63B3451A49620F4A5AFA2CC3DB878416DEAE7CB0CBCE0E46F362C

Android arm64 libopencvforunity.so
SHA-256 E8682834CC3054A93DABCA70198F9D67C3D0D6DB7803CAF3B519429684B33C22
```

購入Assetのファイルを第三者へ再配布しないでください。Public RepositoryにはOpenCV for Unity package内容を含めません。

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

RuntimeはmanifestのSHA-256、展開後file countとrequired pathsを検証し、`Application.persistentDataPath/Vosk/models/vosk-model-small-ja-0.22`へ展開します。Public RepositoryにはDLL、zip、manifestを収録しません。

Validation環境のWindows `libvosk.dll`は実使用とSHA-256まで確認済みですが、DLL自体に製品Version情報がなく元配布archiveも残っていないため、exact runtime versionは`UNCONFIRMED`です。推測で`0.3.x`を割り当てないでください。

```text
libvosk.dll
SHA-256 9331C2F6A32CF77141AF27C9750E79532718F51BBC2E3CEA3C60F14CA4251E4E
```

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

## 8. VOICEVOX / VOICEVOX ONNX Runtime / Open JTalk

Validation環境で確認できたVoice stackは次です。

- VOICEVOX Core: `0.16.4`
- VOICEVOX ONNX Runtime: `1.17.3`
- Production選択VVM: `1.vvm`
- Voice: 冥鳴ひまり
- Style: ノーマル
- Style ID: `14`
- Voice model metadata version: `0.16.0`
- VVM format version: `1`

VOICEVOX Core 0.16系は通常のMicrosoft ONNX Runtimeではなく、製品版VVMを読むため`VOICEVOX ONNX Runtime`を使用します。公式Core changelogも`voicevox_onnxruntime-1.17.3`を指定しています。Generic Microsoft ONNX Runtime 1.17.3と同一物として扱わないでください。

Windows側は次を必要とします。

- `voicevox_unity_bridge`としてP/Invoke可能なnative bridge
- VOICEVOX Core `0.16.4`
- VOICEVOX ONNX Runtime `1.17.3`
- Open JTalk dictionary
- `1.vvm`

配置Path:

```text
Assets/StreamingAssets/VoiceVox/open_jtalk_dic_utf_8-1.11/
Assets/StreamingAssets/VoiceVox/models/1.vvm
```

Validation fingerprint:

```text
Windows voicevox_core.dll
SHA-256 DBA594584FD70A25148FA0F73D50D061FA75E2BDBE46DC37CE79BF772B675420

Android libvoicevox_core.so
SHA-256 5382A785358F9A4B7510C2A8C317A4D985430CA415DA9C1FCC4529693A4C2420

Windows voicevox_onnxruntime.dll
SHA-256 C677274EDB5A77EA26893BA8368B3791724AB8A7ACAAB73A310C04FC47E9CD82

Android ONNX Runtime binary used by the VOICEVOX path
SHA-256 9BD4FEE054893F4CDFAED00AC5257BC414E08E916BD73528C310F5423F762369

1.vvm
SHA-256 8DF20815BE9A84A4B4723B9E778D2EF6A4DF277E4872FE9EFE29676A60E02774
```

Android側sourceはassetsの同じ相対Pathからdictionary / modelを端末の`filesDir`へコピーし、VOICEVOX CoreとVOICEVOX ONNX Runtime系native libraryを読み込みます。

`voicevox_unity_bridge.dll`とAndroid側`libvoicevox_runtime.so`は実使用ファイルとSHA-256を確認済みですが、独立SemVer / build manifest / native source provenanceがPublic treeから再現できないため`UNCONFIRMED`です。Public v0.1ではこれらのBinaryを再配布しません。VOICEVOX経路を完全再現可能にする前にbuild provenanceまたは再build手順を固定してください。

Open JTalk dictionaryは`open_jtalk_dic_utf_8-1.11`を期待します。Distributionに含まれるLicense / noticeを保持し、Dictionary自体はRepositoryに収録しません。

VOICEVOX VVMのCurrent termsはアプリケーション組み込み再配布を許可していますが、本Repositoryでは`1.vvm`を同梱しません。生成音声の利用時はVOICEVOXおよび冥鳴ひまりの利用規約・クレジット条件に従ってください。

## 9. Perception / YOLOX

- OpenCV for Unity `3.0.2`を導入します。
- Projectが期待するYOLOX model filenameは`yolox_tiny.onnx`です。
- 配置Pathは`Assets/StreamingAssets/OpenCVForUnityExamples/dnn/yolox_tiny.onnx`です。
- Provenance: Megvii-BaseDetection/YOLOX release `0.1.1rc0`

Validation fingerprint:

```text
yolox_tiny.onnx
Size: 20,219,662 bytes
SHA-256: 427CC366D34E27FF7A03E2899B5E3671425C262EA2291F88BB942BC1CC70B0F7
SHA-1: 45985579A307AAE54C7B54CA257BC0B48606DEAC
```

SHA-1はOpenCV for UnityのDownloader定義値と一致し、公式YOLOX ONNX Runtime documentationが同じ`0.1.1rc0` release assetを案内しています。ONNX modelはRepositoryに含みません。Artifactに独立したLicense文書は確認できていないため、再配布する場合はupstream Apache-2.0 noticeとrelease artifact条件を再確認してください。

## 10. Android Build / USB Serial

Android USB-OTG Body接続は`usb-serial-for-android` `3.11.0`を使用します。GradleでJitPack repositoryを有効にし、次のDependencyを追加してください。

```gradle
implementation 'com.github.mik3y:usb-serial-for-android:3.11.0'
```

- FTDI FT232R: VID `0x0403`, PID `0x6001`で実機確認済み
- CH34xも既存default proberの対象として維持
- AndroidX dependencyは選択したUnity / Android Gradle Plugin構成で解決
- `Assets/Plugins/Android/baseProjectTemplate.gradle`はR8 `8.13.19`を`Assets/Plugins/Android/BuildTools/r8-8.13.19.jar`から読む設定

R8 JARはRepositoryに収録しません。R8 upstreamはversion指定prebuiltをGoogle Mavenまたは公式`r8-releases` bucketから取得できます。

```text
https://storage.googleapis.com/r8-releases/raw/8.13.19/r8.jar
```

配置Path:

```text
Assets/Plugins/Android/BuildTools/r8-8.13.19.jar
```

Validation fingerprint:

```text
R8: 8.13.19
Version commit: 4ab5d6fdeb2fdaa29021e03d0ee02219e53e39fd
SHA-256: 7712EDBAE6A71F35937FFC1BD5F4C202919EF2A5E58D3C3544015A868A3CF457
```

R8 upstream LicenseはBSD 3-Clauseです。Public v0.1ではJARを再配布しません。Android Body TransportはUSB SerialとBluetoothを選択可能であり、Bluetoothを削除・USBへ置換しないでください。

## 11. Arduino Firmware

対象はArduino UnoまたはNano classic相当です。Arduino IDE / Arduino CLIで次を導入してください。

- Arduino AVR Boards（Arduino AVR Core / Wire）
- Adafruit PWM Servo Driver Library（Validation側確認版`3.0.3`）
- Adafruit BusIO（Validation側確認版`1.17.4`）

Validation PCにはArduino AVR Boards `1.8.6`が導入されています。ただしPublic v0.1 Firmwareはまだcompileしていないため、`1.8.6`を「実際にcompile済みのCore Version」とは表記しません。Firmware Compile statusは引き続き`NOT RUN`です。

Firmwareの詳細は[`Firmware/.../README.md`](Firmware/Arduino/BODYLOBO/Salieri_BODYLOBO_Unified_PCA9685_10Servo_115200/README.md)を参照してください。

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
