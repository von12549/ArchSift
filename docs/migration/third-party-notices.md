# ArchSift W00 third-party licenses and provenance

Date: 2026-10-06 (Australia/Sydney)

Status: candidate dependency license inventory/text/provenance archived. W01 freezes actual NuGet resolution in locks; W08 verifies actual ZIP runtime components/distribution inventory. No product package exists at this point.

## Archive inventory

| Component | Version baseline | License | Local archive | Authority and attribution |
| --- | --- | --- | --- | --- |
| TngTech.ArchUnitNET | 0.13.4 | Apache-2.0 | [LICENSE](licenses/ArchUnitNET-0.13.4-LICENSE.txt), [NOTICE](licenses/ArchUnitNET-0.13.4-NOTICE.txt) | [Package-source LICENSE](https://github.com/TNG/ArchUnitNET/blob/1ab5943d761d48f86b42f45ef047130dc9aff1c6/LICENSE), [NOTICE](https://github.com/TNG/ArchUnitNET/blob/1ab5943d761d48f86b42f45ef047130dc9aff1c6/NOTICE); Florian Gather, Paula Ruiz, Fritz Brandhuber, Pavel Fischer; TNG Technology Consulting GmbH |
| CycleDetection | 2.0.0 | MIT (publisher expression) | [MIT terms](licenses/CycleDetection-2.0.0-MIT.txt), [nuspec](evidence/cycledetection-2.0.0.nuspec), [NuGet MIT reference](evidence/nuget-MIT-license-reference.html) | Daniel Bradley, Robert Giesecke; [official source README](https://github.com/RobertGiesecke/CycleDetection/blob/4f6a70768ec1354efb08841f8cc0839b8e1b0586/README.markdown); actual nuspec declares MIT |
| JetBrains.Annotations | 2026.2.0 | MIT | [LICENSE](licenses/JetBrains.Annotations-2026.2.0-LICENSE.txt), [nuspec](evidence/jetbrains.annotations-2026.2.0.nuspec) | [Fixed official-source license](https://github.com/JetBrains/JetBrains.Annotations/blob/51456a32946d20b160c91b384e08ba6d4a5731e4/license.md); license Copyright (c) 2016–2024 JetBrains s.r.o.; package metadata Copyright (c) 2016–2025 JetBrains s.r.o.; retain both |
| Mono.Cecil | 0.11.6 | MIT | [LICENSE](licenses/Mono.Cecil-0.11.6-LICENSE.txt) | [0.11.6 tag LICENSE](https://github.com/jbevain/cecil/blob/0.11.6/LICENSE.txt); Copyright (c) 2008–2015 Jb Evain; Copyright (c) 2008–2011 Novell, Inc. |
| Newtonsoft.Json | 13.0.4 | MIT | [LICENSE](licenses/Newtonsoft.Json-13.0.4-LICENSE.txt), [nuspec](evidence/newtonsoft.json-13.0.4.nuspec) | Actual nupkg LICENSE.md; source `4e13299d4b0ec96bd4df9954ef646bd2d1b5bf2a`; Copyright (c) 2007 James Newton-King (license), Copyright © James Newton-King 2008 (metadata); preserve originals |
| System.ValueTuple | 4.6.2, supplementary transitive candidate | MIT | [LICENSE](licenses/System.ValueTuple-4.6.2-LICENSE.txt), [nuspec](evidence/system.valuetuple-4.6.2.nuspec) | [Package-source LICENSE](https://github.com/dotnet/maintenance-packages/blob/84b081710c66cc560b77d4de38cee493c3a38e9d/LICENSE); .NET Foundation and Contributors; package copyright Microsoft Corporation |
| System.Collections.Immutable | 1.5.0, supplementary transitive lower-bound candidate | MIT | [LICENSE](licenses/System.Collections.Immutable-1.5.0-LICENSE.txt), [THIRD-PARTY-NOTICES](licenses/System.Collections.Immutable-1.5.0-THIRD-PARTY-NOTICES.txt), [nuspec](evidence/system.collections.immutable-1.5.0.nuspec) | Actual nupkg license/notices; [metadata-linked CoreFX source license](https://github.com/dotnet/corefx/blob/30ab651fcb4354552bd4891619a0bdd81e0ebdbf/LICENSE.TXT); .NET Foundation and Contributors; package copyright Microsoft Corporation |

Remote license archives are UTF-8/LF snapshots with final newline. Local nuspec/license snapshots only normalize line endings; [machine inventory](third-party-inventory.json) separately records original cache SHA-256. Snapshot hashes are in [W00 delivery manifest](../acceptance/20261006-w00-artifact-manifest.json), never presented as upstream byte hashes when normalization differs.

## Evidence and limitations

Guard manual loading lists the first five packages, only its selected DLL closure. ArchUnitNET netstandard2.0 nuspec also declares System.ValueTuple >=4.6.2; a compatible CycleDetection group declares System.Collections.Immutable >=1.5.0. Earlier complete-closure wording refers to Guard's known manual baseline, not ArchSift restore completeness. NETStandard.Library/System.Runtime from other framework groups are not automatically resolved net10 dependencies.

Read-only verification found seven local nupkg SHA-512 values matching .nupkg.sha512 and 33 package DLL SHA-256 values matching extracted cache files. First five also match frozen Guard locks. Supplementary packages establish local package/metadata consistency only, not signature validation/redownload. W01/W08 govern actual framework groups, version promotion, full closure and assets.

Fixed CycleDetection source tree has no separate LICENSE/NOTICE; publisher nuspec explicitly declares MIT. Archived MIT is standard terms for reading, not a claimed original repository LICENSE; no copyright years invented. Retain Daniel Bradley/Robert Giesecke attribution. Official README notes earlier algorithm examples from Stack Overflow. ArchSift consumes NuGet binaries, not copied algorithm source; any later source copying requires separate snippet-license review. Retain this information in W08 notices.

JetBrains official license commit is not bound to package 2026.2.0. Record selected commit and metadata without claiming package build provenance. Preserve both license and package copyright lines.

## Implementation and distribution

W01 freezes actual direct/transitive versions/framework groups/locks; extend inventory for new schema/test/other dependencies. W05 calls ArchUnitNET APIs without copying implementation source. No dependency DLL has yet been placed in ArchSift.

W08 derives release components from actual publish output/locks: full MIT licenses/copyright, ArchUnitNET full Apache-2.0 LICENSE/NOTICE and actual Microsoft component notices. Record partial distribution explicitly instead of treating candidate list as a package audit. Collect self-contained .NET runtime license/notices for the selected actual runtime.

## Guard asset authorization

User explicitly stated full Guard ownership and reuse/distribution rights (English translation; original authorization remains in provenance). [Source inventory](source-inventory.json) records `GUARD-OWNER-20261006`, covering selective copying/adaptation/distribution of selected assets with ArchSift; third-party libraries retain their own licenses above. No public Guard LICENSE no longer blocks these authorized migrations. This record chooses no public open-source license for Guard or ArchSift.
