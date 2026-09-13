# Project Salieri AI - Modification Guide

この資料は、Project Salieri AI Public v0.1 を実際に触ってみたい人向けの「最初にどこを変えるか」ガイドです。

全体像は [START_HERE.md](START_HERE.md)、主要Sourceの役割と接続は [SOURCE_GUIDE.md](SOURCE_GUIDE.md) を先に参照してください。

Project Salieri AIでは、上位ほど意味や意図、下位ほど身体・Servo・通信など実機固有の処理を扱います。

```text
Perception / Attention / Conversation
                ↓
         Character Motion
                ↓
          VirtualBody
                ↓
     Physical Retarget / Safety
                ↓
        Communication / Hardware
```

改造するときは、いきなり下位のServo処理まで触るのではなく、**自分が変えたい責任のレイヤーから見る**のが基本です。

---

## 1. ポーズを増やしたい

現在のFree Poseでは、Semantic Targetを選び、そのTargetをHand IKへ接続します。

```text
Semantic Target
→ Free Pose
→ HandTargetAuthority
→ VRM Animator IK
→ RobotArmIKSolver
→ VirtualBody
→ Physical Body
```

最初に見る場所：

- `SemanticBodyTargetCatalogV0`
- `SpatialTargetRegistry`
- `ProductionPoseRequestService`
- `FreePoseExecutor`
- `FreePoseArmExecutor`

まずはTargetやPose定義を増やす方向から確認し、IK SolverやServo Mappingまで同時に変更しない方が追いやすくなります。

---

## 2. 手の届く位置や腕の動きを変えたい

Targetは合っているのに腕の追従や姿勢を変えたい場合は、Character Motion / VirtualBody側を見ます。

```text
Hand Target
→ VRMArmTargetIKController
→ VRM solved Hand
→ RobotArmIKSolver
→ BodyJointConstraint
```

最初に見る場所：

- `VRMArmTargetIKController`
- `RobotArmIKSolver`
- `BodyJointConstraint`

`BodyJointConstraint`はVirtualBody Jointの最終Writerなので、可動域を変える場合はここが重要な境界です。

---

## 3. 首の向き方やLOOKを変えたい

「どこを見るか」と「首をどう動かすか」は別の責任です。

```text
Face / Object / LOOK Request
→ OrientationTargetResolver
→ AttentionTarget
→ VRM LookAt
→ Neck Retarget
→ NeckController
```

見る対象の選び方を変える場合：

- `OrientationPriorityRequestService`
- `OrientationTargetResolver`

首の物理的な動き方を変える場合：

- `VirtualNeckRetargetShadow`
- `PhysicalNeckOutputOwner`
- `NeckController`

Attention選択とServo出力を同時に変更しない方が、原因を追いやすくなります。

---

## 4. Servoの向き・中心・可動域を変えたい

VirtualBody側の動きは正しいが、実機Servo側だけ合わない場合はPhysical Retargetを見ます。

```text
BodyJointConstraint.AppliedAngle
→ BodyJointServoBridge
→ ServoControlUnit
→ BodyCommandCoordinator
```

最初に見る場所：

- `BodyJointServoBridge`
- `ServoControlUnit`

ここでは主に、実機ごとのInvert、Offset、Min / Max、Enableなどを扱います。

VirtualBody自体の姿勢まで変えてしまう前に、まずRetarget / Servo設定側で調整できるか確認してください。

---

## 5. Arduinoや通信方式を変えたい

身体の計算はそのままで、出力先だけ変えたい場合はCommunication Layerを見ます。

```text
BodyCommandCoordinator
→ Platform Sender
→ Serial / USB / Bluetooth
→ Arduino
```

最初に見る場所：

- `BodyCommandCoordinator`
- `SerialSender_PC_Body`
- `AndroidUsbSerialSender`
- `AndroidBluetoothSender`
- `RuntimeConnectionSettings`

現在はWindows SerialとAndroid USB Serialが主要経路で、Bluetooth経路も保持されています。

---

## 6. 会話やLLMの振り分けを変えたい

入力、会話判定、Generation、Provider選択は別の責任に分かれています。

```text
UserSpeechRouter
→ ExternalInputBuffer
→ RuntimeProcessor
→ ConversationReactionService
→ ConversationService
→ LLMRouteController
→ Provider
```

最初に見る場所：

- 入力：`UserSpeechRouter`
- 会話振り分け：`ConversationReactionService`
- Generation：`ConversationService`
- Cloud / Local切替：`LLMRouteController`

Emergency / Stop系は通常Conversationより先に処理されるため、会話系を変更するときもSafety境界を維持してください。

---

## 7. 顔・物体認識やAttentionを変えたい

認識と「何を見るか」は分離されています。

Object側の概略：

```text
Camera
→ YOLOX Detection
→ ByteTrack
→ Observation Target Selection
→ Orientation Candidate
→ OrientationTargetResolver
```

最初に見る場所：

- 顔認識：`FaceDetector_OpenCV` / `FacePerceptionBuffer`
- Object認識：`YoloXObjectDetectionService`
- Tracking：`ByteTrackObjectTrackingService`
- Object選択：`ObservationTargetSelectionService`
- Attention統合：`OrientationTargetResolver`

認識精度を変えたいのか、認識後の対象選択を変えたいのかを分けて考えると追いやすくなります。

---

## 8. Memory / Recallを試したい

現在のProduction Experience Save / RecallはJSON経路です。

```text
Observation Target
→ Recall
→ Known / Unknown
→ LOOK / POINT / ASK
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

## 9. 改造時に確認する順番

問題が起きたときは、まず次の順で境界を確認します。

```text
1. Inputは届いているか
2. Authorityは誰が持っているか
3. そのSourceのOutputは正しいか
4. 次のSourceへ渡っているか
5. VirtualBodyまでは正しいか
6. Physical Retarget後も正しいか
7. TransportへCommandが届いているか
8. 実機が実際に到達したか
```

特にProject Salieri AIでは、

```text
Commanded / Sent != Physical Position Reached
```

という境界があります。現在のPublic v0.1では、ServoへCommandを送ったことと実機がその角度へ到達したことは同じ意味ではありません。

---

## 10. 迷ったときの基準

Project Salieri AIを改造するときは、まず既存の責任境界を使ってください。

- 意味・意図を変えたい → 上位Layer
- キャラクターの動きを変えたい → Character Motion
- 仮想身体の構造を変えたい → VirtualBody
- 実機固有の角度やServo設定を変えたい → Physical Retarget
- 接続先を変えたい → Communication

一つの変更で複数Layerを同時に触るより、どのLayerで差が生まれているかを確認しながら一段ずつ変更する方が、Project Salieri AIの現在の構造を追いやすくなります。
