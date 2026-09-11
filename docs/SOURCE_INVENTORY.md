# Source Inventory

Project Salieri AI 現行ソースの棚卸し用ドキュメント。

この一覧は、公式HPおよび今後の公開リポジトリで「どのソースが何を担当しているか」を第三者が理解できる状態にすることを目的とします。

## 分類ルール

### Origin

- `Own` — Studio Hazama 714 が独自に作成したコード
- `Own-Dependent` — 自作コードだが外部ライブラリ/APIに依存
- `Modified` — 既存ソースやサンプルを改変・流用
- `Third-Party` — 第三者コードそのもの
- `Unknown` — 出自確認が必要

### Status

- `Production`
- `Test`
- `Debug`
- `Legacy`
- `Unknown`

### Public Scope

- `Public Candidate`
- `Private`
- `Hold`

## Inventory

| File | Main Type | Purpose | Category | Status | Origin | Current License | Target License | Public Scope | Notes |
|---|---|---|---|---|---|---|---|---|---|
| _TBD_ |  |  |  |  |  |  |  |  |  |

## 集計

- ソースファイル総数: TBD
- Production候補: TBD
- Test / Debug / Legacy候補: TBD
- 出自確認が必要なファイル: TBD
- 公開候補: TBD

## 注意

ライセンスは一括置換しません。`Modified`、`Third-Party`、`Unknown` は元ライセンスと出自を確認するまで独自ライセンスへ変更しないものとします。
