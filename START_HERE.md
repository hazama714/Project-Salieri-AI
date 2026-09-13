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

## 1. まず全体像

Project Salieri AIでは、AI・キャラクター・ロボットを一つの処理として直接つなぐのではなく、役割ごとに段階を分けています。

```text
Perception
  ↓
Attention / Conversation / Intent
  ↓
Character Motion
  ↓
VirtualBody
  ↓
Physical Retarget / Safety
  ↓
Communication
  ↓
Hardware
```

上位では「誰を見るか」「何を話すか」「どの動作を選ぶか」といった意味を扱い、下位へ進むほど、身体構造、可動域、サーボ、通信方式など実機固有の事情を扱います。

このレイヤー分離によって、AI側、キャラクター側、ロボット側をそれぞれ改良しやすい構造を目指しています。

---

## 2. 現在のProductionは大きく3系統

現在のProduction SourceとMainScene上の有効Componentを追うと、主要経路は大きく次の3系統として見ることができます。

```text
[認知・会話]
Camera / STT / Text
  → Perception
  → Attention / Runtime
  → Conversation / LLM

[仮想身体]
Attention / Body Intent / Free Pose
  → VRM / Animator IK
  → VirtualBody

[実身体]
VirtualBody / Commanded Joint State
  → Physical Retarget / Safety
  → BodyCommandCoordinator
  → USB Serial / Bluetooth / PC Serial
  → Arduino / Servo
```

すべての機能が完全に一つのManagerへ集約されているわけではなく、現在は首・腕・会話・記憶など、部位や責任ごとの既存経路が連携して動いています。

---

## 3. Perception - 世界を見る

現実世界から情報を受け取る入口です。

現在の主な要素には、次のものがあります。

- Camera Input
- Face Detection
- Object Detection
- Object Tracking
- Speech / Text Input

顔はOpenCV系の検出結果を安定化し、ObjectはYOLOXによるDetectionからByteTrackによるTrackingへ接続します。

Objectについては、安定したTrackの中からObservation Targetを選びます。

概略は次のようになります。

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

ここで得られた候補は、次のAttentionレイヤーへ渡されます。

---

## 4. Attention - 何を見るかを決める

Face、Object、LOOK要求など複数の候補から、現在どこへ注意を向けるかを解決するレイヤーです。

現在の中心は次のSourceです。

- `OrientationPriorityRequestService`
- `OrientationTargetResolver`
- `OrientationResolutionTargetDriver`

概略は次の通りです。

```text
Face Candidate
Object Candidate
Explicit LOOK / Priority Request
        ↓
OrientationTargetResolver
        ↓
Attention Target
```

このAttention Targetは首のLOOKだけでなく、`current_attention_target`を使ったPOINTなど、身体表現側からも利用されます。

---

## 5. Conversation - 聞いて、考えて、返す

音声またはText Inputは、Runtimeの入力Queueを経由して会話処理へ送られます。

現在の主要フローは次の通りです。

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

会話結果は発話だけでなく、Body IntentやHand Targetなどの身体表現へも接続されます。

短期会話Contextは、完了したConversation Turnを限定数保持して次の会話生成へ渡します。

---

## 6. Character Motion - キャラクターとして身体を動かす

このレイヤーでは、AIやRuntimeが選んだ意図を、キャラクターの身体動作へ変換します。

現在は主に、

- VRM
- Animator IK
- LOOK
- POINT
- Free Pose
- Semantic Body Target

などを使っています。

### 現在のFree Poseの考え方

Project Salieri AIの現在のFree Poseでは、Unity空間にあらかじめ配置されたTargetを選択し、そのTargetをもとに腕のIKへ接続します。

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

固定Targetは左右別に管理され、`current_attention_target`のように現在のAttentionから動的に生成されるTargetもあります。

これは現在の主要方式であり、Project Salieri AI全体を将来にわたってIKだけへ限定するものではありません。用途に応じてAnimation、FK、別の制御方式を組み合わせられる構造を目指しています。

---

## 7. VRM / IK - キャラクターの姿勢を解く

腕では、World上のHand TargetをVRM Humanoid Animator IKへ渡し、解決されたVRMの手位置をVirtualBody側のArm Solverが追跡します。

現在の腕経路は概ね次の通りです。

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

## 8. VirtualBody - キャラクターと実機の間にある身体

VirtualBodyは、キャラクター側の身体表現と実際のロボットの身体の間に置く中間表現です。

```text
Character / VRM
      ↓
  VirtualBody
      ↓
Physical Body
```

キャラクターの骨格構造と実機のサーボ構造を一対一で直接結びつけるのではなく、VirtualBodyを介することで、実機ごとの構造差や制約を分離しやすくします。

現在のProductionでは、全身を一つのCanonical Body Stateが所有しているわけではありません。

- Arms：VRM solved hand position → `RobotArmIKSolver` → `BodyJointConstraint`
- Neck：`NeckController`のCommanded state → `VBodyNeckPoseDriver` → `BodyJointConstraint`

というように、部位ごとのSourceからVirtualBodyへ反映しています。

---

## 9. Neck / LOOK - 視線から物理首まで

首は、Attention TargetをVRM Animatorで解き、その解決後の姿勢をPhysical NeckへRetargetする経路を持っています。

```text
Attention Target
  ↓
VRM Animator LookAt
  ↓
VRM solved Neck / Head
  ↓
VirtualNeckRetarget
  ↓
PhysicalNeckOutputOwner
  ↓
NeckController
  ├─ VirtualBody Neck
  └─ Physical Neck Servo
```

`NeckController`はCommanded Poseを保持し、rate limitやHOLDを適用します。

現在の首制御では、Targetを見失った場合も即座に別姿勢へ切り替えるのではなく、Commanded stateを基準にVirtualBodyとPhysical側を扱います。

---

## 10. Physical Retarget - VirtualBodyを実機へ合わせる

VirtualBodyのJoint角度を、そのまま機体へ送るのではなく、実際のServo構成に合わせて変換します。

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

この層では、機体ごとの

- Servo使用可否
- 回転方向
- Offset
- Min / Max
- 補間
- Output Enable

などを扱います。

これにより、VirtualBody側の表現と実機固有のServo設定を分離します。

Public v0.1時点では、腕のPhysical Outputは起動時に自動でArmされない構成です。Safety条件とStartup synchronizationを通過してから出力します。

---

## 11. Communication - 実機へCommandを送る

最終的なServo Commandは`BodyCommandCoordinator`に集約され、Platformや設定に応じてTransportを選びます。

```text
BodyCommandCoordinator
├─ Windows
│  → PC Serial
│  → Arduino
│
└─ Android
   ├─ USB Serial / USB-OTG
   └─ Bluetooth
```

Public v0.1時点のMainSceneでは、WindowsはSerial、AndroidはUSB Serialが主設定です。Bluetooth経路も保持されています。

Arduino側ではPCA9685を使用したServo制御Firmwareを公開しています。

---

## 12. Memory / Recall - 経験を残す

Project Salieri AIには、会話とは別にExperience / Recallの実験基盤があります。

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

また、Visual Recallは現在exact matchingを中心とした実験段階です。

---

## 13. Safety / Authority - 誰が最後に書くか

Project Salieri AIでは、複数の機能が同じ身体部位を同時に操作しないよう、各所にAuthorityやOwnerを置いています。

代表例：

- Attention resolution：`OrientationTargetResolver`
- Hand Target：`HandTargetAuthority`
- Physical Neck：`PhysicalNeckOutputOwner`
- Physical Arm Enable：`ArmServoOutputController`
- Transport dispatch：`BodyCommandCoordinator`

また、Interaction StateやPermissionによって、Orientation、Servo、Voice、Thinking、Searchなどの実行可否を切り替えます。

Emergency / Stop系の入力は通常Conversationより先に扱われます。

---

## 14. MotionとFunctional Motion

Project Salieri AIでは、見た目として身体を動かすことと、現実世界で目的を達成するための身体動作を分けて考えています。

例えば、

```text
Animationで手を振る
  → Motion / Expression

現実の対象物へ手を伸ばす
  → Functional Motion
```

Functional Motionでは、対象位置、現在姿勢、身体制約、安全性、必要に応じてSensor Feedbackなど、より多くの現実世界の情報が必要になります。

現在のProject Salieri AIは、Motion / ExpressionからFunctional Motionへ段階的に接続できるRuntimeを目指して開発しています。

---

## 15. 何を改造したい？

最初に見る場所の目安です。

| やりたいこと | 主に見る領域 |
|---|---|
| 会話やAIの反応を変えたい | Conversation / LLM / Reaction |
| 顔や物体認識を変えたい | Perception |
| 何を見るかを変えたい | Attention / Orientation |
| ポーズやジェスチャーを増やしたい | Body Intent / Free Pose / Spatial Target |
| VRMの腕や首の動きを変えたい | Animator IK / IK Solver |
| ロボットの腕・首構造を変えたい | VirtualBody / BodyJointConstraint |
| Servoの方向や可動域を変えたい | Physical Retarget / Servo Bridge |
| Arduinoや通信方式を変えたい | Communication / Firmware |
| 記憶を実験したい | Experience / Recall / World Memory |
| Safetyや動作許可を追いたい | Permission / Authority / Output Controller |

---

## 16. 現在どこまでつながっているか

Public v0.1では、少なくとも次の経路がProduction Sourceとして存在し、MainScene上で接続されています。

```text
Camera / Speech / Text
        ↓
Perception / Runtime
        ↓
Attention / Conversation
        ↓
Body Intent
        ↓
VRM / Animator IK
        ↓
VirtualBody
        ↓
Physical Retarget / Safety
        ↓
BodyCommandCoordinator
        ↓
PC Serial / Android USB Serial / Bluetooth
        ↓
Arduino / Servo
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

## 17. 次に読むもの

- [README](README.md) - Public v0.1の概要
- [DEPENDENCIES](DEPENDENCIES.md) - 外部依存関係
- [SOURCE_VERSIONING](SOURCE_VERSIONING.md) - Source Versionの扱い
- [THIRD_PARTY_NOTICES](THIRD_PARTY_NOTICES.md) - 第三者Componentの権利情報
- [Firmware README](Firmware/Arduino/BODYLOBO/Salieri_BODYLOBO_Unified_PCA9685_10Servo_115200/README.md) - Arduino / Servo側

今後、より詳細なSource単位の責任と呼び出し関係は`SOURCE_GUIDE.md`として整理していく予定です。
