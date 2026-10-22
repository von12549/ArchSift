# FA05 candidate human UI acceptance and post-close audit

Historical status: **CURRENT CANDIDATE HUMAN UI ACCEPTED / POST-CLOSE AUDIT PASSED / ACTUAL GITHUB CANDIDATE PENDING / FA06 NOT AUTHORIZED**.

The user explicitly confirmed all 12 items passed, usability 4/5 and safe shutdown. All five UI repairs were included. No unsupported assistance rating was inferred, and the Agent neither performed nor supplied human answers. The preserved [raw acceptance JSON](20261017-fa05-human-ui-completion.json) is authoritative. Evidence: `D:/ArchSift-lab/runs/fa05-20261017-human-ui-01`; original checklist/start/end bytes remain unchanged.

| Item | User result |
| --- | --- |
| Identity and provenance | Pass |
| History control alignment, fix 1 | Pass |
| Policy sources English/Chinese localization, fix 2 | Pass |
| Full execution and top summary, fix 5 | Pass |
| Theory discovery text encoding, fix 3 | Pass |
| Subset summary/incomplete blocking, fix 5 | Pass |
| Case/Rule/Result sorting and reset, fix 4 | Pass |
| Cancellation, history and reports | Pass |
| Draft and approved-source protection | Pass |
| Restore approved configuration | Pass |
| Language, narrow layout and usability | Pass |
| Safe shutdown | Pass |

The user operated the session and rated overall usability **4/5**. Direct feedback supplied the result; automated browser QA was not substituted. The Agent initiated zero tests in this session.

## Session and actual jobs

Fixed candidate `fa05-ui-20261016-23a656b62463-r2` had 258 files and manifest SHA-256 `b3590b4b94e3f9e0d081607176bcf8fa89c662368340e3734c84f68466898c45`. A-tree mapping SHA-256 was `c319af1bbaca5a442fd67f8fe0401f0c384f05fb4355bf1df1f6275f67f6fa5c`; 92 required C# identities plus five B02 JSON cases totalled 97.

Start was Sydney 2026-10-17 00:43:32; normal close **2026-10-17 00:51:56 +11:00** (UTC 2026-10-16 13:51:56). Web PID 33444 and supervisor PID 32820 ended; Web exited 0 and port 1265 stopped listening.

| User-driven job | Actual result |
| --- | --- |
| Initial full run | 97 executed/passed, zero not-run; allow/0, complete coverage |
| Subset | One executed/passed, 96 not-run; block/4, incomplete |
| Cancellation | Cancelled, block/130; zero executed/passed |
| Two unapproved draft attempts | Approved-policy mismatch rejected before build, without a published report |
| Full run after approved configuration restored | 97 executed/passed, zero not-run; allow/0, complete coverage |

The supervisor recorded six user-driven jobs. Final review checked configuration, target, tool/mapping identity and actual reports. Both full runs' 97 execution IDs matched existing native/CLI/Web evidence for that candidate and retained the original 80. Discovery logs were strict UTF-8 without U+FFFD. Four JSON/HTML reports, two rejected draft configurations and all six job records were retained. Audit remained not-run and snapshot updates disabled; earlier independent audit was not written into these runs.

Key files under the evidence root: final-audit/human-feedback.json, final-audit/session-jobs.json, session-final.json and final-audit/process-and-port.json.

## Post-close checks and limits

Read-only final audit passed: zero related Web/supervisor/architecture processes and listeners; the FA05 mutex previously held by the supervisor was acquired. No new build, test, audit or IFX application entry point ran.

Original IFX remained clean with 20 original C# files, 16 references and 11 API snapshots unchanged. Both trees' 92 cases and 31 identity inputs, mapping/baseline/policy/project/solution/build/package policies remained equal. Current 258-file and two old 257-file candidates matched. Latest 268 machine-evidence files/4610 archive files, earlier 1063/295/46 stop records, initial lock/archive failures, old failed UI source/tests and human-session evidence were preserved.

Host hash before/after: `364ec6a885b3387d0b93f1bc14be64d1d6c85d0a3bcd331b0240503e7666efdb`. InProject stayed at `2877974fb38a12e856a6045ba223615c200841ee`; ArchSift HEAD stayed unchanged. This session indexed 166 evidence files with SHA-256. Old failures and expected rejections were retained, never relabeled successful.

Local machine acceptance and this independent human UI review completed. Actual GitHub candidate validation was still pending; no workflow/check was installed or triggered. FA05 overall remained incomplete. FA06, formal protection changes, commits, pushes and publication did not start. Supporting earlier narratives remain in the original evidence archive. Later retention handling follows exact receipts.
