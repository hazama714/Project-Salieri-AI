# Project Salieri AI - Start Here

Project Salieri AIは、**AIキャラクターの知覚・会話・身体表現を、現実世界の身体へつなぐためのPhysical AI Runtime**です。

この資料は、Project Salieri AIを改造・検証してみたい人が、ソースコードを細部まで読む前に、

- 何がどこにあるのか
- どのような順番で処理されるのか
- どこを触ると何が変わるのか
- 現在どこまで実装されているのか

を大まかにつかむための「地図」です。

> この資料はPublic v0.1時点のProduction構成を基準にしています。Project Salieri AIは開発中のため、今後IK以外の身体制御方式や新しいBody構成などが追加される可能性があります。

---

## 1. Project Salieri AIのレイヤーモデル

Project Salieri AIでは、AI・キャラクター・ロボットを一つの処理として直接つなぐのではなく、責任を次の7レイヤーへ分けて考えます。

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

基本原則は、**上位ほど「意味や意図」を扱い、下位ほど「身体固有の事情」を扱う**ことです。

```text
Semantic Side
  認知・注意・会話・意図
        ↓
Embodiment Boundary
  Character Motion → VirtualBody
        ↓
Physical Side
  Retarget / Safety → Communication → Hardware
```

Perception、IK、Serial通信などは他のAI・ゲーム・ロボットプロジェクトにも存在する一般的な領域です。Project Salieri AIでは、それらをAIキャラクターから実身体まで連続したRuntimeとして接続し、VirtualBodyやAuthority / Safety境界を含めてProductionへ実装しています。

---

## 2. 現在のProductionは大きく3系統

Production SourceとMainScene上の有効Componentを追うと、主要経路は大きく次の3系統として見ることができます。

```text
[認知・会話]
Camera / STT / Text
  → L1 Perception
  → L2 Attention / Conversation / Intent

[仮想身体]
Attention / Body Intent / Free Pose
  → L3 Character Motion
  → L4 VirtualBody

[実身体]
VirtualBody / Commanded Joint State
  → L5 Physical Retarget / Safety
  → L6 Communication
  → L7 Hardware
```

すべての機能が完全に一つのManagerへ集約されているわけではなく、現在は首・腕・会話・記憶など、部位や責任ごとの既存経路が連携して動いています。

---

## 3. L1 Perception - 世界から情報を受け取る

現実世界から情報を受け取る入口です。

現在の主な要素は、Camera Input、Face Detection、Object Detection、Object Tracking、Speech / Text Inputです。

顔はOpenCV系の検出結果を安定化し、ObjectはYOLOXによるDetectionからByteTrackによるTrackingへ接続します。Objectについては、安定したTrackの中からObservation Targetを選びます。

```text
CameraInput
├─ Face Detection
│  → Face Perception
│  → Face Attention Candidate
│
└─ Object Detection
   → ByteTrack
   → Observation Target Selection
   → Object Attention Candidate
```

ここで得られた候補はL2のAttentionへ渡されます。

---

## 4. L2 Attention / Conversation / Intent - 何を見るか、何を話すか、何をするか

L2では、知覚した情報をもとに「何へ注意を向けるか」「何を話すか」「どの身体表現を選ぶか」といった意味側の判断を扱います。

### Attention

Face、Object、LOOK要求など複数の候補から、現在どこへ注意を向けるかを解決します。

```text
Face Candidate
Object Candidate
Explicit LOOK / Priority Request
        ↓
OrientationTargetResolver
        ↓
Attention Target
```

現在の中心Sourceは、`OrientationPriorityRequestService`、`OrientationTargetResolver`、`OrientationResolutionTargetDriver`です。

Attention Targetは首のLOOKだけでなく、`current_attention_target`を使ったPOINTなど、身体表現側からも利用されます。

### Conversation

音声またはText Inputは、Runtimeの入力Queueを経由して会話処理へ送られます。

```text
STT Final / Text Input
  ↓
UserSpeechRouter
  ↓
ExternalInputBuffer
  ↓
RuntimeProcessor
  ↓
ConversationReactionService
  ↓
ConversationService
  ↓
LLMRouteController
  ↓
Cloud / Local Provider
  ↓
Speech + Body Intent
```

Public v0.1時点のMainSceneではCloud Conversationが主な実運用経路で、Local Providerは代替経路としてSourceと参照を持っています。

会話結果は発話だけでなく、Body IntentやHand TargetなどのL3 Character Motionへも接続されます。

---

## 5. L3 Character Motion - キャラクターとして身体を動かす

L3では、AIやRuntimeが選んだ意図を、キャラクターとしての身体動作へ変換します。

現在は主に、VRM、Animator IK、LOOK、POINT、Free Pose、Semantic Body Targetなどを使用しています。

### 現在のFree Pose例

現在のFree Poseでは、Unity空間に用意されたSemantic Targetを選択し、そのTargetをもとに腕のIKへ接続します。

```text
Conversation / Body Intent
  ↓
Semantic Target Selection
  ↓
Free Pose
  ↓
Hand Target
  ↓
VRM Animator IK
```

固定Targetは左右別に管理され、`current_attention_target`のように現在のAttentionから動的に利用されるTargetもあります。

これは**現在の主要方式の一つ**であり、Project Salieri AI全体を将来にわたってIKだけへ限定するものではありません。用途に応じてAnimation、FK、別の制御方式を組み合わせることを想定しています。

### 腕の現在経路

```text
HandTargetAuthority
  ↓
VRM Hand Target
  ↓
VRMArmTargetIKController
  ↓
VRM solved Hand Bone
  ↓
RobotArmIKSolver
  ↓
BodyJointConstraint
  ↓
VirtualBody Arm
```

`HandTargetAuthority`は左右の手Targetについて単一Writerとなり、複数の処理が同時に同じTargetを書き換えないための境界として機能します。

---

## 6. L4 VirtualBody - キャラクターと実機の間にある身体

VirtualBodyは、キャラクター側の身体表現と実際のロボットの身体の間に置く中間表現です。

```text
Character / VRM
      ↓
  VirtualBody
      ↓
Physical Body
```

キャラクターの骨格構造と実機のServo構造を一対一で直接結びつけるのではなく、VirtualBodyを介することで、実機ごとの構造差や制約を分離しやすくします。

現在のProductionでは、全身を一つのCanonical Body Stateが所有しているわけではありません。

- Arms：VRM solved hand position → `RobotArmIKSolver` → `BodyJointConstraint`
- Neck：`NeckController`のCommanded state → `VBodyNeckPoseDriver` → `BodyJointConstraint`

というように、部位ごとのSourceからVirtualBodyへ反映しています。

---

## 7. LOOK / Neck - L2からL5をまたぐ実装例

首のLOOKは、複数レイヤーをまたぐ分かりやすい例です。

```text
L2  Attention Target
      ↓
L3  VRM Animator LookAt
      ↓
    VRM solved Neck / Head
      ↓
L5  VirtualNeckRetarget
      ↓
    PhysicalNeckOutputOwner
      ↓
    NeckController
      ├─ L4 VirtualBody Neck
      └─ Physical Neck Servo
```

`NeckController`はCommanded Poseを保持し、rate limitやHOLDを適用します。

現在の首制御では、Targetを見失った場合も即座に別姿勢へ切り替えるのではなく、Commanded stateを基準にVirtualBodyとPhysical側を扱います。

このように、レイヤーは「必ず一方向に一度だけ通る箱」ではなく、責任を区別するための設計上の地図です。

---

## 8. L5 Physical Retarget / Safety - VirtualBodyを実機へ合わせる

VirtualBodyのJoint角度やCommanded Stateを、実際の機体で使用できる形へ変換します。

腕では主に次の経路です。

```text
BodyJointConstraint.AppliedAngle
  ↓
BodyJointServoBridge
  ↓
ServoControlUnit
  ↓
BodyCommandCoordinator
```

この層では、機体ごとのServo使用可否、回転方向、Offset、Min / Max、補間、Output Enableなどを扱います。

また、Project Salieri AIでは「誰が最後に書けるか」を明確にするため、AuthorityやOwnerを各所に置いています。

代表例：

- Attention resolution：`OrientationTargetResolver`
- Hand Target：`HandTargetAuthority`
- Physical Neck：`PhysicalNeckOutputOwner`
- Physical Arm Enable：`ArmServoOutputController`
- Transport dispatch：`BodyCommandCoordinator`

Interaction StateやPermissionによって、Orientation、Servo、Voice、Thinking、Searchなどの実行可否も切り替えます。Emergency / Stop系の入力は通常Conversationより先に扱われます。

Public v0.1時点では、腕のPhysical Outputは起動時に自動でArmされない構成です。Safety条件とStartup synchronizationを通過してから出力します。

---

## 9. L6 Communication - 実機へCommandを送る

最終的なServo Commandは`BodyCommandCoordinator`に集約され、Platformや設定に応じてTransportを選びます。

```text
BodyCommandCoordinator
├─ Windows
│  → PC Serial
│
└─ Android
   ├─ USB Serial / USB-OTG
   └─ Bluetooth
```

Public v0.1時点のMainSceneでは、WindowsはSerial、AndroidはUSB Serialが主設定です。Bluetooth経路も保持されています。

---

## 10. L7 Hardware - Arduino / Servo / Physical Body

L7は、Runtimeから届いたCommandを実際の身体へ反映するHardware側です。

```text
Communication
  ↓
Arduino
  ↓
PCA9685
  ↓
Servo
  ↓
Physical Body
```

Public v0.1では、Arduino + PCA9685を使ったServo制御FirmwareをRepositoryに収録しています。

このレイヤーは、今後別のマイコン、Servo Driver、Motor Driver、Body構成へ置き換える余地を持つPhysical側の終端です。

---

## 11. Memory / Recall - レイヤーを横断する経験基盤

Memory / Recallは身体レイヤーとは別に、Perception、Attention、Conversation、Behaviorを横断するRuntime基盤です。

現在のObject Experience系は概ね次の流れです。

```text
Observation Target
  ↓
Recall Key
  ↓
Experience Recall
  ↓
Known / Unknown

Unknown
  ↓
LOOK → POINT → ASK
  ↓
User Answer
  ↓
Answer Binding
  ↓
Experience Save
  ↓
JSON Store
```

現在のProduction Experience Save / RecallはJSON Storeを使用しています。

SQLiteによるWorld Memory subsystemもSourceとして存在しますが、Public v0.1時点では通常Production経路へ完全接続された状態ではありません。

Visual Recallは現在exact matchingを中心とした実験段階です。

---

## 12. MotionとFunctional Motion

Project Salieri AIでは、見た目として身体を動かすことと、現実世界で目的を達成するための身体動作を分けて考えています。

```text
Animationで手を振る
  → Motion / Expression

現実の対象物へ手を伸ばす
  → Functional Motion
```

Functional Motionでは、対象位置、現在姿勢、身体制約、安全性、必要に応じてSensor Feedbackなど、より多くの現実世界の情報が必要になります。

現在のProject Salieri AIは、Motion / ExpressionからFunctional Motionへ段階的に接続できるRuntimeを目指して開発しています。

---

## 13. 何を改造したい？

最初に見るレイヤーの目安です。

| やりたいこと | 最初に見るレイヤー / 領域 |
|---|---|
| 顔や物体認識を変えたい | L1 Perception |
| 何を見るかを変えたい | L2 Attention / Orientation |
| 会話やAIの反応を変えたい | L2 Conversation / LLM / Reaction |
| ポーズやジェスチャーを増やしたい | L2 Body Intent → L3 Free Pose / Spatial Target |
| VRMの腕や首の動きを変えたい | L3 Character Motion / Animator IK / IK Solver |
| ロボットの腕・首構造を変えたい | L4 VirtualBody / BodyJointConstraint |
| Servoの方向や可動域を変えたい | L5 Physical Retarget / Servo Bridge |
| Safetyや動作許可を追いたい | L5 Permission / Authority / Output Controller |
| PC / Android通信を変えたい | L6 Communication |
| ArduinoやServo Driverを変えたい | L7 Hardware / Firmware |
| 記憶を実験したい | Memory / Recall / World Memory |

具体的な変更箇所は [MODIFICATION_GUIDE.md](MODIFICATION_GUIDE.md) を参照してください。

---

## 14. 現在どこまでつながっているか

Public v0.1では、少なくとも次の経路がProduction Sourceとして存在し、MainScene上で接続されています。

```text
Camera / Speech / Text
        ↓
L1 Perception
        ↓
L2 Attention / Conversation / Intent
        ↓
L3 Character Motion
        ↓
L4 VirtualBody
        ↓
L5 Physical Retarget / Safety
        ↓
L6 Communication
        ↓
L7 Hardware
```

一方で、すべての機能が完成しているわけではありません。

例えば、

- World Memory SQLite backendは通常Productionへ未接続
- Visual Recallはexact matching中心の実験段階
- Physical Position Reachedを確認するEncoder / ACKは未実装
- 全身VirtualBodyを単一Canonical Stateへ完全統合した状態ではない
- 一部のLegacy / Debug名を持つComponentが、現在もProduction composition上で利用されている

といった開発途中の部分があります。

Project Salieri AIは完成品の内部を隠すのではなく、こうした現在地点も含めて、改造・検証できるDIY Physical AIの開発土壌として公開しています。

---

## 15. 次に読むもの

Project Salieri AIを理解してから実際にSourceへ入る場合は、次の順番を推奨します。

1. **この `START_HERE.md`** - レイヤーと全体像
2. [SOURCE_GUIDE.md](SOURCE_GUIDE.md) - 主要Source、Input / Output、Authority、実行Flow
3. [MODIFICATION_GUIDE.md](MODIFICATION_GUIDE.md) - 目的別に最初に変更する場所
4. [DEPENDENCIES.md](DEPENDENCIES.md) - 外部依存関係
5. [Firmware README](Firmware/Arduino/BODYLOBO/Salieri_BODYLOBO_Unified_PCA9685_10Servo_115200/README.md) - Arduino / Servo側

Source Versionの扱いは [SOURCE_VERSIONING.md](SOURCE_VERSIONING.md)、第三者Componentの権利情報は [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) を参照してください。
