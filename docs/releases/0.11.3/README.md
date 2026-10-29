# ArchSift 0.11.3 qualification build

ArchSift provides .NET architecture analysis, a framework-dependent CLI/Web workspace and architecture-test libraries. This version adds installed-host/configured-source IFX initialization while preserving exact package, policy and payload integrity checks.

The consumer supplies an eligible .NET host, restores its pinned packages from explicitly configured sources, and initializes external local workspace state. Ordinary workspace startup does not execute architecture tests. IFX architecture execution retains a separate approved offline preparation requirement.

This package set is produced for qualification. Public availability, final committed source identity and release acceptance must be established by a separate publication record. A stable-looking version number alone is not publication or qualification evidence. See the accompanying release notes and retained build/package manifests. Prior RC validation does not automatically accept these bytes.

No runtime installation, machine environment repair or new installer is included. Third-party notices and license texts remain part of the distribution.
