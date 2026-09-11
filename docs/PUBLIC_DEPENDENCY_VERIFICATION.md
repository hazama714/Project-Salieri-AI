# Public Dependency Verification

Project Salieri AI Public v0.1の外部Dependencyについて、Validation環境で確認できた実体fingerprintと、未確定項目を将来確定するための手順を記録します。

この文書はPublic packageの再現性監査用です。Secret、API Key、token、個人環境の絶対Pathは記録しません。

## 判定

- `CONFIRMED`: Version、実ファイル、SHA-256、実使用根拠が揃っている。
- `UNCONFIRMED`: 実ファイルや使用は確認できるが、exact version / archive provenance / build manifestのいずれかが不足している。
- `NOT FOUND`: 元archive、manifest、build record等が見つからない。

## Confirmed validation fingerprints

### OpenCV for Unity

- OpenCV for Unity: `3.0.2`
- Bundled OpenCV: `4.13.0`
- Windows x64 `opencvforunity.dll`
  - SHA-256: `4B6D8BF9E6B63B3451A49620F4A5AFA2CC3DB878416DEAE7CB0CBCE0E46F362C`
- Android arm64 `libopencvforunity.so`
  - SHA-256: `E8682834CC3054A93DABCA70198F9D67C3D0D6DB7803CAF3B519429684B33C22`

Public RepositoryにはAsset Store package内容を含めません。

### VOICEVOX Core

- Core version: `0.16.4`
- Windows x64 `voicevox_core.dll`
  - SHA-256: `DBA594584FD70A25148FA0F73D50D061FA75E2BDBE46DC37CE79BF772B675420`
- Android arm64 `libvoicevox_core.so`
  - SHA-256: `5382A785358F9A4B7510C2A8C317A4D985430CA415DA9C1FCC4529693A4C2420`

VOICEVOX公式は0.16+ Core source / build artifactをMITとして公開しています。

### VOICEVOX ONNX Runtime

- Runtime version: `1.17.3`
- Windows validation binary
  - SHA-256: `C677274EDB5A77EA26893BA8368B3791724AB8A7ACAAB73A310C04FC47E9CD82`
- Android validation binary
  - SHA-256: `9BD4FEE054893F4CDFAED00AC5257BC414E08E916BD73528C310F5423F762369`

VOICEVOX Core 0.16系の製品版VVMは`VOICEVOX ONNX Runtime`を要求します。Generic Microsoft ONNX Runtime 1.17.3と同一物として扱いません。

### Production-selected VVM

- File: `1.vvm`
- Voice: 冥鳴ひまり
- Style: ノーマル
- Style ID: `14`
- Voice model metadata version: `0.16.0`
- VVM format version: `1`
- SHA-256: `8DF20815BE9A84A4B4723B9E778D2EF6A4DF277E4872FE9EFE29676A60E02774`

Public RepositoryにはVVMを含めません。

### YOLOX Tiny ONNX

- File: `yolox_tiny.onnx`
- Official release lineage: `0.1.1rc0`
- Size: `20,219,662` bytes
- SHA-256: `427CC366D34E27FF7A03E2899B5E3671425C262EA2291F88BB942BC1CC70B0F7`
- SHA-1: `45985579A307AAE54C7B54CA257BC0B48606DEAC`

SHA-1はProjectで使用しているOpenCV for Unity downloader定義と一致しています。

### R8

- Version: `8.13.19`
- Version commit: `4ab5d6fdeb2fdaa29021e03d0ee02219e53e39fd`
- SHA-256: `7712EDBAE6A71F35937FFC1BD5F4C202919EF2A5E58D3C3544015A868A3CF457`
- Upstream license: BSD-3-Clause

Public RepositoryにはR8 JARを含めません。

## Unconfirmed items and resolution procedure

### 1. Windows Vosk native runtime

Current status:

- `libvosk.dll`の実使用を確認済み。
- SHA-256: `9331C2F6A32CF77141AF27C9750E79532718F51BBC2E3CEA3C60F14CA4251E4E`
- DLL内に製品Version metadataなし。
- 元配布archive / manifestは`NOT FOUND`。

確定手順:

1. 旧download archive、browser download history、package cache、backupを探す。
2. Archive内のREADME / VERSION / release tagを確認する。
3. 展開した`libvosk.dll`のSHA-256が上記fingerprintと一致することを確認する。
4. 一致した場合のみexact versionを`CONFIRMED`へ変更する。
5. 元archiveが見つからない場合は、推測で`0.3.x`を付与しない。
6. 再現性を優先する場合は、公式Vosk releaseから新たにversion-pinned runtimeを選定し、Production互換試験後に置換候補として別途扱う。既存Public v0.1 Sourceを無断変更しない。

Windows Voskを将来同梱する場合は`libvosk.dll`だけでなく、同梱される`libgcc` / `libstdc++` / `libwinpthread`等のlicenseも個別監査する。

### 2. `voicevox_unity_bridge.dll`

Current status:

- SHA-256: `0D93047EF46047854ED00F0F009DC9B042C68C417E421B8657C3CF9D1AA5C94B`
- 実使用確認済み。
- 独立SemVer / build manifestなし。
- Project内統合物だが、公開可能なnative source / build recipeのclosureが取れていない。

確定手順:

1. Native sourceのoriginを特定する。
2. Studio-owned source、modified third-party source、generated binaryを区別する。
3. Build toolchain、compiler version、target architecture、link dependencyを記録する。
4. Clean buildを行い、build outputのSHA-256を保存する。
5. 元binaryと一致しない場合は、動作互換性を検証した上で新buildを正式artifactとして採用するか判断する。
6. Source / build recipe / license provenanceが閉じるまでPublic配布しない。

### 3. Android `libvoicevox_runtime.so`

Current status:

- SHA-256: `F1190908EFC5FC5BB60E4FF748E8AFBB8FC0EF052ACC829E544752CC39CF5CC5`
- 実使用確認済み。
- Production内にnative source / build manifestなし。

確定手順:

1. 旧NDK project、CMake / ndk-build files、CI artifact、backupを探索する。
2. Source originとthird-party dependencyを記録する。
3. NDK version、ABI、STL、link libraries、compile flagsを固定する。
4. RebuildしてSHA-256を記録する。
5. Source/build provenanceが確定するまでPublic配布しない。

### 4. Arduino AVR Core actual build version

Current status:

- Validation PC installed Arduino AVR Boards: `1.8.6`
- Public v0.1 Firmware Compile: `NOT RUN`
- よって`1.8.6`は「導入Version」であり「compile済みVersion」ではない。

確定手順:

1. Arduino CLIまたはIDEでArduino AVR Boards `1.8.6`を明示選択する。
2. 対象Board / FQBNを記録する。
3. FirmwareをVerify / Compileする。
4. Compiler outputにCore 1.8.6の使用が現れることを保存する。
5. Compile result、warning、binary size、toolchain versionを記録する。
6. 実機UploadとStartup Full-Offを確認する場合は、それをCompile確認とは別Gateとして記録する。

### 5. Open JTalk / UniDic dictionary distribution

Current status:

- Expected directory: `open_jtalk_dic_utf_8-1.11`
- Distribution lineageは1.11まで特定。
- Archive SHA-256 / bundled notice setは未pin。

確定手順:

1. 公式配布元から`open_jtalk_dic_utf_8-1.11` archiveを取得する。
2. Archive SHA-256を記録する。
3. Archive内のCOPYING / LICENSE / README / dictionary noticeを保存する。
4. UniDic由来データのnoticeを分離して記録する。
5. Production側directoryのfile setとarchive展開結果が一致するか比較する。

## Public release gate interpretation

Public v0.1は第三者Binary / Model / DictionaryをRepositoryに同梱しません。そのため上記`UNCONFIRMED`項目は、現時点ではPublic source publicationのBlockerではなく、**完全再現性および将来のartifact redistributionに対するReview Required**です。

ただし次の状態になった場合はPublic blockerへ昇格します。

- 未確認third-party artifactをRepositoryへ追加する。
- License / attribution条件が不明なartifactを配布物へ同梱する。
- README / DEPENDENCIESが「cloneのみで動く」と誤認させる。
- Project-specific native bridgeを、source/build provenanceなしで公式配布物として含める。

## Source of truth

- Installation requirements: [`../DEPENDENCIES.md`](../DEPENDENCIES.md)
- Third-party terms / distribution boundary: [`../THIRD_PARTY_NOTICES.md`](../THIRD_PARTY_NOTICES.md)
- Project-specific license: [`../LICENSE`](../LICENSE)
