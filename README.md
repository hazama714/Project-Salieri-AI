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

## はじめに読む資料

Project Salieri AIを改造・検証してみたい場合は、まず[START_HERE.md](START_HERE.md)を参照してください。

レイヤー構成、現在のRuntime Flow、VirtualBody、Free Pose、Physical Retarget、Communication、Memory / Recallなどを、Source Codeを細部まで読む前に全体像としてつかめるようにまとめています。

## Public v0.1の範囲

- Unity Runtimeの公開対象Source 502件
- PC Editor / Android向けの共通Runtime基盤
- Attention、Conversation、Experience Record基盤、Recall基盤、World Memory基盤、Free Pose、VRM / IK、VirtualBody、Physical Control、Safety、Communication
- Studio Hazama 714制作のVirtualBody Runtime用Robot Body Model 2件
- Arduino + PCA9685向けBODYLOBO Firmware 1件

Source Versionはファイル単位で管理し、Public v0.1の初回公開対象は`0.1.0`から開始します。運用規則は[Source Versioning](SOURCE_VERSIONING.md)を参照してください。

## 収録しないもの

- Asis3D AvatarおよびAvatar由来Asset
- VRMファイル、Avatar由来Mesh、Texture、Material、BlendShape、Avatar、MetaObject
- Local LLM RuntimeおよびGGUF Model
- OpenCV for Unity Asset Store内容
- Vosk / VOICEVOX / Open JTalk / UniDicのModel・Dictionary・Native Binary
- ONNX Model、DLL、SO、AAR、JAR
- API Key、credential、Runtime database、log、build成果物

このRepositoryに収録する`Asis3D_3_Vbody.fbx`と`AILob.fbx`は、Studio Hazama 714が制作したRobot Body / VirtualBody用Assetです。表示・IK解・物理Retargetの基盤となるVirtualBodyであり、会話キャラクターとして表示するVRM Avatarとは別物です。

完成済みAvatarや第三者Binaryは再配布しません。利用者は自分のVRMと必要な外部Dependencyを用意してください。導入条件は[DEPENDENCIES.md](DEPENDENCIES.md)、外部コンポーネントの権利情報は[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)にまとめています。

## 実装成熟度

- Experience Record foundation：回答と対象証拠の保存基盤を実装済み
- Recall foundation：保存済みExperienceの検索・Known判定基盤を実装済み
- World Memory infrastructure：Entity / Fact / Evidence / ProvenanceおよびSQLite永続化基盤を実装済み
- Visual identity matching：単一frameの64-bit dHash exact matchであり、照明・角度・crop変化に対する安定性は実験段階

本Repositoryはturnkey完成品ではありません。第三者Dependency、Native runtime、Model、辞書、利用者VRMの導入と、Scene参照の設定が必要です。

## セットアップ概要

1. Unity `2022.3.62f3`でProjectを開きます。
2. [DEPENDENCIES.md](DEPENDENCIES.md)に従って外部Package、Asset、Native Runtime、Modelを導入します。
3. 自分のVRM0 Avatarを配置し、Animator / IK / Scene参照を設定します。
4. Cloud Conversationを使う場合は、Unity上でOpenAI API Keyを各自設定します。
5. API KeyをGitへcommitしないでください。
6. 実機Bodyを使う場合は[Firmware README](Firmware/Arduino/BODYLOBO/Salieri_BODYLOBO_Unified_PCA9685_10Servo_115200/README.md)を確認します。

外部DependencyとAvatarを意図的に除外しているため、clone直後は利用者による導入・参照設定が必要です。収録VirtualBody FBXはAvatarの代替ではありません。

## License

Project Salieri独自Sourceは[Project Salieri License v1.0](LICENSE)の対象です。

- 個人・非商用の自己利用：許可
- その他の利用：事前許諾が必要

第三者Dependency、利用者が追加するAsset、Avatar、Modelにはそれぞれ固有のLicenseが適用されます。Project Salieri Licenseがそれらを上書きすることはありません。

## Apache 2.0 Repositoryとの関係

別公開Repository `Project-Salieri-AI-Android-Runtime` は、`Android Edge AI Runtime for Unity` に関連する公開RepositoryとしてApache License 2.0のまま維持されます。本RepositoryのPublic v0.1はProject Salieri AI用の独立した公開母体であり、同RepositoryのLicenseや既に公開されたVersionの権利を変更しません。

## Firmware

Arduino Firmwareは次に収録しています。

[`Firmware/Arduino/BODYLOBO/Salieri_BODYLOBO_Unified_PCA9685_10Servo_115200`](Firmware/Arduino/BODYLOBO/Salieri_BODYLOBO_Unified_PCA9685_10Servo_115200)

Public Firmware Manifestは[PUBLIC_V0.1_FIRMWARE_MANIFEST.md](PUBLIC_V0.1_FIRMWARE_MANIFEST.md)を参照してください。
