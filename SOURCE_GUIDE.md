# Project Salieri AI - Source Guide

この資料は、Project Salieri AI Public v0.1 の主要Sourceについて、**何を担当し、何を受け取り、どこへ渡すのか**を機能単位で確認するためのガイドです。

全502 Sourceの完全一覧ではありません。まず全体像をつかみたい場合は [START_HERE.md](START_HERE.md) を参照してください。

本資料は、Production SourceとMainSceneの有効Component・参照を照合した監査結果を基準にしています。Project Salieri AIは開発中のため、今後Source名、経路、Body構成、制御方式は変更される可能性があります。

---

## 1. L1-L7 Layer Map

本資料では、HPと`START_HERE.md`と同じ7レイヤーを使ってSourceの位置を示します。

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

すべての機能が必ず1つのレイヤーだけに収まるわけではありません。LOOK / Neck、Memory / Recall、Safety / Authorityのように複数レイヤーを横断する機能は、Cross-layerとして扱います。

---

## 2. Runtime全体の主要フロー

```text
Camera / Microphone / Text / Runtime Request
                    │
        ┌───────────┴───────────┐
        │                       │
  L1 Perception          L2 Conversation
        │                       │
Face / Object Candidate     Reaction / Activation
        │                       │
L2 Orientation Resolver     ConversationService
        │                       │
Attention Target       Cloud / Local LLM Response
        │                 ┌─────┴─────┐
        │                 │           │
        │              Speech     Body Intent
        │                 │           │
        │          VoicePlayback  L3 Free Pose / POINT
        │                             │
        └──────────────┬──────────────┘
                       │
                L3 VRM Animator IK
                 ┌─────┴─────┐
                 │           │
             Neck/Head    Solved Hands
                 │           │
          Neck Retarget  RobotArmIKSolver
                 │           │
          NeckController  L4 BodyJointConstraint
                 │           │
          L4 VBody Neck   L4 VBody Arms
                 │           │
          Neck Servo   L5 BodyJointServoBridge
                 └─────┬─────┘
                       │
             L6 BodyCommandCoordinator
             ┌─────────┴──────────┐
             │                    │
       Windows Serial       Android Transport
             │              USB / Bluetooth
             └─────────┬──────────┘
                       │
               L7 Arduino / Servo
```

---

## 3. L1 → L2 Perception / Attention

### 役割

CameraからFace / Objectを観測し、複数の候補から現在のAttention Targetを解決します。

- L1：観測・検出・追跡
- L2：候補の選択・Attention解決

### 主なフロー

```text
L1 CameraInput
├─ FaceDetector_OpenCV
│  → FacePerceptionBuffer
│  → FaceAttentionTargetDriver
│  → FaceOrientationTargetSource
│
└─ YoloXObjectDetectionService
   → ByteTrackObjectTrackingService
   → ObservationTargetSelectionService
   → ObjectOrientationTargetSource

L2 Face / Object / Priority Request
→ OrientationTargetResolver
→ OrientationResolutionTargetDriver
→ AttentionTarget
```

### 主要Source

| Layer | Source | 主な役割 | Input | Output / 次 |
|---|---|---|---|---|
| L1 | [`CameraInput.cs`](Assets/Sensors/Camera/Common/CameraInput.cs) | Camera開始と現在Frame公開 | WebCamTexture | CurrentTexture |
| L1 | [`FaceDetector_OpenCV.cs`](Assets/Sensors/Camera/OpenCV/FaceDetector_OpenCV.cs) | 顔検出 | Camera Texture | Face位置・サイズ |
| L1 | [`FacePerceptionBuffer.cs`](Assets/Core/Perception/Face/FacePerceptionBuffer.cs) | 顔検出状態の安定化 | Raw Face State | Stable / TemporaryLost / FullyLost |
| L1→L2 | [`FaceAttentionTargetDriver.cs`](Assets/Core/Perception/Attention/FaceAttentionTargetDriver.cs) | Face位置をWorld Targetへ変換 | Face Center | Face Attention Target |
| L2 | [`FaceOrientationTargetSource.cs`](Assets/Core/Perception/Attention/FaceOrientationTargetSource.cs) | FaceをOrientation候補へ変換 | Face Target | Candidate |
| L1 | [`YoloXObjectDetectionService.cs`](Assets/Core/Perception/Object/YoloXObjectDetectionService.cs) | Object Detection | Camera Frame | Detections |
| L1 | [`ByteTrackObjectTrackingService.cs`](Assets/Core/Perception/Object/Tracking/ByteTrackObjectTrackingService.cs) | DetectionをTrackへ接続 | Detections | TrackedObjectSet |
| L1→L2 | [`ObservationTargetSelectionService.cs`](Assets/Core/Perception/Object/Targeting/ObservationTargetSelectionService.cs) | Observation Target選択 | Tracks | CurrentTarget |
| L2 | [`ObjectOrientationTargetSource.cs`](Assets/Core/Perception/Attention/ObjectOrientationTargetSource.cs) | ObjectをOrientation候補へ変換 | Selected Target | Candidate |
| L2 | [`OrientationPriorityRequestService.cs`](Assets/Core/Perception/Attention/OrientationPriorityRequestService.cs) | LOOK等の明示要求を優先度・期限付きで保持 | Orientation Request | Active Request |
| L2 | `OrientationTargetResolver` | Face / Object / Priority等を単一解決 | Candidates | Resolution |
| L2 | `OrientationResolutionTargetDriver` | 解決結果を共有Attention Transformへ反映 | Resolution | AttentionTarget |

### Authority

- Orientation解決：`OrientationTargetResolver`
- 明示要求：`OrientationPriorityRequestService`
- Object Target：`ObservationTargetSelectionService.CurrentTarget`
- 最終Attention Transform：`OrientationResolutionTargetDriver`

---

## 4. L1 → L2 Conversation

### 役割

STTやText InputをRuntimeへ取り込み、会話判定、LLM Routing、発話、Body Intentへ接続します。

Speech InputはL1の入力から始まり、会話判定とIntent生成はL2で扱います。

### 主なフロー

```text
L1 STT Final / Text Input
→ AndroidSTTReceiver / Text Input
→ UserSpeechRouter
→ UserSpeechInputBuffer
→ ExternalInputBuffer
→ AutonomousClock
→ RuntimeProcessor
→ CognitiveReflexController
→ L2 ConversationReactionService
→ ConversationService
→ LLMRouteController
→ Cloud / Local Provider
→ ResponseBus / Body Adapter
```

### 主要Source

| Layer | Source | 主な役割 | Input | Output / 次 |
|---|---|---|---|---|
| L1 | [`AndroidSTTReceiver.cs`](Assets/Sensors/Audio/AndroidSTTReceiver.cs) | Platform STT Finalの入口 | Transcript | UserSpeechRouter |
| L1→L2 | [`UserSpeechRouter.cs`](Assets/Sensors/Audio/UserSpeechRouter.cs) | InputId等を付与してSpeechをRuntime形式へ変換 | Text | CommunicationInput |
| L1→L2 | [`UserSpeechInputBuffer.cs`](Assets/Core/Perception/Speech/UserSpeechInputBuffer.cs) | Speech状態保持とExternal Input化 | CommunicationInput | External Event |
| L2 | `ExternalInputBuffer` | 優先度付き入力Queue | Input | Queued Event |
| L2 | `AutonomousClock` | QueueからRuntimeへ入力供給 | Queued Event | RuntimeProcessor |
| L2 | `RuntimeProcessor` | InputType別のRuntime dispatch | External Input | Cognitive Reflex |
| L2 | `CognitiveReflexController` | UserSpeechをConversationへ接続 | Speech | ConversationReactionService |
| L2 | [`ConversationReactionService.cs`](Assets/Core/Reflex/Cognitive/ConversationReactionService.cs) | Safety、Activation、Pending Question、通常Conversationの振り分け | Speech | Conversation / Action / Answer |
| L2 | [`ConversationService.cs`](Assets/Core/Reflex/Cognitive/ConversationService.cs) | 1 TurnのGrounding・Generation・Body/Speech出力 | Conversation Request | LLM Response |
| L2 | [`LLMRouteController.cs`](Assets/Core/LLM/Common/LLMRouteController.cs) | Cloud / Local選択とFallback | Generation Request | Provider |
| L2 | `CloudConversationProvider` | Cloud Client Adapter | Request | Structured Response |
| L2 | `LocalConversationProvider` | Local Client Adapter | Request | Response |
| L2 | `ResponseBus` | 応答Envelope配信 | Response | Playback |
| L2 | `VoicePlaybackController` | TTSとSpeaking lifecycle | Text | Audio / Speech Event |

### Authority

- Speech routing：`ConversationReactionService`
- Generation：`ConversationService`
- Provider選択：`LLMRouteController`
- Speech Playback：`VoicePlaybackController`

Public v0.1のMainSceneではCloud Conversationが主な実運用経路で、Local Providerは代替経路として保持されています。

---

## 5. L2 → L3 Free Pose / Arm

### 役割

会話やBody Intentから選ばれたSemantic Targetを、実際のHand Targetへ変換し、Character Motionへ接続します。

- L2：Semantic Target / Body Intent
- L3：Free Pose / Hand Target / Animator IKへの入力

### 主なフロー

```text
L2 Cloud Conversation Response
├─ Explicit Hand Target
│  → ConversationalHandTargetProductionAdapter
│
└─ Body Expression Intent
   → BodyExpressionIntentProductionAdapter

→ ProductionPoseRequestRuntime
→ ProductionPoseRequestService
→ L3 FreePoseExecutor
→ FreePoseArmExecutor
→ SpatialTargetRegistry
→ HandTargetAuthority
→ VRM Hand Target
```

### 主要Source

| Layer | Source | 主な役割 | Input | Output / 次 |
|---|---|---|---|---|
| L2→L3 | [`ConversationalHandTargetProductionAdapter.cs`](Assets/Core/Reflex/Cognitive/ConversationalHandTargetProductionAdapter.cs) | LLMの明示Hand Targetを検証してProduction Poseへ渡す | Structured Response | Target Request |
| L2→L3 | [`BodyExpressionIntentProductionAdapter.cs`](Assets/Core/Reflex/Cognitive/BodyExpressionIntentProductionAdapter.cs) | Body Intentを既存PoseへGrounding | Semantic Intent | Pose Request |
| L2→L3 | [`SemanticBodyTargetCatalogV0.cs`](Assets/Body/FreePose/Production/SemanticBodyTargetCatalogV0.cs) | 利用可能Target IDのCatalog | Target ID | Validated Target |
| L3 | [`ProductionPoseRequestService.cs`](Assets/Body/FreePose/Production/ProductionPoseRequestService.cs) | Production RequestをPoseDefinitionへ変換 | Request | FreePoseExecutor |
| L3 | [`FreePoseExecutor.cs`](Assets/Body/FreePose/FreePoseExecutor.cs) | 両腕・Headを1 Poseとして開始・置換・Release | PoseDefinition | Arm / Head Executor |
| L3 | [`FreePoseArmExecutor.cs`](Assets/Body/FreePose/FreePoseArmExecutor.cs) | Target lookup、Lease、補間Submit | Semantic Target | Hand Position |
| L3 | [`SpatialTargetRegistry.cs`](Assets/Body/SpatialTarget/SpatialTargetRegistry.cs) | 固定Spatial TargetのSource of Truth | ID | Transform |
| L3 | [`HandTargetAuthority.cs`](Assets/Body/SpatialTarget/HandTargetAuthority.cs) | 左右別LeaseとHand Target単一Writer | Lease + Position | VRM Hand Target |
| L2→L3 | [`FreePoseHeadAdapter.cs`](Assets/Body/FreePose/FreePoseHeadAdapter.cs) | Head IntentをOrientation Requestへ変換 | Head Directive | Orientation Request |

### 現在の考え方

現在のFree Poseでは、Unity空間に用意されたTargetを選択し、そのTargetへ向けて腕のIK経路を動かします。

固定Targetは左右別に管理され、`current_attention_target`は現在のAttentionを利用する動的Targetです。

この方式は現在の主要実装であり、Project Salieri AI全体を将来にわたってIKだけへ限定するものではありません。

### Authority

- Target ID：`SemanticBodyTargetCatalogV0`
- Target Transform：`SpatialTargetRegistry`
- Hand Target Writer：`HandTargetAuthority`

---

## 6. L3 → L4 VRM / IK

### 役割

World上のHand TargetからVRMのHumanoid IKを解き、その結果をVirtualBodyの腕へ写します。

### 主なフロー

```text
L3 HandTargetAuthority
→ VRM Hand Target
→ VRMArmTargetIKController.OnAnimatorIK
→ VRM solved Hand Bone
→ RobotArmIKSolver.LateUpdate
→ L4 BodyJointConstraint
→ VirtualBody Arm
```

### 主要Source

| Layer | Source | 主な役割 | Input | Output / 次 |
|---|---|---|---|---|
| L3 | [`VRMArmTargetIKController.cs`](Assets/Expression/Motion/IK/VRMArmTargetIKController.cs) | Hand TargetをAnimator IKへ設定 | Hand Target | VRM solved Hand |
| L3 | `UpperBodyAnimatorIKCoordinator` | Upper Body / LookAtをAnimator IK passで調整 | Targets | VRM Chest / Head |
| L3 | `VrmHeadLookAtVisual` | VRM LookAt設定 | AttentionTarget | Solved Head |
| L3→L4 | [`RobotArmIKSolver.cs`](Assets/Body/IK/RobotArmIKSolver.cs) | VRM solved HandへVirtualBody 4軸を近づける | Solved Hand | Joint Angles |
| L4 | [`BodyJointConstraint.cs`](Assets/Body/Joint/BodyJointConstraint.cs) | Joint角度をClampしてTransformへ適用 | Target Angle | VirtualBody Joint |

### Authority

- VRM Hand IK Input：`VRMArmTargetIKController`
- VirtualBody Arm Solver：左右`RobotArmIKSolver`
- Joint Transform Writer：各`BodyJointConstraint`

---

## 7. Cross-layer LOOK / Neck（L2 → L3 → L4 → L5）

### 役割

Attention TargetをVRMのLookAtへ反映し、解決された首・頭方向をPhysical NeckへRetargetします。

LOOK / Neckは、AttentionからCharacter Motion、VirtualBody、Physical Retargetまでをまたぐ代表的なCross-layer経路です。

### 主なフロー

```text
L2 OrientationResolutionTargetDriver
→ AttentionTarget
→ L3 UpperBodyAnimatorIKCoordinator.OnAnimatorIK
→ VrmHeadLookAtVisual
→ VRM solved Neck / Head
→ VRMNeckSolvedPoseReader
→ L5 VirtualNeckRetargetShadow
→ VirtualNeckPhysicalOutputBridge
→ PhysicalNeckOutputOwner
→ FaceTrackingOutputController
→ NeckController
├─ L4 VBodyNeckPoseDriver
├─ NeckPoseFrameDriver
└─ L5 ServoControlUnit → L6 BodyCommandCoordinator
```

### 主要Source

| Layer | Source | 主な役割 | Input | Output / 次 |
|---|---|---|---|---|
| L2 | `OrientationResolutionTargetDriver` | Attention Targetの最終Writer | Resolution | AttentionTarget |
| L3 | `UpperBodyAnimatorIKCoordinator` | Animator IK callback所有 | Attention Target | VRM LookAt |
| L3 | `VrmHeadLookAtVisual` | LookAt weight / position設定 | Attention Target | Solved Head |
| L3→L5 | `VRMNeckSolvedPoseReader` | 解決後のVRM Neck / Headを読む | VRM Bones | Solved Pose |
| L5 | `VirtualNeckRetargetShadow` | Solved方向をPhysical yaw/pitch候補へ変換 | Solved Pose | Retarget State |
| L5 | `VirtualNeckPhysicalOutputBridge` | Retarget StateをPhysical Ownerへ提出 | Retarget State | Physical Owner |
| L5 | `PhysicalNeckOutputOwner` | Physical Neck出力の単一受付 | State | Guarded Output |
| L5 | `FaceTrackingOutputController` | Mode / Permission / Emergency確認 | Retarget | Desired Pose |
| L5 | [`NeckController.cs`](Assets/Body/Parts/Controller/NeckController.cs) | Rate Limit / HOLD / Commanded Pose保持 | Desired Pose | Commanded Pose / Servo |
| L4 | `VBodyNeckPoseDriver` | Commanded PoseをVirtualBody Neckへ反映 | Commanded yaw/pitch | Neck Joint |

### Authority

- LOOK Target：`OrientationTargetResolver`
- Physical Neck受付：`PhysicalNeckOutputOwner`
- Commanded Neck State：`NeckController`
- VirtualBody Neck Writer：`BodyJointConstraint`

---

## 8. L4 VirtualBody

### 役割

VRMなどのキャラクター表現とPhysical Bodyの間にある中間身体です。

現在は単一Managerが全身状態を一括所有する構造ではなく、部位別のSourceが各`BodyJointConstraint`へ値を渡します。

### 現在の主な部位

| 部位 | Input | Writer | VirtualBody Output |
|---|---|---|---|
| Right Arm | VRM Right Hand solved position | `RobotArmIKSolver_R` | 4 Joint Constraints |
| Left Arm | VRM Left Hand solved position | `RobotArmIKSolver_L` | 4 Joint Constraints |
| Neck | `NeckController.Commanded*` | `VBodyNeckPoseDriver` | Neck Yaw / Pitch Constraints |
| Chest | Upper Body Candidate | `UpperBodyAnimatorIKCoordinator` | 現在は主にVRM Chest側 |
| Legs | VRM Leg系 | Existing Leg IK | 現行Free Pose経路外 |

### 重要な境界

`BodyJointConstraint`が対象Transformの最終的な`localRotation` Writerです。

---

## 9. L5 Physical Retarget / Physical Output

### 役割

VirtualBodyのJoint角度を、実機ごとのServo設定へ変換し、Physical Outputへ渡します。

### Arm Flow

```text
L4 BodyJointConstraint.AppliedAngle
→ L5 BodyJointServoBridge
→ ServoControlUnit
→ L6 BodyCommandCoordinator
→ Platform Sender
→ L7 Arduino / Servo
```

### Neck Flow

```text
L5 NeckController.CommandedYaw/Pitch
→ Neck ServoControlUnit
→ L6 BodyCommandCoordinator
→ Platform Sender
```

### 主要Source

| Layer | Source | 主な役割 | Input | Output / 次 |
|---|---|---|---|---|
| L5 | [`BodyJointServoBridge.cs`](Assets/Body/Servo/Bridge/BodyJointServoBridge.cs) | VirtualBody角をServo入力へRetarget | AppliedAngle | Servo Angle |
| L5 | [`ServoControlUnit.cs`](Assets/Body/Servo/Units/ServoControlUnit.cs) | Invert / Offset / Min / Max / Enableを適用 | Input Angle | Sender |
| L5 | `ArmServoOutputController` | 腕8軸のArm / DisarmとSafety Gate | Safety / Connection | Bridge Enable |
| L5 | [`NeckController.cs`](Assets/Body/Parts/Controller/NeckController.cs) | Neck Commanded Poseと送信管理 | Desired Pose | Neck Servo Command |
| L5→L6 | [`BodyCommandCoordinator.cs`](Assets/Body/Command/BodyCommandCoordinator.cs) | Servo Command QueueとTransport選択 | Servo / Raw Command | Selected Sender |

### Safety境界

腕Physical Outputは起動時に自動Armされず、Safety条件とStartup synchronizationを通過してから出力されます。

また、現在のPhysical契約では、Command送信完了と実際のServo到達は同義ではありません。

---

## 10. L6 Communication

### 役割

`BodyCommandCoordinator`から実機Transportを選び、ArduinoへCommandを送ります。

### 主なフロー

```text
L6 BodyCommandCoordinator
├─ Windows
│  → SerialSender_PC_Body
│  → Serial Port
│
└─ Android
   ├─ AndroidUsbSerialSender
   │  → USB-OTG
   │
   └─ AndroidBluetoothSender
      → Bluetooth

→ L7 Hardware
```

### 主要Source

| Layer | Source | 主な役割 | Input | Output |
|---|---|---|---|---|
| L6 | [`BodyCommandCoordinator.cs`](Assets/Body/Command/BodyCommandCoordinator.cs) | Platform / 設定に応じたSender選択 | Body Command | Selected Transport |
| L6 | `SerialSender_PC_Body` | Windows Serial出力 | Command | Serial Port |
| L6 | [`AndroidUsbSerialSender.cs`](Assets/App/Communication/UsbSerial/AndroidUsbSerialSender.cs) | Android USB-OTG出力 | Command | USB Serial |
| L6 | [`AndroidBluetoothSender.cs`](Assets/App/Communication/Bluetooth/AndroidBluetoothSender.cs) | Android Bluetooth出力 | Command | Bluetooth |
| L6 | [`RuntimeConnectionSettings.cs`](Assets/App/Settings/RuntimeConnectionSettings.cs) | Platform / Android Transport設定 | Serialized Settings | Transport Selection |

Public v0.1 MainSceneではWindowsはPC Serial、AndroidはUSB Serialが主設定です。Bluetoothは代替Transportとして保持されています。

---

## 11. L7 Hardware

L7はUnity RuntimeのSource Guideから見た最終出力先です。

```text
L6 Transport
→ Arduino
→ PCA9685
→ Servo
→ Physical Body
```

Arduino側の詳細は次を参照してください。

- [Firmware README](Firmware/Arduino/BODYLOBO/Salieri_BODYLOBO_Unified_PCA9685_10Servo_115200/README.md)
- [Public v0.1 Firmware Manifest](PUBLIC_V0.1_FIRMWARE_MANIFEST.md)

---

## 12. Cross-layer Memory / Recall

### 役割

Observation Targetに対するExperienceのRecall、質問、回答Binding、保存を扱います。

Memory / Recallは身体出力のL1→L7直列経路とは別に、Perception、Attention、Conversationを横断してContextを供給するSubsystemです。

### 主なフロー

```text
L1/L2 ObservationTarget
→ Visual Snapshot / RecallKey
→ ExperienceRecallResolver
→ Known / Unknown

Unknown
→ L2 LOOK / POINT / ASK
→ User Answer
→ AnswerBindingService
→ ExperiencePersistenceService
→ JsonFileExperienceStore
→ Context Refresh
```

### 主要Source

| Source | 主な役割 | Input | Output / 次 |
|---|---|---|---|
| `ObservedObjectMemoryContextRuntimeHost` | 現在ObjectのKnown / Unknown評価 | Target Change | Memory Context |
| `FindPointAskBehaviorController` | Recall、LOOK、POINT、ASK、回答保存の統合 | Selected Object | Question / Save |
| `QuestionInteractionSession` | Question / Speech / Answer lifecycle | Question + Speech Event | Accepted Receipt |
| `AnswerBindingService` | Answerを対象QuestionへBinding | Answer Receipt | Binding |
| [`ExperiencePersistenceService.cs`](Assets/Core/Experience/Storage/ExperiencePersistenceService.cs) | BindingをExperienceRecordへ変換 | Binding | Save |
| [`JsonFileExperienceStore.cs`](Assets/Core/Experience/Storage/JsonFileExperienceStore.cs) | Experience JSON永続化とLookup | Record / Key | Stored Record / Result |
| [`ExperienceRecallResolver.cs`](Assets/Core/Experience/Storage/ExperienceRecallResolver.cs) | RecallKeyからKnown等を判定 | RecallKey | Recall Result |
| [`ConversationWorkingMemory.cs`](Assets/Core/Reflex/Cognitive/ConversationWorkingMemory.cs) | 完了Conversation Turnを短期保持 | Turn Lifecycle | Recent Dialogue |

### 現在の状態

- Production Experience Save / Recall：JSON
- Conversation Working Memory：短期Runtime Memory
- SQLite World Memory：Subsystemは存在するが通常Production経路への接続は未完了
- Visual Recall：現時点ではexact matching中心の実験段階

---

## 13. Cross-layer Safety / Authority

Project Salieri AIでは、「どの処理が最後に身体へ書き込めるか」を機能ごとに分けています。

Safety / Authorityは特定の1レイヤーではなく、主にL2・L5・L6の境界を横断してRuntimeを統制します。

| 主なLayer | 責任 | Current Owner |
|---|---|---|
| L2 | Interaction State | `InteractionStateController` |
| L2 / L5 | State別Permission | `LimboPermissionResolver` |
| Cross-layer | Camera / LLM / Voice / Servo利用可否 | `ExecutionResourceManager` |
| L2 | Fixed Action実行 | `ExecutionController` |
| L2 | Emergency Speech Admission | `ConversationReactionService` |
| L2 | Head Request Arbitration | `OrientationPriorityRequestService` |
| L5 | Physical Neck Writer境界 | `PhysicalNeckOutputOwner` |
| L3 | Right / Left Hand Writer | `HandTargetAuthority` |
| L5 | Physical Arm Enable | `ArmServoOutputController` |
| L6 | Transport Dispatch | `BodyCommandCoordinator` |

Emergency / Stop系は通常Conversationより先に扱われます。

すべての身体経路が一つのExecution Orchestrationを必ず通る構造ではなく、Free PoseやLOOKなどはそれぞれ専用Authorityを持っています。

---

## 14. どこを触ればいいか

| やりたいこと | Layer | 最初に見るSource / 領域 |
|---|---|---|
| 顔認識を変えたい | L1 | `FaceDetector_OpenCV` / `FacePerceptionBuffer` |
| Object認識・追跡を変えたい | L1 | `YoloXObjectDetectionService` / `ByteTrackObjectTrackingService` |
| 見る対象を変えたい | L2 | `OrientationTargetResolver` / `OrientationPriorityRequestService` |
| 会話入力を変えたい | L1→L2 | `UserSpeechRouter` / `ConversationReactionService` |
| LLM Routingを変えたい | L2 | `LLMRouteController` |
| ポーズTargetを増やしたい | L2→L3 | `SemanticBodyTargetCatalogV0` / `SpatialTargetRegistry` |
| Hand Target制御を変えたい | L3 | `HandTargetAuthority` / `FreePoseArmExecutor` |
| 腕IKを変えたい | L3→L4 | `VRMArmTargetIKController` / `RobotArmIKSolver` |
| 首LOOKを変えたい | L2→L5 | `OrientationTargetResolver` / `NeckController` |
| VirtualBody可動域を変えたい | L4 | `BodyJointConstraint` |
| Servo Mappingを変えたい | L5 | `BodyJointServoBridge` / `ServoControlUnit` |
| PC / Android通信を変えたい | L6 | `BodyCommandCoordinator` / Platform Sender |
| Arduino / Servo側を変えたい | L7 | Firmware / PCA9685 / Servo |
| Recallを変えたい | Cross-layer | `ExperienceRecallResolver` / `JsonFileExperienceStore` |
| Safety / Permissionを変えたい | Cross-layer | `LimboPermissionResolver` / 各Authority Owner |

より実際の改造手順は [MODIFICATION_GUIDE.md](MODIFICATION_GUIDE.md) を参照してください。

---

## 15. この資料の読み方

Project Salieri AIを改造するときは、Sourceを単体で見るより、

```text
どのLayerにいるか
→ どこから入力されるか
→ 誰がAuthorityを持つか
→ どのSourceが変換するか
→ 次のLayer / Sourceへどこで渡すか
→ 最後にどのWriterが身体へ反映するか
```

という順で追うと構造を把握しやすくなります。

読む順番の目安：

1. [START_HERE.md](START_HERE.md) — L1-L7とRuntime全体像
2. `SOURCE_GUIDE.md` — SourceとLayerの対応
3. [MODIFICATION_GUIDE.md](MODIFICATION_GUIDE.md) — 実際にどこから改造するか

この3資料は、同じL1-L7の座標系で読めるようにしています。
