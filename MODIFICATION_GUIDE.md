# Project Salieri AI - Modification Guide

この資料は、Project Salieri AI Public v0.1 を実際に触ってみたい人向けの「最初にどこを変えるか」ガイドです。

全体像は [START_HERE.md](START_HERE.md)、主要Sourceの役割と接続は [SOURCE_GUIDE.md](SOURCE_GUIDE.md) を先に参照してください。

本資料では、HP・`START_HERE.md`・`SOURCE_GUIDE.md`と同じL1〜L7のレイヤー体系を使います。

```text
L1  Perception
      Camera / Face / Object / STT
          ↓
L2  Attention / Conversation / Intent
      Orientation / LLM / Body Intent
          ↓
L3  Character Motion
      VRM / Animator IK / LOOK / POINT / Free Pose
          ↓
L4  VirtualBody
      Joint Constraints / Body Representation
          ↓
L5  Physical Retarget / Safety
      Mapping / Clamp / Authority / Permission
          ↓
L6  Communication
      BodyCommandCoordinator / Serial / USB / Bluetooth
          ↓
L7  Hardware
      Arduino / PCA9685 / Servo / Physical Body
```

基本原則は、**上位ほど意味や意図、下位ほど身体固有の事情を扱う**ことです。

改造するときは、いきなり下位のServo処理まで触るのではなく、まず**自分が変えたい責任のレイヤーから見る**のが基本です。

---

## 1. ポーズを増やしたい — L2 → L3

現在のFree Poseでは、L2で選ばれたSemantic TargetをL3のCharacter Motionへ渡し、Hand IKへ接続します。

```text
L2 Semantic Target
→ L3 Free Pose
→ HandTargetAuthority
→ VRM Animator IK
→ RobotArmIKSolver
→ L4 VirtualBody
→ L5 Physical Retarget
→ L6 Communication
→ L7 Physical Body
```

最初に見る場所：

- `SemanticBodyTargetCatalogV0`
- `SpatialTargetRegistry`
- `ProductionPoseRequestService`
- `FreePoseExecutor`
- `FreePoseArmExecutor`

TargetやPose定義を増やしたいだけなら、まずL2〜L3で完結できるか確認します。IK SolverやServo Mappingまで同時に変更しない方が、責任境界を追いやすくなります。

---

## 2. 手の届く位置や腕の動きを変えたい — L3 → L4

Targetは合っているのに腕の追従や姿勢を変えたい場合は、L3 Character MotionからL4 VirtualBodyへの境界を見ます。

```text
L3 Hand Target
→ VRMArmTargetIKController
→ VRM solved Hand
→ RobotArmIKSolver
→ L4 BodyJointConstraint
```

最初に見る場所：

- `VRMArmTargetIKController`
- `RobotArmIKSolver`
- `BodyJointConstraint`

`BodyJointConstraint`はVirtualBody Jointの最終Writerなので、VirtualBody側の可動域を変える場合はL4の重要な境界です。

---

## 3. 首の向き方やLOOKを変えたい — Cross-layer L2 → L3 → L5 → L4 → L6

LOOK / Neckは複数レイヤーを横断します。

```text
L1 Face / Object
→ L2 OrientationTargetResolver
→ AttentionTarget
→ L3 VRM LookAt
→ L5 Neck Retarget / Safety
→ NeckController
├→ L4 VirtualBody Neck
└→ L6 BodyCommandCoordinator
   → L7 Physical Neck
```

見る対象の選び方を変える場合：

- `OrientationPriorityRequestService`
- `OrientationTargetResolver`

キャラクター側のLookAtを変える場合：

- `UpperBodyAnimatorIKCoordinator`
- `VrmHeadLookAtVisual`

首のPhysical Retargetや出力を変える場合：

- `VirtualNeckRetargetShadow`
- `PhysicalNeckOutputOwner`
- `NeckController`

Attention選択とPhysical出力を同時に変更せず、どのレイヤーで期待値との差が出ているかを先に確認します。

---

## 4. Servoの向き・中心・可動域を変えたい — L5

L4 VirtualBody側の動きは正しいが、実機Servo側だけ合わない場合はL5 Physical Retarget / Safetyを見ます。

```text
L4 BodyJointConstraint.AppliedAngle
→ L5 BodyJointServoBridge
→ ServoControlUnit
→ L6 BodyCommandCoordinator
→ L7 Servo
```

最初に見る場所：

- `BodyJointServoBridge`
- `ServoControlUnit`

ここでは主に、実機ごとのInvert、Offset、Min / Max、Enableなどを扱います。

VirtualBody自体の姿勢まで変える前に、まずL5で調整できるか確認してください。

---

## 5. Arduinoや通信方式を変えたい — L6 → L7

身体の計算はそのままで、出力先だけ変えたい場合はL6 CommunicationとL7 Hardwareを見ます。

```text
L6 BodyCommandCoordinator
→ Platform Sender
→ Serial / USB / Bluetooth
→ L7 Arduino / PCA9685 / Servo
```

最初に見る場所：

- `BodyCommandCoordinator`
- `SerialSender_PC_Body`
- `AndroidUsbSerialSender`
- `AndroidBluetoothSender`
- `RuntimeConnectionSettings`
- Arduino Firmware

現在はWindows SerialとAndroid USB Serialが主要経路で、Bluetooth経路も保持されています。

---

## 6. 会話やLLMの振り分けを変えたい — L1 → L2

入力、会話判定、Generation、Provider選択はL1〜L2の責任です。

```text
L1 UserSpeechRouter
→ ExternalInputBuffer
→ RuntimeProcessor
→ L2 ConversationReactionService
→ ConversationService
→ LLMRouteController
→ Provider
```

最初に見る場所：

- 入力：`UserSpeechRouter`
- 会話振り分け：`ConversationReactionService`
- Generation：`ConversationService`
- Cloud / Local切替：`LLMRouteController`

Emergency / Stop系は通常Conversationより先に処理されるため、L2を変更するときもSafety / Permission境界を維持してください。

---

## 7. 顔・物体認識やAttentionを変えたい — L1 → L2

認識はL1、認識結果から「何を見るか」を決める処理はL2です。

Object側の概略：

```text
L1 Camera
→ YOLOX Detection
→ ByteTrack
→ Observation Target Selection
→ Orientation Candidate
→ L2 OrientationTargetResolver
```

最初に見る場所：

- 顔認識：`FaceDetector_OpenCV` / `FacePerceptionBuffer`
- Object認識：`YoloXObjectDetectionService`
- Tracking：`ByteTrackObjectTrackingService`
- Object選択：`ObservationTargetSelectionService`
- Attention統合：`OrientationTargetResolver`

認識精度を変えたいのか、認識後の対象選択を変えたいのかをL1 / L2で分けて考えると追いやすくなります。

---

## 8. Memory / Recallを試したい — Cross-layer L1 / L2 / L3

Memory / Recallは一つの身体レイヤーに閉じず、観測・判断・質問・身体表現を横断します。

```text
L1 Observation Target
→ Recall
→ L2 Known / Unknown
→ L3 LOOK / POINT / ASK
→ User Answer
→ Answer Binding
→ Experience Save
→ JSON Store
```

最初に見る場所：

- `ExperienceRecallResolver`
- `AnswerBindingService`
- `ExperiencePersistenceService`
- `JsonFileExperienceStore`
- `FindPointAskBehaviorController`

SQLite World Memory subsystemは存在しますが、Public v0.1時点では通常Production経路へ完全接続された状態ではありません。

---

## 9. Safety / Authorityを変えたい — Cross-layer L2 / L5 / L6

Safety / Authorityは特定の1レイヤーだけではなく、意味側・身体側・通信側の境界に配置されています。

代表例：

| Responsibility | Layer | Current Owner |
|---|---|---|
| Interaction State | L2 | `InteractionStateController` |
| State別Permission | L2 / L5 | `LimboPermissionResolver` |
| Camera / LLM / Voice / Servo利用可否 | Cross-layer | `ExecutionResourceManager` |
| Head Request Arbitration | L2 | `OrientationPriorityRequestService` |
| Right / Left Hand Writer | L3 | `HandTargetAuthority` |
| Physical Neck Writer境界 | L5 | `PhysicalNeckOutputOwner` |
| Physical Arm Enable | L5 | `ArmServoOutputController` |
| Transport Dispatch | L6 | `BodyCommandCoordinator` |

Emergency / Stop系は通常Conversationより先に扱われます。

---

## 10. 改造時に確認する順番

問題が起きたときは、L1からL7へ順番に境界を確認します。

```text
L1  Input / Perceptionは届いているか
 ↓
L2  Attention / Conversation / Intentは正しいか
 ↓
L3  Character MotionのTarget / IKは正しいか
 ↓
L4  VirtualBodyのJoint Stateは正しいか
 ↓
L5  Retarget / Clamp / Safety後も正しいか
 ↓
L6  TransportへCommandが届いているか
 ↓
L7  実機が実際に到達したか
```

途中の各境界では、あわせて次を確認します。

```text
- Authorityは誰が持っているか
- そのSourceのOutputは正しいか
- 次のSourceへ渡っているか
- Writerが複数になっていないか
```

特にProject Salieri AIでは、

```text
Commanded / Sent != Physical Position Reached
```

という境界があります。Public v0.1では、ServoへCommandを送ったことと実機がその角度へ到達したことは同じ意味ではありません。

---

## 11. 迷ったときのL1〜L7早見表

| 変えたいもの | 最初に見るLayer | 主な領域 |
|---|---:|---|
| 顔・物体・音声入力 | L1 | Perception |
| 見る対象・会話・意図 | L2 | Attention / Conversation / Intent |
| ポーズ・LOOK・POINT・IK | L3 | Character Motion |
| 仮想身体のJoint構造 | L4 | VirtualBody |
| 実機角度・可動域・Servo設定 | L5 | Physical Retarget / Safety |
| PC / Android通信経路 | L6 | Communication |
| Arduino / PCA9685 / Servo | L7 | Hardware |
| 首LOOK・Memory・Safety | Cross-layer | 複数Layerを順番に確認 |

一つの変更で複数Layerを同時に触るより、どのLayerで差が生まれているかを確認しながら一段ずつ変更する方が、Project Salieri AIの現在の構造を追いやすくなります。

---

## 12. 次に読むもの

1. [START_HERE.md](START_HERE.md) — L1〜L7の全体地図
2. [SOURCE_GUIDE.md](SOURCE_GUIDE.md) — 各Layerに属する主要Sourceと実行Flow
3. この`MODIFICATION_GUIDE.md` — 目的別に最初に触るLayerとSource
4. [DEPENDENCIES.md](DEPENDENCIES.md) — 外部依存関係
5. [Firmware README](Firmware/Arduino/BODYLOBO/Salieri_BODYLOBO_Unified_PCA9685_10Servo_115200/README.md) — L7 Hardware側

HP、`START_HERE.md`、`SOURCE_GUIDE.md`、`MODIFICATION_GUIDE.md`は、同じL1〜L7レイヤー体系を共通の設計地図として使用します。
