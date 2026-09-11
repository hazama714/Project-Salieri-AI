# Project Salieri AI — Source Versioning Policy

Effective date: 2026-09-11

## Independent version domains

Project release versions, individual source versions, and license versions are
managed independently.

- Current project release: Project Salieri AI Public v0.1
- Current license: Project Salieri License v1.0
- Initial public source version: 0.1.0
- Initial source date: 2026-09-11

A change to one domain does not automatically change either of the others.
Only source files actually changed by a later implementation change receive a
new source version.

## Version format

Each public Project Salieri source file uses an independent semantic version:

    MAJOR.MINOR.PATCH

All 502 sources in the Public v0.1 Source Manifest begin at 0.1.0. This records
the start of public source-version management; it does not mean that each
source is feature-complete or stable.

## 0.x development phase

A 0.x.y source is a development or pre-stable source whose responsibility,
public API, serialized contract, or input/output contract has not yet been
declared stable.

- PATCH: a bug fix, safety improvement, performance improvement, or other
  improvement within the existing behavior and responsibility.
- MINOR: a feature, capability extension, responsibility change, or breaking
  change made during the development phase.

Examples:

- 0.1.0 → 0.1.1: compatible fix or improvement.
- 0.1.x → 0.2.0: feature, capability, responsibility, or development-stage
  breaking change.

## Stable phase

Version 1.0.0 means that the source responsibility, public API, serialized
contract, and input/output contract have been deliberately accepted as a
stable source contract.

After 1.0.0:

- MAJOR: a breaking change, including a public API or namespace break, class
  contract change, serialized-field compatibility break, Scene or Prefab
  reference-contract change, input/output contract change, mandatory caller
  migration, or destructive responsibility change.
- MINOR: a backward-compatible feature, public method, optional setting,
  supported mode, or capability.
- PATCH: a backward-compatible bug fix, condition correction, exception or
  safety improvement, performance improvement, or internal improvement within
  the existing contract.

## Changes that do not increment the source version

The source version is not incremented for changes limited to:

- comments;
- license or copyright headers;
- source-version metadata;
- formatting;
- whitespace; or
- documentation that cannot change runtime behavior.

Accordingly, applying the initial public license and metadata header does not
change 0.1.0 to 0.1.1.

## Source Date

Source Date is the date on which that specific source version was established.
It is not a build timestamp and is not updated by a metadata-only or formatting-
only change. The initial Source Date for the 502-file Public v0.1 baseline is
2026-09-11.

## Initial source header

Each Own or included Own-Dependent source in the frozen Public v0.1 manifest
uses this header:

    // Project Salieri AI
    // Source Version: 0.1.0
    // Source Date: 2026-09-11
    // Copyright (c) 2026 Studio Hazama 714
    // Licensed under Project Salieri License v1.0
    // SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
    // See LICENSE for details.

Third-party source must retain its upstream copyright and license and must not
receive the Project Salieri ownership header. External dependency notices are
managed separately from Project Salieri source licensing.

## Asset boundary

Source versioning does not apply to avatar or other non-source assets. The
Asis3D VRM and its generated meshes, textures, materials, BlendShape assets,
Avatar assets, MetaObjects, and other derived assets are not part of the
502-source baseline and are not licensed under Project Salieri License v1.0.
They may be present in a local Staging workspace for validation but are not
approved for public redistribution.
