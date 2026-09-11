# THIRD-PARTY NOTICES

この文書はProject Salieri AI Public v0.1が参照または利用する外部Dependencyを整理するものです。Public Repositoryには、下表の第三者Source、Native Binary、Model、Dictionary、購入Assetを収録しません。`Packages/manifest.json`等のDependency宣言は、第三者Package本体の再配布を意味しません。

Project Salieri License v1.0はProject Salieri独自Sourceにのみ適用され、第三者コンポーネントのLicenseやCopyrightを変更しません。利用者は導入時点の配布元Licenseと利用条件を確認してください。

| Name | Provider | License | Included in Repository | User Installation Required | Redistribution status | Notes |
|---|---|---|---:|---:|---|---|
| Unity Engine / Unity Editor | Unity Technologies | Unity Terms / applicable Unity license | NO | YES | Not redistributed | Unity HubからUnity 2022.3.62f3を導入。 |
| OpenCV for Unity | Enox Software | Unity Asset Store EULA / publisher terms | NO | YES | Redistribution of purchased Asset content prohibited | 利用者自身のAsset Store entitlementで導入。Project Salieri Sourceと混同しない。 |
| UniVRM | VRM Consortium | MIT | NO | YES | Upstream license applies | Validation環境で確認した版は0.131.0。Copyright (c) 2020 VRM Consortium。 |
| UniGLTF | VRM Consortium / ousttrue | MIT | NO | YES | Upstream license applies | Validation環境で確認した版は0.131.0。Copyright (c) 2018 ousttrue。 |
| TextMesh Pro | Unity Technologies ApS | Unity Companion License | NO | YES | Unity Package Manager経由 | Validation環境は3.0.7。Unity-dependent projectとして利用。 |
| SQLite-net Unity package | Gil Barbosa Reis / Krueger Systems, Inc. | MIT | NO | YES | Upstream notices must be preserved if redistributed | `com.gilzoide.sqlite-net` 1.3.2。Repository内のpackage payloadは非収録。 |
| Windows Vosk runtime | Alpha Cephei / Vosk project | Apache-2.0 | NO | YES | Native runtime not redistributed | 利用者が互換Vosk runtimeを導入。 |
| Vosk Japanese Model | Alpha Cephei / model contributors | Apache-2.0 (local model manifest evidence) | NO | YES | Model archive not redistributed | Validation環境のモデル識別子は`vosk-model-small-ja-0.22`。配布元条件を再確認。 |
| VOICEVOX runtime / models | VOICEVOX project and respective model/voice providers | REVIEW REQUIRED | NO | YES | Binary/model redistribution not cleared | Core、engine、model、voiceごとに条件が異なり得るため、利用者が公式条件を確認。 |
| ONNX Runtime | Microsoft | MIT | NO | YES | Native binary not redistributed | 利用者が対象Platform用Runtimeを導入。 |
| Open JTalk / UniDic | Nagoya Institute of Technology / NAIST / UniDic Consortium and contributors | BSD-style multi-notice; exact bundled set must be preserved | NO | YES | Dictionary and binary not redistributed | Validation用Dictionaryには複数のCopyright/License noticeがある。 |
| Android Vosk | Alpha Cephei / Vosk project | Apache-2.0 | NO | YES | AAR/native payload not redistributed | Android向けSTT runtime。 |
| JNA | Java Native Access project | LGPL-2.1-or-later OR Apache-2.0 | NO | YES | Dual-license conditions apply | Android Vosk依存。採用するlicense optionを利用者側Distributionで確認。 |
| usb-serial-for-android | Google Inc., Mike Wakerly and contributors | MIT | NO | YES | AAR not redistributed | Validation環境は3.11.0。Android USB-OTG / FT232R経路で利用。 |
| AndroidX | Android Open Source Project / Google | Apache-2.0 | NO | YES | Gradle dependencyとして取得 | Android build dependency。 |
| R8 | Google | REVIEW REQUIRED | NO | YES | Build tool binary not redistributed | Public build templateはValidation環境の`r8-8.13.19.jar`を参照。対象artifactのnoticeを導入時に確認。 |
| YOLOX Source / architecture | Megvii and contributors | Apache-2.0 | NO | YES | Upstream code not redistributed here | Project Salieri独自Adapterとは別Dependency。 |
| YOLOX ONNX model | Model artifact provider | REVIEW REQUIRED | NO | YES | Model redistribution not cleared | exact model artifactのprovenance/license確認が必要。Public v0.1には非収録。 |
| Arduino AVR Core / Wire | Arduino | Wire: LGPL-2.1-or-later; AVR Core files retain their own notices | NO | YES | Boards Managerから導入 | Firmware build dependency。導入版はsketchではpinされない。 |
| Adafruit PWM Servo Driver Library | Adafruit | BSD-3-Clause | NO | YES | Library Managerから導入 | Validation側確認版3.0.3。 |
| Adafruit BusIO | Adafruit | MIT | NO | YES | Library Managerから導入 | Validation側確認版1.17.4。PWM Servo Driverの依存。 |

## 明示的な非収録物

- Asis3Dおよびその派生Avatar Asset
- Local LLM Source / GGUF Model / native runtime
- OpenCV for Unity Asset Store package contents
- DLL、SO、dylib、AAR、JAR
- Vosk / VOICEVOX / Open JTalk / UniDic / YOLOX等のModel・Dictionary・Binary

`REVIEW REQUIRED`の項目は、将来それ自体を配布物へ含める前に、exact artifact、version、provider、license、notice、再配布条件の確認が必要です。
