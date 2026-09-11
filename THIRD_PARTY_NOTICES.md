# THIRD-PARTY NOTICES

この文書はProject Salieri AI Public v0.1が参照または利用する外部Dependencyを整理するものです。下表の第三者Source、Native Binary、Model、Dictionary、購入AssetはPublic Repositoryに収録しません。Dependency宣言は第三者Package本体の再配布を意味しません。

Project Salieri License v1.0はProject Salieri独自Sourceと、同Licenseの対象として明示されたStudio Hazama 714制作Assetにのみ適用されます。第三者コンポーネント、利用者Avatar、voice/modelのLicenseやCopyrightは変更しません。

| Component / Artifact | Exact Version | Provider | Upstream URL | License / Terms | Included in Repository | Redistribution Status | Attribution / Notice Requirement | User Installation Required | Notes |
|---|---|---|---|---|---:|---|---|---:|---|
| Unity Engine / Unity Editor | 2022.3.62f3 | Unity Technologies | https://unity.com/releases/editor/whats-new/2022.3.62f3 | Unity Terms / applicable Unity license | NO | Not redistributed | Unity terms apply | YES | Unity Hubから導入。 |
| OpenCV for Unity Asset Store product | 3.0.2 | Enox Software | https://enoxsoftware.com/opencv-for-unity-ver3-0-2-release/ | Unity Asset Store EULA / publisher terms | NO | Purchased Asset content not redistributed | Asset Store / publisher terms apply | YES | Enox公式release noteで3.0.2、内包OpenCV 4.13.0を確認。 |
| OpenCV | 4.13.0, as bundled by OpenCV for Unity 3.0.2 | OpenCV project contributors | https://opencv.org/license/ | Apache-2.0 | NO | Not redistributed separately | Preserve upstream notices if redistributed | YES, via OpenCV for Unity | Asset Store productとupstream OpenCVを分離して扱う。 |
| UniVRM | 0.131.0 | VRM Consortium | https://github.com/vrm-c/UniVRM/releases/tag/v0.131.0 | MIT | NO | Upstream terms apply | Preserve copyright/license if redistributed | YES | UPM導入。Copyright (c) 2020 VRM Consortium。 |
| UniGLTF | 0.131.0 | VRM Consortium / ousttrue | https://github.com/vrm-c/UniVRM/releases/tag/v0.131.0 | MIT | NO | Upstream terms apply | Preserve copyright/license if redistributed | YES | UPM導入。Copyright (c) 2018 ousttrue。 |
| User VRM0 Avatar | User-selected | Avatar rights holder | User-selected distribution source | Avatar-specific license | NO | Project does not redistribute | Follow Avatar license and author conditions | YES | Asis3D Avatarは非収録。VirtualBody FBXとは別物。 |
| TextMesh Pro | 3.0.7 | Unity Technologies ApS | https://docs.unity3d.com/Packages/com.unity.textmeshpro@3.0/license/LICENSE.html | Unity Companion License | NO | Unity Package Manager経由 | Unity Companion License applies | YES | `Packages/manifest.json`で指定。 |
| SQLite-net Unity package | 1.3.2 | Gil Barbosa Reis / Krueger Systems, Inc. | https://github.com/gilzoide/unity-sqlite-net/tree/1.3.2 | MIT | NO | Package payload not redistributed | Preserve bundled upstream notices if redistributed | YES | `com.gilzoide.sqlite-net`。 |
| Vosk library / Windows native runtime | UNCONFIRMED exact runtime version | Alpha Cephei / Vosk project | https://github.com/alphacep/vosk-api | Apache-2.0 | NO | Native runtime not redistributed | Preserve Apache-2.0 notice; dependency runtimes must be audited separately if redistributed | YES | Public source P/Invokes `libvosk`; local DLL SHA-256 `9331C2F6A32CF77141AF27C9750E79532718F51BBC2E3CEA3C60F14CA4251E4E`。DLLにversion metadataがなく元archive不明。 |
| Vosk Android library | 0.3.75 (`com.alphacephei:vosk-android:0.3.75@aar`) | Alpha Cephei / Vosk project | https://github.com/alphacep/vosk-android-demo/blob/master/app/build.gradle | Apache-2.0 | NO | AAR/native payload not redistributed | Preserve Apache-2.0 notice if redistributed | YES | Official Android demoでartifact coordinateを確認。Project sourceのruntime identifier `vosk_android_0.3.75`と整合。 |
| Vosk Japanese model | vosk-model-small-ja-0.22 | Alpha Cephei / model contributors | https://alphacephei.com/vosk/models | Apache-2.0 | NO | Model archive not redistributed | Preserve model license/notice if redistributed | YES | Official model listがApache 2.0を明記。Runtime manifestはversion 0.22を要求。 |
| JNA | 5.18.1 (`net.java.dev.jna:jna:5.18.1@aar`) | Java Native Access project | https://github.com/java-native-access/jna | LGPL-2.1-or-later OR Apache-2.0 | NO | Binary not redistributed | Distribution時に採用license optionのcopyright/license noticeを保持 | YES, Android Vosk dependency | Vosk official Android demoが5.18.1を使用。JNA 4.0以降はdual license。 |
| VOICEVOX Core | 0.16.4 | VOICEVOX project | https://github.com/VOICEVOX/voicevox_core | MIT for source and build artifacts in 0.16+ line | NO | Core binary not redistributed | If redistributed, retain MIT copyright/license | YES | Validation binary reports 0.16.4。VOICEVOX公式は0.16未満のbuild artifactだけ別Licenseと明記。Local archive provenanceは未保存。 |
| VOICEVOX ONNX Runtime | 1.17.3 (`voicevox_onnxruntime-1.17.3`) | VOICEVOX project / onnxruntime-builder | https://github.com/VOICEVOX/onnxruntime-builder/releases | VOICEVOX ONNX Runtime terms | NO | Current Repository does not redistribute. Official terms permit application-bundled redistribution. | VOICEVOX credit required; generated audio must follow each voice-library term | YES | VOICEVOX Core 0.16系の製品版VVM用runtime。Generic Microsoft ONNX Runtimeと同一扱いしない。Validation binaries report 1.17.3。 |
| `voicevox_unity_bridge.dll` | Native source/build provenance NOT FOUND | Origin UNKNOWN; copied through AITuber_PCDev lineage | N/A — native source/build project not found | REVIEW REQUIRED | NO | Do not redistribute | Rights/build provenance must be established before any distribution | YES for current Windows VOICEVOX integration | SHA-256 `0D93047EF46047854ED00F0F009DC9B042C68C417E421B8657C3CF9D1AA5C94B`。ProductionとAITuber_PCDev保存Binaryはexact match。Native author/source/build recipeは未特定。 |
| `libvoicevox_runtime.so` | Project-specific build; provenance CONFIRMED OWN-DEPENDENT | Studio Hazama 714 / Hazama integration, dependent on VOICEVOX Core | Local Android Studio `VoiceVoxRuntime` project; not published in this repository | Studio-owned wrapper; public license migration REVIEW REQUIRED; third-party dependencies keep own terms | NO | Do not redistribute in Public v0.1 | If later distributed, add Project license notice and preserve VOICEVOX / header / dependency notices | YES for current Android VOICEVOX integration | SHA-256 `F1190908EFC5FC5BB60E4FF748E8AFBB8FC0EF052ACC829E544752CC39CF5CC5`。Source/CMake/Gradle/toolchain/Exact Build Matchを確認。Current binary is unstripped Debug build. |
| VOICEVOX VVM `1.vvm` / 冥鳴ひまり | model metadata 0.16.0, VVM format 1, style ID 14 | VOICEVOX project / 冥鳴ひまり rights holder | https://github.com/VOICEVOX/voicevox_vvm | VOICEVOX voice-model terms + 冥鳴ひまり voice-library terms | NO | Repository does not redistribute. Current VOICEVOX VVM terms permit application-bundled redistribution. | VOICEVOX credit required; generated audio using this library requires `VOICEVOX:冥鳴ひまり` credit and the character-specific terms | YES | Selected Production model is `1.vvm`; SHA-256 `8DF20815BE9A84A4B4723B9E778D2EF6A4DF277E4872FE9EFE29676A60E02774`。 |
| Open JTalk dictionary | `open_jtalk_dic_utf_8-1.11` distribution | Nagoya Institute of Technology contributors | https://open-jtalk.sourceforge.net/ | Distribution-specific BSD-style notices | NO | Dictionary not redistributed | Distributionに含まれる全noticeを保持 | YES | Expected pathは`Assets/StreamingAssets/VoiceVox/open_jtalk_dic_utf_8-1.11`。Archive hash / bundled notice setはまだpinしていない。 |
| UniDic dictionary data | Distribution bundled with selected Open JTalk dictionary; exact revision REVIEW REQUIRED | UniDic Consortium / contributors | https://clrd.ninjal.ac.jp/unidic/ | Distribution-specific notices | NO | Dictionary not redistributed | Distributionに含まれる全noticeを保持 | YES | Open JTalk runtimeと辞書権利を分離して確認。 |
| usb-serial-for-android | 3.11.0 | Google Inc., Mike Wakerly and contributors | https://github.com/mik3y/usb-serial-for-android/tree/v3.11.0 | MIT | NO | AAR not redistributed | Preserve MIT copyright/license if redistributed | YES | Android USB-OTG / FT232R・CH34x経路。 |
| AndroidX | Resolved by user build; REVIEW REQUIRED | Android Open Source Project / Google | https://developer.android.com/jetpack/androidx | Apache-2.0 | NO | Gradle dependency not vendored | Preserve applicable notices | YES | exact resolved modules/versionは公開Projectではpinされていない。 |
| R8 build tool artifact | 8.13.19 | Google / R8 project | https://r8.googlesource.com/r8/+/refs/heads/main/LICENSE | BSD-3-Clause | NO | JAR not redistributed | If redistributed, retain copyright, conditions and disclaimer | YES | Expected path `Assets/Plugins/Android/BuildTools/r8-8.13.19.jar`; version commit `4ab5d6fdeb2fdaa29021e03d0ee02219e53e39fd`; SHA-256 `7712EDBAE6A71F35937FFC1BD5F4C202919EF2A5E58D3C3544015A868A3CF457`。 |
| YOLOX source / architecture | 0.1.1rc0 release lineage for selected model | Megvii and contributors | https://github.com/Megvii-BaseDetection/YOLOX | Apache-2.0 | NO | Upstream source not redistributed here | Preserve Apache-2.0 notice if redistributed | YES, as external basis | Project Salieri独自Adapterとは別Dependency。 |
| `yolox_tiny.onnx` model artifact | 0.1.1rc0 official release asset | Megvii and contributors | https://github.com/Megvii-BaseDetection/YOLOX/releases/download/0.1.1rc0/yolox_tiny.onnx | Upstream repository is Apache-2.0; no separate artifact-specific license found | NO | Model not redistributed | If redistributed, retain upstream Apache-2.0 notice and recheck release artifact terms | YES | SHA-256 `427CC366D34E27FF7A03E2899B5E3671425C262EA2291F88BB942BC1CC70B0F7`; SHA-1 `45985579A307AAE54C7B54CA257BC0B48606DEAC` matches local downloader definition。 |
| Arduino AVR Core / Wire | Validation PC installed Arduino AVR Boards 1.8.6; firmware actual build version UNCONFIRMED | Arduino | https://github.com/arduino/ArduinoCore-avr | Core files retain individual notices; main Arduino core headers use LGPL-2.1-or-later; Wire retains its applicable upstream notices | NO | Boards Manager dependency | Preserve applicable source notices if redistributed | YES | Firmware compile remains `NOT RUN`; 1.8.6は導入Versionでありcompile済みversionとは主張しない。 |
| Adafruit PWM Servo Driver Library | 3.0.3 validation-side version | Adafruit | https://github.com/adafruit/Adafruit-PWM-Servo-Driver-Library | BSD-3-Clause | NO | Library Manager dependency | Preserve copyright/license if redistributed | YES | Firmware build dependency。 |
| Adafruit BusIO | 1.17.4 validation-side version | Adafruit | https://github.com/adafruit/Adafruit_BusIO | MIT | NO | Library Manager dependency | Preserve copyright/license if redistributed | YES | PWM Servo Driver dependency。 |

## Third-party distribution boundary

Public v0.1は第三者Binary / Model / Dictionary / purchased Assetを配布しない方針です。したがって現時点のPublic Repositoryで第三者配布義務が直接発生する対象は、Gitに含まれるProject固有Sourceが参照するDependency説明とnoticeの正確性です。

将来、配布物へ第三者artifactを同梱する場合は次を別々に判断してください。

- **OpenCV for Unity**: Asset Store購入物をProject側から再配布しない。各利用者が自身のentitlementで取得する。
- **Vosk / JNA / MinGW runtime**: 現在は非収録。Binary同梱時はVosk本体だけでなく`libgcc` / `libstdc++` / `libwinpthread`等のruntime licenseも個別確認する。
- **VOICEVOX Core 0.16.4**: Official 0.16+ Core build artifactはMIT。再配布するならMIT noticeを保持する。
- **VOICEVOX ONNX Runtime 1.17.3**: Generic Microsoft ONNX RuntimeではなくVOICEVOX固有termsで扱う。公式termsはアプリ組み込み再配布を許可するが、VOICEVOX creditとvoice-library条件が必要。
- **VOICEVOX VVM / 冥鳴ひまり**: Current VVM termsはアプリ組み込み再配布を許可する。生成音声の利用は冥鳴ひまり側の条件も適用され、`VOICEVOX:冥鳴ひまり` creditが必要。
- **Windows `voicevox_unity_bridge.dll`**: 元Native Source/build recipe/作者記録を発見できず、OriginはUNKNOWN。現在のPublic repoには含めない。権利と再build provenanceが閉じるまで再配布しない。
- **Android `libvoicevox_runtime.so`**: Studio-owned wrapper SourceとExact Build Matchを確認した`OWN-DEPENDENT` integration。ただしPublic license migration、third-party header区分、Release/strip方針が未完了のためPublic v0.1にはSource/Binaryを含めない。
- **YOLOX ONNX**: exact official release artifactは特定済み。現状は非収録。再配布する場合はApache-2.0 noticeとartifact-specific termsの再確認を行う。
- **R8 / Arduino / Adafruit libraries**: Build dependencyとして利用者に取得させ、Public RepositoryではBinary / library payloadを同梱しない。

Project Salieri Licenseの「再配布禁止」はProject Salieri独自Sourceに対する条件であり、第三者artifactのlicenseを縮小・拡張しません。第三者側で再配布可能でも、Project Salieri本体の再配布にはProject Salieri Licenseの条件が別途適用されます。

## Project-owned integration components not included in Public v0.1

Android `libvoicevox_runtime.so`のwrapper implementationは技術監査上`CONFIRMED OWN-DEPENDENT`です。所有根拠とbuild provenanceは確認されていますが、現在のPublic v0.1境界ではSource/Binaryとも収録しません。将来公開対象にする場合は、Project Salieri License適用を明示し、`voicevox_core.h`等の第三者部分を分離してnoticeを維持してください。

Windows `voicevox_unity_bridge.dll`については同じ扱いに昇格させません。保存Binaryのlineageは確認できましたが、Native Sourceとauthor/build provenanceが見つからないため`UNKNOWN / NOT FOUND`のままです。

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

`REVIEW REQUIRED` / `UNCONFIRMED` / `NOT FOUND`は、現時点のRepositoryに該当物を含むという意味ではありません。将来そのartifactを配布物へ含める場合、または完全再現手順を保証する場合に、exact artifact、version、provider、license、notice、再配布条件を確認する必要があります。