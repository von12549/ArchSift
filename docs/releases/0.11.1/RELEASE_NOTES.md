# ArchSift 0.11.1 recovery correction candidate

Unpublished candidate. Public publication is not authorized. The original 0.11.0 packages and acceptance history remain immutable.

An initialization interrupted during atomic marker creation can leave a valid transaction journal and generated configuration together with a workspace-state.json.<GUID>.tmp file. Version 0.11.0 rejects that file during resumption. This correction recognizes only the reserved root-level, lowercase GUID marker-temporary form after the journal matches the expected transaction. Temporary bytes remain untouched and are never parsed, promoted or trusted. The committed marker is rebuilt from the validated transaction; unknown names, links, changed bindings and configuration hashes still reject.

The five candidate NuGet packages share version 0.11.1; Cli includes the matching embedded Web payload. This is not a new installer and does not broaden Setup's historical ZIP allowlist. Existing source contracts, target execution restrictions, child environment isolation, signature policy and host drift checks remain in force.

CI and exact candidate provenance, fresh consumer/audit gates, actual spare interruption/resumption, Windows strict offline and independent human UI acceptance must be reviewed before qualification. Ubuntu strict offline remains deferred by the user. Candidate or CI success cannot retroactively qualify the original 0.11.0 interruption state. See the [correction plan](../../plans/20261027-nr06-marker-recovery-correction.md).