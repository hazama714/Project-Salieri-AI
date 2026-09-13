# Project Salieri AI - Source Guide

この資料は、Project Salieri AI Public v0.1 の主要Sourceについて、**何を担当し、何を受け取り、どこへ渡すのか**を機能単位で確認するためのガイドです。

全502 Sourceの完全一覧ではありません。まず全体像をつかみたい場合は [START_HERE.md](START_HERE.md) を参照してください。

本資料は、Production SourceとMainSceneの有効Component・参照を照合した監査結果を基準にしています。Project Salieri AIは開発中のため、今後Source名、経路、Body構成、制御方式は変更される可能性があります。

---

## 1. Runtime全体の主要フロー

```text
Camera / Microphone / Text / Runtime Request
                    │
        ┌───────────┴───────────┐
        │                       │
    Perception              Conversation
        │                       │
Face / Object Candidate     Reaction / Activation
        │                       │
Orientation Resolver        ConversationService
        │                       │
Attention Target       Cloud / Local LLM Response
        │                 ┌─────┴─────┐
        │                 │           │
        │              Speech     Body Intent
        │                 │           │
        │          VoicePlayback   Free Pose / POINT
        │                             │
        └──────────────┬──────────────┘
                       │
                VRM Animator IK
                 ┌─────┴─────┐
                 │           │
             Neck/Head    Solved Hands
                 │           │
          Neck Retarget  RobotArmIKSolver
                 │           │
          NeckController  BodyJointConstraint
                 │           │
          VBody Neck      VBody Arms
                 │           │
          Neck Servo   BodyJointServoBridge
                 └─────┬─────┘
                       │
             BodyCommandCoordinator
             ┌─────────┴──────────┐
             │                    │
       Windows Serial       Android Transport
             │              USB / Bluetooth
             └─────────┬──────────┘
                       │
                Arduino / Servo
```

---

## 2. Perception / Attention

### 役割

CameraからFace / Objectを観測し、複数の候補から現在のAttention Targetを解決します。

### 主なフロー

```text
CameraInput
├─ FaceDetector_OpenCV
│  → FacePerceptionBuffer
│  → FaceAttentionTargetDriver
│  → FaceOrientationTargetSource
│
└─ YoloXObjectDetectionService
   → ByteTrackObjectTrackingService
   → ObservationTargetSelectionService
   → ObjectOrientationTargetSource

Face / Object / Priority Request
→ OrientationTargetResolver
→ OrientationResolutionTargetDriver
→ AttentionTarget
```

### 主要Source

| Source | 主な役割 | Input | Output / 次 |
|---|---|---|---|
| [`CameraInput.cs`](Assets/Sensors/Camera/Common/CameraInput.cs) | Camera開始と現在Frame公開 | WebCamTexture | CurrentTexture |
| [`FaceDetector_OpenCV.cs`](Assets/Sensors/Camera/OpenCV/FaceDetector_OpenCV.cs) | 顔検出 | Camera Texture | Face位置・サイズ |
| [`FacePerceptionBuffer.cs`](Assets/Core/Perception/Face/FacePerceptionBuffer.cs) | 顔検出状態の安定化 | Raw Face State | Stable / TemporaryLost / FullyLost |
| [`FaceAttentionTargetDriver.cs`](Assets/Core/Perception/Attention/FaceAttentionTargetDriver.cs) | Face位置をWorld Targetへ変換 | Face Center | Face Attention Target |
| [`FaceOrientationTargetSource.cs`](Assets/Core/Perception/Attention/FaceOrientationTargetSource.cs) | FaceをOrientation候補へ変換 | Face Target | Candidate |
| [`YoloXObjectDetectionService.cs`](Assets/Core/Perception/Object/YoloXObjectDetectionService.cs) | Object Detection | Camera Frame | Detections |
| [`ByteTrackObjectTrackingService.cs`](Assets/Core/Perception/Object/Tracking/ByteTrackObjectTrackingService.cs) | DetectionをTrackへ接続 | Detections | TrackedObjectSet |
| [`ObservationTargetSelectionService.cs`](Assets/Core/Perception/Object/Targeting/ObservationTargetSelectionService.cs) | Observation Target選択 | Tracks | CurrentTarget |
| [`ObjectOrientationTargetSource.cs`](Assets/Core/Perception/Attention/ObjectOrientationTargetSource.cs) | ObjectをOrientation候補へ変換 | Selected Target | Candidate |
| [`OrientationPriorityRequestService.cs`](Assets/Core/Perception/Attention/OrientationPriorityRequestService.cs) | LOOK等の明示要求を優先度・期限付きで保持 | Orientation Request | Active Request |
| `OrientationTargetResolver` | Face / Object / Priority等を単一解決 | Candidates | Resolution |
| `OrientationResolutionTargetDriver` | 解決結果を共有Attention Transformへ反映 | Resolution | AttentionTarget |

### Authority

- Orientation解決：`OrientationTargetResolver`
- 明示要求：`OrientationPriorityRequestService`
- Object Target：`ObservationTargetSelectionService.CurrentTarget`
- 最終Attention Transform：`OrientationResolutionTargetDriver`

---

## 3. Conversation

### 役割

STTやText InputをRuntimeへ取り込み、会話判定、LLM Routing、発話、Body Intentへ接続します。

### 主なフロー

```text
STT Final / Text Input
→ AndroidSTTReceiver / Text Input
→ UserSpeechRouter
→ UserSpeechInputBuffer
→ ExternalInputBuffer
→ AutonomousClock
→ RuntimeProcessor
→ CognitiveReflexController
→ ConversationReactionService
→ ConversationService
→ LLMRouteController
→ Cloud / Local Provider
→ ResponseBus / Body Adapter
```

### 主要Source

| Source | 主な役割 | Input | Output / 次 |
|---|---|---|---|
| [`AndroidSTTReceiver.cs`](Assets/Sensors/Audio/AndroidSTTReceiver.cs) | Platform STT Finalの入口 | Transcript | UserSpeechRouter |
| [`UserSpeechRouter.cs`](Assets/Sensors/Audio/UserSpeechRouter.cs) | InputId等を付与してSpeechをRuntime形式へ変換 | Text | CommunicationInput |
| [`UserSpeechInputBuffer.cs`](Assets/Core/Perception/Speech/UserSpeechInputBuffer.cs) | Speech状態保持とExternal Input化 | CommunicationInput | External Event |
| `ExternalInputBuffer` | 優先度付き入力Queue | Input | Queued Event |
| `AutonomousClock` | QueueからRuntimeへ入力供給 | Queued Event | RuntimeProcessor |
| `RuntimeProcessor` | InputType別のRuntime dispatch | External Input | Cognitive Reflex |
| `CognitiveReflexController` | UserSpeechをConversationへ接続 | Speech | ConversationReactionService |
| [`ConversationReactionService.cs`](Assets/Core/Reflex/Cognitive/ConversationReactionService.cs) | Safety、Activation、Pending Question、通常Conversationの振り分け | Speech | Conversation / Action / Answer |
| [`ConversationService.cs`](Assets/Core/Reflex/Cognitive/ConversationService.cs) | 1 TurnのGrounding・Generation・Body/Speech出力 | Conversation Request | LLM Response |
| [`LLMRouteController.cs`](Assets/Core/LLM/Common/LLMRouteController.cs) | Cloud / Local選択とFallback | Generation Request | Provider |
| `CloudConversationProvider` | Cloud Client Adapter | Request | Structured Response |
| `LocalConversationProvider` | Local Client Adapter | Request | Response |
| `ResponseBus` | 応答Envelope配信 | Response | Playback |
| `VoicePlaybackController` | TTSとSpeaking lifecycle | Text | Audio / Speech Event |

### Authority

- Speech routing：`ConversationReactionService`
- Generation：`ConversationService`
- Provider選択：`LLMRouteController`
- Speech Playback：`VoicePlaybackController`

Public v0.1のMainSceneではCloud Conversationが主な実運用経路で、Local Providerは代替経路として保持されています。

---

## 4. Free Pose / Arm

### 役割

会話やBody Intentから選ばれたSemantic Targetを、実際のHand Targetへ変換し、VRM IKへ接続します。

### 主なフロー

```text
Cloud Conversation Response
├─ Explicit Hand Target
│  → ConversationalHandTargetProductionAdapter
│
└─ Body Expression Intent
   → BodyExpressionIntentProductionAdapter

→ ProductionPoseRequestRuntime
→ ProductionPoseRequestService
→ FreePoseExecutor
→ FreePoseArmExecutor
→ SpatialTargetRegistry
→ HandTargetAuthority
→ VRM Hand Target
```

### 主要Source

| Source | 主な役割 | Input | Output / 次 |
|---|---|---|---|
| [`ConversationalHandTargetProductionAdapter.cs`](Assets/Core/Reflex/Cognitive/ConversationalHandTargetProductionAdapter.cs) | LLMの明示Hand Targetを検証してProduction Poseへ渡す | Structured Response | Target Request |
| [`BodyExpressionIntentProductionAdapter.cs`](Assets/Core/Reflex/Cognitive/BodyExpressionIntentProductionAdapter.cs) | Body Intentを既存PoseへGrounding | Semantic Intent | Pose Request |
| [`SemanticBodyTargetCatalogV0.cs`](Assets/Body/FreePose/Production/SemanticBodyTargetCatalogV0.cs) | 利用可能Target IDのCatalog | Target ID | Validated Target |
| [`ProductionPoseRequestService.cs`](Assets/Body/FreePose/Production/ProductionPoseRequestService.cs) | Production RequestをPoseDefinitionへ変換 | Request | FreePoseExecutor |
| [`FreePoseExecutor.cs`](Assets/Body/FreePose/FreePoseExecutor.cs) | 両腕・Headを1 Poseとして開始・置換・Release | PoseDefinition | Arm / Head Executor |
| [`FreePoseArmExecutor.cs`](Assets/Body/FreePose/FreePoseArmExecutor.cs) | Target lookup、Lease、補間Submit | Semantic Target | Hand Position |
| [`SpatialTargetRegistry.cs`](Assets/Body/SpatialTarget/SpatialTargetRegistry.cs) | 固定Spatial TargetのSource of Truth | ID | Transform |
| [`HandTargetAuthority.cs`](Assets/Body/SpatialTarget/HandTargetAuthority.cs) | 左右別LeaseとHand Target単一Writer | Lease + Position | VRM Hand Target |
| [`FreePoseHeadAdapter.cs`](Assets/Body/FreePose/FreePoseHeadAdapter.cs) | Head IntentをOrientation Requestへ変換 | Head Directive | Orientation Request |

### 現在の考え方

現在のFree Poseでは、Unity空間に用意されたTargetを選択し、そのTargetへ向けて腕のIK経路を動かします。

固定Targetは左右別に管理され、`current_attention_target`は現在のAttentionを利用する動的Targetです。

この方式は現在の主要実装であり、Project Salieri AI全体を将来にわたってIKだけへ限定するものではありません。

### Authority

- Target ID：`SemanticBodyTargetCatalogV0`
- Target Transform：`SpatialTargetRegistry`
- Hand Target Writer：`HandTargetAuthority`

---

## 5. VRM / IK

### 役割

World上のHand TargetからVRMのHumanoid IKを解き、その結果をVirtualBodyの腕へ写します。

### 主なフロー

```text
HandTargetAuthority
→ VRM Hand Target
→ VRMArmTargetIKController.OnAnimatorIK
→ VRM solved Hand Bone
→ RobotArmIKSolver.LateUpdate
→ BodyJointConstraint
→ VirtualBody Arm
```

### 主要Source

| Source | 主な役割 | Input | Output / 次 |
|---|---|---|---|
| [`VRMArmTargetIKController.cs`](Assets/Expression/Motion/IK/VRMArmTargetIKController.cs) | Hand TargetをAnimator IKへ設定 | Hand Target | VRM solved Hand |
| `UpperBodyAnimatorIKCoordinator` | Upper Body / LookAtをAnimator IK passで調整 | Targets | VRM Chest / Head |
| `VrmHeadLookAtVisual` | VRM LookAt設定 | AttentionTarget | Solved Head |
| [`RobotArmIKSolver.cs`](Assets/Body/IK/RobotArmIKSolver.cs) | VRM solved HandへVirtualBody 4軸を近づける | Solved Hand | Joint Angles |
| [`BodyJointConstraint.cs`](Assets/Body/Joint/BodyJointConstraint.cs) | Joint角度をClampしてTransformへ適用 | Target Angle | VirtualBody Joint |

### Authority

- VRM Hand IK Input：`VRMArmTargetIKController`
- VirtualBody Arm Solver：左右`RobotArmIKSolver`
- Joint Transform Writer：各`BodyJointConstraint`

---

## 6. LOOK / Neck

### 役割

Attention TargetをVRMのLookAtへ反映し、解決された首・頭方向をPhysical NeckへRetargetします。

### 主なフロー

```text
OrientationResolutionTargetDriver
→ AttentionTarget
→ UpperBodyAnimatorIKCoordinator.OnAnimatorIK
→ VrmHeadLookAtVisual
→ VRM solved Neck / Head
→ VRMNeckSolvedPoseReader
→ VirtualNeckRetargetShadow
→ VirtualNeckPhysicalOutputBridge
→ PhysicalNeckOutputOwner
→ FaceTrackingOutputController
→ NeckController
├─ VBodyNeckPoseDriver
├─ NeckPoseFrameDriver
└─ ServoControlUnit → BodyCommandCoordinator
```

### 主要Source

| Source | 主な役割 | Input | Output / 次 |
|---|---|---|---|
| `OrientationResolutionTargetDriver` | Attention Targetの最終Writer | Resolution | AttentionTarget |
| `UpperBodyAnimatorIKCoordinator` | Animator IK callback所有 | Attention Target | VRM LookAt |
| `VrmHeadLookAtVisual` | LookAt weight / position設定 | Attention Target | Solved Head |
| `VRMNeckSolvedPoseReader` | 解決後のVRM Neck / Headを読む | VRM Bones | Solved Pose |
| `VirtualNeckRetargetShadow` | Solved方向をPhysical yaw/pitch候補へ変換 | Solved Pose | Retarget State |
| `VirtualNeckPhysicalOutputBridge` | Retarget StateをPhysical Ownerへ提出 | Retarget State | Physical Owner |
| `PhysicalNeckOutputOwner` | Physical Neck出力の単一受付 | State | Guarded Output |
| `FaceTrackingOutputController` | Mode / Permission / Emergency確認 | Retarget | Desired Pose |
| [`NeckController.cs`](Assets/Body/Parts/Controller/NeckController.cs) | Rate Limit / HOLD / Commanded Pose保持 | Desired Pose | Commanded Pose / Servo |
| `VBodyNeckPoseDriver` | Commanded PoseをVirtualBody Neckへ反映 | Commanded yaw/pitch | Neck Joint |

### Authority

- LOOK Target：`OrientationTargetResolver`
- Physical Neck受付：`PhysicalNeckOutputOwner`
- Commanded Neck State：`NeckController`
- VirtualBody Neck Writer：`BodyJointConstraint`

---

## 7. VirtualBody

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

## 8. Physical Retarget / Physical Output

### 役割

VirtualBodyのJoint角度を、実機ごとのServo設定へ変換し、Physical Outputへ渡します。

### Arm Flow

```text
BodyJointConstraint.AppliedAngle
→ BodyJointServoBridge
→ ServoControlUnit
→ BodyCommandCoordinator
→ Platform Sender
→ Arduino / Servo
```

### Neck Flow

```text
NeckController.CommandedYaw/Pitch
→ Neck ServoControlUnit
→ BodyCommandCoordinator
→ Platform Sender
```

### 主要Source

| Source | 主な役割 | Input | Output / 次 |
|---|---|---|---|
| [`BodyJointServoBridge.cs`](Assets/Body/Servo/Bridge/BodyJointServoBridge.cs) | VirtualBody角をServo入力へRetarget | AppliedAngle | Servo Angle |
| [`ServoControlUnit.cs`](Assets/Body/Servo/Units/ServoControlUnit.cs) | Invert / Offset / Min / Max / Enableを適用 | Input Angle | Sender |
| `ArmServoOutputController` | 腕8軸のArm / DisarmとSafety Gate | Safety / Connection | Bridge Enable |
| [`NeckController.cs`](Assets/Body/Parts/Controller/NeckController.cs) | Neck Commanded Poseと送信管理 | Desired Pose | Neck Servo Command |
| [`BodyCommandCoordinator.cs`](Assets/Body/Command/BodyCommandCoordinator.cs) | Servo Command QueueとTransport選択 | Servo / Raw Command | Selected Sender |

### Safety境界

腕Physical Outputは起動時に自動Armされず、Safety条件とStartup synchronizationを通過してから出力されます。

また、現在のPhysical契約では、Command送信完了と実際のServo到達は同義ではありません。

---

## 9. Communication

### 役割

`BodyCommandCoordinator`から実機Transportを選び、ArduinoへCommandを送ります。

### 主なフロー

```text
BodyCommandCoordinator
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
```

### 主要Source

| Source | 主な役割 | Input | Output |
|---|---|---|---|
| [`BodyCommandCoordinator.cs`](Assets/Body/Command/BodyCommandCoordinator.cs) | Platform / 設定に応じたSender選択 | Body Command | Selected Transport |
| `SerialSender_PC_Body` | Windows Serial出力 | Command | Serial Port |
| [`AndroidUsbSerialSender.cs`](Assets/App/Communication/UsbSerial/AndroidUsbSerialSender.cs) | Android USB-OTG出力 | Command | USB Serial |
| [`AndroidBluetoothSender.cs`](Assets/App/Communication/Bluetooth/AndroidBluetoothSender.cs) | Android Bluetooth出力 | Command | Bluetooth |
| [`RuntimeConnectionSettings.cs`](Assets/App/Settings/RuntimeConnectionSettings.cs) | Platform / Android Transport設定 | Serialized Settings | Transport Selection |

Public v0.1 MainSceneではWindowsはPC Serial、AndroidはUSB Serialが主設定です。Bluetoothは代替Transportとして保持されています。

---

## 10. Memory / Recall

### 役割

Observation Targetに対するExperienceのRecall、質問、回答Binding、保存を扱います。

### 主なフロー

```text
ObservationTarget
→ Visual Snapshot / RecallKey
→ ExperienceRecallResolver
→ Known / Unknown

Unknown
→ LOOK
→ POINT
→ ASK
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

## 11. Safety / Authority

Project Salieri AIでは、「どの処理が最後に身体へ書き込めるか」を機能ごとに分けています。

| 責任 | Current Owner |
|---|---|
| Interaction State | `InteractionStateController` |
| State別Permission | `LimboPermissionResolver` |
| Camera / LLM / Voice / Servo利用可否 | `ExecutionResourceManager` |
| Fixed Action実行 | `ExecutionController` |
| Emergency Speech Admission | `ConversationReactionService` |
| Head Request Arbitration | `OrientationPriorityRequestService` |
| Physical Neck Writer境界 | `PhysicalNeckOutputOwner` |
| Right / Left Hand Writer | `HandTargetAuthority` |
| Physical Arm Enable | `ArmServoOutputController` |
| Transport Dispatch | `BodyCommandCoordinator` |

Emergency / Stop系は通常Conversationより先に扱われます。

すべての身体経路が一つのExecution Orchestrationを必ず通る構造ではなく、Free PoseやLOOKなどはそれぞれ専用Authorityを持っています。

---

## 12. どこを触ればいいか

| やりたいこと | 最初に見るSource / 領域 |
|---|---|
| 顔認識を変えたい | `FaceDetector_OpenCV` / `FacePerceptionBuffer` |
| Object認識・追跡を変えたい | `YoloXObjectDetectionService` / `ByteTrackObjectTrackingService` |
| 見る対象を変えたい | `OrientationTargetResolver` / `OrientationPriorityRequestService` |
| 会話入力を変えたい | `UserSpeechRouter` / `ConversationReactionService` |
| LLM Routingを変えたい | `LLMRouteController` |
| ポーズTargetを増やしたい | `SemanticBodyTargetCatalogV0` / `SpatialTargetRegistry` |
| Hand Target制御を変えたい | `HandTargetAuthority` / `FreePoseArmExecutor` |
| 腕IKを変えたい | `VRMArmTargetIKController` / `RobotArmIKSolver` |
| 首LOOKを変えたい | `OrientationTargetResolver` / `NeckController` |
| VirtualBody可動域を変えたい | `BodyJointConstraint` |
| Servo Mappingを変えたい | `BodyJointServoBridge` / `ServoControlUnit` |
| PC / Android通信を変えたい | `BodyCommandCoordinator` / Platform Sender |
| Recallを変えたい | `ExperienceRecallResolver` / `JsonFileExperienceStore` |
| Safety / Permissionを変えたい | `LimboPermissionResolver` / 各Authority Owner |

---

## 13. この資料の読み方

Project Salieri AIを改造するときは、Sourceを単体で見るより、

```text
どこから入力されるか
→ 誰がAuthorityを持つか
→ どのSourceが変換するか
→ 次にどこへ渡すか
→ 最後にどのWriterが身体へ反映するか
```

という順で追うと構造を把握しやすくなります。

より大きな設計思想とLayer構成は [START_HERE.md](START_HERE.md) を参照してください。
