# Project Salieri AI

Project Salieri AIは、**AIキャラクターに現実の身体を与えるためのPhysical AI Runtime**です。

知覚・注意・会話・身体表現・実機制御を、次の一本の経路として接続することを目指しています。

```text
Seeing
  → Attention
  → Conversation
  → Body Action
  → VRM / IK
  → VirtualBody
  → Physical Retarget
  → Arduino / Servo
```

## Public v0.1の範囲

- Unity Runtimeの公開対象Source 502件
- PC Editor / Android向けの共通Runtime基盤
- Attention、Conversation、Experience / Memory、Free Pose、VRM / IK、VirtualBody、Physical Control、Safety、Communication
- Arduino + PCA9685向けBODYLOBO Firmware 1件

Source Versionはファイル単位で管理し、Public v0.1の初回公開対象は`0.1.0`から開始します。運用規則は[Source Versioning](SOURCE_VERSIONING.md)を参照してください。

## 収録しないもの

- Asis3DおよびAvatar由来Asset
- VRMファイル、Mesh、Texture、Material、BlendShape、Avatar、MetaObject
- Local LLM RuntimeおよびGGUF Model
- OpenCV for Unity Asset Store内容
- Vosk / VOICEVOX / Open JTalk / UniDicのModel・Dictionary・Native Binary
- ONNX Model、DLL、SO、AAR、JAR
- API Key、credential、Runtime database、log、build成果物

このRepositoryは完成済みAvatarや第三者Binaryを再配布しません。利用者は自分のVRMと必要な外部Dependencyを用意してください。導入条件は[DEPENDENCIES.md](DEPENDENCIES.md)、外部コンポーネントの権利情報は[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)にまとめています。

## セットアップ概要

1. Unity `2022.3.62f3`でProjectを開きます。
2. [DEPENDENCIES.md](DEPENDENCIES.md)に従って外部Package、Asset、Native Runtime、Modelを導入します。
3. 自分のVRM0 Avatarを配置し、Animator / IK / Scene参照を設定します。
4. Cloud Conversationを使う場合は、Unity上でOpenAI API Keyを各自設定します。
5. API KeyをGitへcommitしないでください。
6. 実機Bodyを使う場合は[Firmware README](Firmware/Arduino/BODYLOBO/Salieri_BODYLOBO_Unified_PCA9685_10Servo_115200/README.md)を確認します。

外部DependencyとAvatarを意図的に除外しているため、clone直後は利用者による導入・参照設定が必要です。

## License

Project Salieri独自Sourceは[Project Salieri License v1.0](LICENSE)の対象です。

- 個人・非商用の自己利用：許可
- その他の利用：事前許諾が必要

第三者Dependency、利用者が追加するAsset、Avatar、Modelにはそれぞれ固有のLicenseが適用されます。Project Salieri Licenseがそれらを上書きすることはありません。

## 旧Apache Repositoryとの関係

旧公開Repository `Project-Salieri-AI-Android-Runtime` はApache License 2.0のLegacy / Architecture Referenceとして維持されます。本RepositoryのPublic v0.1は現行Project Salieri AI用の独立した公開母体であり、旧RepositoryのLicenseや履歴を変更しません。

## Firmware

Arduino Firmwareは次に収録しています。

[`Firmware/Arduino/BODYLOBO/Salieri_BODYLOBO_Unified_PCA9685_10Servo_115200`](Firmware/Arduino/BODYLOBO/Salieri_BODYLOBO_Unified_PCA9685_10Servo_115200)

Public Firmware Manifestは[PUBLIC_V0.1_FIRMWARE_MANIFEST.md](PUBLIC_V0.1_FIRMWARE_MANIFEST.md)を参照してください。
