# THIRD-PARTY NOTICES

この文書はProject Salieri AI Public v0.1が参照または利用する外部Dependencyを整理するものです。下表の第三者Source、Native Binary、Model、Dictionary、購入AssetはPublic Repositoryに収録しません。Dependency宣言は第三者Package本体の再配布を意味しません。

Project Salieri License v1.0はProject Salieri独自Sourceと、同Licenseの対象として明示されたStudio Hazama 714制作Assetにのみ適用されます。第三者コンポーネント、利用者Avatar、voice/modelのLicenseやCopyrightは変更しません。

| Component / Artifact | Exact Version | Provider | Upstream URL | License | Included in Repository | Redistribution Status | Attribution / Notice Requirement | User Installation Required | Notes |
|---|---|---|---|---|---:|---|---|---:|---|
| Unity Engine / Unity Editor | 2022.3.62f3 | Unity Technologies | https://unity.com/releases/editor/whats-new/2022.3.62f3 | Unity Terms / applicable Unity license | NO | Not redistributed | Unity terms apply | YES | Unity Hubから導入。 |
| OpenCV for Unity Asset Store product | REVIEW REQUIRED | Enox Software | https://assetstore.unity.com/packages/tools/integration/opencv-for-unity-21088 | Unity Asset Store EULA / publisher terms | NO | Purchased Asset content not redistributed | Asset Store / publisher terms apply | YES | Production treeからexact versionを確定できない。 |
| OpenCV | Version selected by OpenCV for Unity | OpenCV project contributors | https://opencv.org/license/ | Apache-2.0 | NO | Not redistributed separately | Preserve upstream notices if redistributed | YES, via OpenCV for Unity | Asset Store productとupstream OpenCVを分離して扱う。 |
| UniVRM | 0.131.0 | VRM Consortium | https://github.com/vrm-c/UniVRM/releases/tag/v0.131.0 | MIT | NO | Upstream terms apply | Preserve copyright/license if redistributed | YES | UPM導入。Copyright (c) 2020 VRM Consortium。 |
| UniGLTF | 0.131.0 | VRM Consortium / ousttrue | https://github.com/vrm-c/UniVRM/releases/tag/v0.131.0 | MIT | NO | Upstream terms apply | Preserve copyright/license if redistributed | YES | UPM導入。Copyright (c) 2018 ousttrue。 |
| User VRM0 Avatar | User-selected | Avatar rights holder | User-selected distribution source | Avatar-specific license | NO | Project does not redistribute | Follow Avatar license and author conditions | YES | Asis3D Avatarは非収録。VirtualBody FBXとは別物。 |
| TextMesh Pro | 3.0.7 | Unity Technologies ApS | https://docs.unity3d.com/Packages/com.unity.textmeshpro@3.0/license/LICENSE.html | Unity Companion License | NO | Unity Package Manager経由 | Unity Companion License applies | YES | `Packages/manifest.json`で指定。 |
| SQLite-net Unity package | 1.3.2 | Gil Barbosa Reis / Krueger Systems, Inc. | https://github.com/gilzoide/unity-sqlite-net/tree/1.3.2 | MIT | NO | Package payload not redistributed | Preserve bundled upstream notices if redistributed | YES | `com.gilzoide.sqlite-net`。 |
| Vosk library / Windows native runtime | REVIEW REQUIRED | Alpha Cephei / Vosk project | https://github.com/alphacep/vosk-api | Apache-2.0 | NO | Native runtime not redistributed | Preserve Apache-2.0 notice if redistributed | YES | Public source P/Invokes `libvosk`; exact Windows runtime artifact/version未確定。 |
| Vosk Android library | 0.3.75 (`com.alphacephei:vosk-android:0.3.75@aar`) | Alpha Cephei / Vosk project | https://github.com/alphacep/vosk-android-demo/blob/master/app/build.gradle | Apache-2.0 | NO | AAR/native payload not redistributed | Preserve Apache-2.0 notice if redistributed | YES | Official Android demoでartifact coordinateを確認。Project sourceのruntime identifier `vosk_android_0.3.75`と整合。 |
| Vosk Japanese model | vosk-model-small-ja-0.22 | Alpha Cephei / model contributors | https://alphacephei.com/vosk/models | Apache-2.0 | NO | Model archive not redistributed | Preserve model license/notice if redistributed | YES | Official model listがApache 2.0を明記。Runtime manifestはversion 0.22を要求。 |
| JNA | 5.18.1 (`net.java.dev.jna:jna:5.18.1@aar`) | Java Native Access project | https://github.com/java-native-access/jna | LGPL-2.1-or-later OR Apache-2.0 | NO | Binary not redistributed | Distribution時に採用license optionのcopyright/license noticeを保持 | YES, Android Vosk dependency | Vosk official Android demoが5.18.1を使用。JNA 4.0以降はLGPL-2.1-or-laterまたはApache-2.0を選択可能。 |
| VOICEVOX Core source/current build line | REVIEW REQUIRED | VOICEVOX project | https://github.com/VOICEVOX/voicevox_core | MIT for current source/build line | NO | Core binary not redistributed | Exact selected artifactのlicense/noticeを確認 | YES | pre-0.16系buildは条件が異なるためversion確定必須。 |
| VOICEVOX runtime / Unity native bridge artifact | REVIEW REQUIRED | Project-specific build / upstream dependencies | https://github.com/VOICEVOX/voicevox_core | REVIEW REQUIRED | NO | Native binary not redistributed | Build provenanceと全同梱noticeを確認 | YES | `voicevox_unity_bridge`、`voicevox_runtime`等。exact build不明。 |
| VOICEVOX voice / `.vvm` model | REVIEW REQUIRED | Respective voice/model provider | https://voicevox.hiroshiba.jp/ | Voice/model-specific terms | NO | Voice/model not redistributed | Provider-specific terms apply | YES | `1.vvm`等。Core licenseとは別に扱う。 |
| ONNX Runtime | REVIEW REQUIRED | Microsoft | https://github.com/microsoft/onnxruntime | MIT | NO | Native binary not redistributed | Redistribution時はThirdPartyNoticesを含める | YES | Windows/Android VOICEVOX等のruntime dependency。exact version不明。 |
| Open JTalk | open_jtalk_dic_utf_8-1.11 distribution; runtime version REVIEW REQUIRED | Nagoya Institute of Technology contributors | https://open-jtalk.sourceforge.net/ | BSD-style notices | NO | Binary/dictionary not redistributed | Distributionに含まれる全noticeを保持 | YES | Dictionaryは`Assets/StreamingAssets/VoiceVox/open_jtalk_dic_utf_8-1.11`想定。 |
| UniDic dictionary data | Distribution bundled with selected Open JTalk dictionary; exact revision REVIEW REQUIRED | UniDic Consortium / contributors | https://clrd.ninjal.ac.jp/unidic/ | Distribution-specific notices | NO | Dictionary not redistributed | Distributionに含まれる全noticeを保持 | YES | Open JTalk runtimeと辞書権利を分離して確認。 |
| usb-serial-for-android | 3.11.0 | Google Inc., Mike Wakerly and contributors | https://github.com/mik3y/usb-serial-for-android/tree/v3.11.0 | MIT | NO | AAR not redistributed | Preserve MIT copyright/license if redistributed | YES | Android USB-OTG / FT232R・CH34x経路。 |
| AndroidX | Resolved by user build; REVIEW REQUIRED | Android Open Source Project / Google | https://developer.android.com/jetpack/androidx | Apache-2.0 | NO | Gradle dependency not vendored | Preserve applicable notices | YES | exact resolved modules/versionは公開Projectではpinされていない。 |
| R8 build tool artifact | 8.13.19 | Google / R8 project | https://r8.googlesource.com/r8/+/refs/heads/main/LICENSE | BSD-3-Clause | NO | JAR not redistributed | If redistributed, retain copyright, conditions and disclaimer | YES | Project expects `Assets/Plugins/Android/BuildTools/r8-8.13.19.jar`. Upstream documents Google Maven and `https://storage.googleapis.com/r8-releases/raw/<version>/r8.jar`; local artifact checksum/provenance pinning remains REVIEW REQUIRED. |
| YOLOX source / architecture | Unpinned | Megvii and contributors | https://github.com/Megvii-BaseDetection/YOLOX | Apache-2.0 | NO | Upstream source not redistributed here | Preserve Apache-2.0 notice if redistributed | YES, as external basis | Project Salieri独自Adapterとは別Dependency。 |
| `yolox_tiny.onnx` model artifact | Candidate upstream release 0.1.1rc0; exact artifact REVIEW REQUIRED | Megvii and contributors / selected artifact provider | https://github.com/Megvii-BaseDetection/YOLOX/releases/tag/0.1.1rc0 | REVIEW REQUIRED for exact artifact | NO | Model not redistributed | Verify artifact hash, provenance and terms before redistribution | YES | Expected pathは`Assets/StreamingAssets/OpenCVForUnityExamples/dnn/yolox_tiny.onnx`。 |
| Arduino AVR Core / Wire | User-installed; not pinned | Arduino | https://github.com/arduino/ArduinoCore-avr | Core files retain individual notices; main Arduino core headers use LGPL-2.1-or-later; Wire retains its applicable upstream notices | NO | Boards Manager dependency | Preserve applicable source notices if redistributed | YES | Firmware build dependency。Repository全体はfile単位noticeを確認する前提。 |
| Adafruit PWM Servo Driver Library | 3.0.3 validation-side version | Adafruit | https://github.com/adafruit/Adafruit-PWM-Servo-Driver-Library | BSD-3-Clause | NO | Library Manager dependency | Preserve copyright/license if redistributed | YES | Firmware build dependency。 |
| Adafruit BusIO | 1.17.4 validation-side version | Adafruit | https://github.com/adafruit/Adafruit_BusIO | MIT | NO | Library Manager dependency | Preserve copyright/license if redistributed | YES | PWM Servo Driver dependency。 |

## Project-owned assets (not third-party)

次の2 FBXはStudio Hazama 714 / Hazama本人が制作したOriginal VirtualBody / Robot Body Assetであり、VRM / VRoid Avatar由来ではありません。Public v0.1への配布が許可されています。

```text
Assets/LobBodyModels/RobotBody/Asis3D_3_Vbody.fbx
Assets/LobBodyModels/RobotBody/AILob.fbx
```

## 明示的な非収録物

- Asis3D AvatarおよびAvatar由来Asset
- Local LLM Source / GGUF Model / native runtime
- OpenCV for Unity Asset Store package contents
- DLL、SO、dylib、AAR、JAR
- Vosk / VOICEVOX / Open JTalk / UniDic / YOLOX等のModel・Dictionary・Binary

`REVIEW REQUIRED`は、現時点のRepositoryに該当物を含むという意味ではありません。将来そのartifactを配布物へ含める場合、または完全再現手順を保証する場合に、exact artifact、version、provider、license、notice、再配布条件を確認する必要があります。
