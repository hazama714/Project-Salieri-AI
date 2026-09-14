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

Project Salieri AIを改造・検証してみたい場合は、次の順で参照してください。

1. [START_HERE.md](START_HERE.md) — レイヤー構成、Runtime Flow、VirtualBody、Free Pose、Physical Retarget、Communication、Memory / Recallなど、まず全体像をつかむための「地図」
2. [SOURCE_GUIDE.md](SOURCE_GUIDE.md) — 主要Sourceの役割、Input / Output、次に渡すSource、Authorityを機能単位で追うためのガイド
3. [MODIFICATION_GUIDE.md](MODIFICATION_GUIDE.md) — ポーズ、IK、首、Servo、通信、会話、認識、Memoryなど、目的別に「最初にどこを触るか」を確認する実践ガイド

Source Codeを細部まで読む前に、この3つを見ることで「何がどこにあり、どの責任を変更すると何が変わるか」を把握しやすくしています。

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

現在のmain branch上のProject Salieri独自Sourceは[Project Salieri License v1.1](LICENSE)の対象です。

Project Salieri License v1.1では、個人・独立した個人事業主による研究、制作、DIY活動に加えて、一定範囲のIndependent Creator活動を明確化しています。

主な考え方は次のとおりです。

- 個人によるSourceの取得、実行、閲覧、研究、技術評価、自己利用のための改造
- 自分のAI・ロボット・Avatar Projectへの組み込み
- 動作デモ、展示、画像・動画・Livestream公開
- 条件を満たすIndependent Creatorによる収益化された動画・配信・展示など
- 独自に制作したSalieri Compatible Body、3D Model、Hardware、Accessory等の制作・販売・レンタル
- Independent Creatorの年間事業収入基準はJPY 10,000,000以下。ただし、これはProject Salieri独自のLicense基準であり、税法上の区分を意味しません

一方、次のような利用は事前許諾が必要です。

- Company、Organization、Educational / Research Institutionなどによる利用
- 企業・Client等のために行う受託、委託、Consulting、Outsourcing
- Source、改変Source、Binary、Project Salieri由来Assetの第三者への再配布
- Project Salieri本体を含むProduct、Application、Service、SaaS、Commercial Robot等の第三者提供
- Independent Creatorの範囲を超える大規模な事業利用
- 個人名義、子会社、事業分割、Sponsor名目などを利用したLicense条件の迂回

第三者が独自に制作したCompatible Body、3D Model、Hardware等は、それ自体がProject Salieri SourceやVirtualBody Assetを含まず、それらから派生していない限り、Project Salieri Licenseの対象にはなりません。作者自身がその作品のLicenseを決定できます。

Project Salieri License v1.1は、外部技術やBodyを公式Referenceへ取り込む場合の権利関係についても明確化しています。第三者特許、著作権、Design Right等は自動的にProject Salieriへ移転・許諾されるものではなく、Communityの継続的な研究・DIY利用を妨げる可能性のある権利条件については、公式Referenceへの採用時に別途Rights Clearanceを求める方針です。

Project Salieri License v1.0で既に配布されたVersionは、引き続きv1.0の条件で扱われます。v1.1が過去のv1.0配布物へ遡及適用されることはありません。v1.0本文は[`LICENSES/Project-Salieri-License-1.0.txt`](LICENSES/Project-Salieri-License-1.0.txt)として保存しています。

第三者Dependency、利用者が追加するAsset、Avatar、Modelにはそれぞれ固有のLicenseが適用されます。Project Salieri Licenseがそれらを上書きすることはありません。

## Apache 2.0 Repositoryとの関係

別公開Repository `Project-Salieri-AI-Android-Runtime` は、`Android Edge AI Runtime for Unity` に関連する公開RepositoryとしてApache License 2.0のまま維持されます。本RepositoryのPublic v0.1はProject Salieri AI用の独立した公開母体であり、同RepositoryのLicenseや既に公開されたVersionの権利を変更しません。

## Firmware

Arduino Firmwareは次に収録しています。

[`Firmware/Arduino/BODYLOBO/Salieri_BODYLOBO_Unified_PCA9685_10Servo_115200`](Firmware/Arduino/BODYLOBO/Salieri_BODYLOBO_Unified_PCA9685_10Servo_115200)

Public Firmware Manifestは[PUBLIC_V0.1_FIRMWARE_MANIFEST.md](PUBLIC_V0.1_FIRMWARE_MANIFEST.md)を参照してください。
