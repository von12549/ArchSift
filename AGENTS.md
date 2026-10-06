# ArchSift project instructions

## Plan and scope

- Maintain implementation status and scope in `docs/plans/20261006-archsift-implementation-plan.md`. Read that plan and the current acceptance records before implementation.
- ArchSift is an independent .NET architecture and dependency analyzer. Follow the frozen Q01–Q14 decisions and W00–W10 dependencies. Keep deferred features deferred unless the user changes the plan.
- The 0.1.0 target is SDK-style C# net8/net9/net10, a .NET 10 tool, Windows x64 CLI and loopback Web UI, six rule types, and JSON/HTML reports. A real ArchUnitNET type-dependency check is required.
- CLI/Web call the same Core service and Contracts. Core must not depend on CLI/Web, Guard installation, `V4_STAGE_INPUT_JSON`, Profile/Stage, onboarding, trust or promotion.
- User requests authorize the corresponding work package; completion of a prerequisite alone does not authorize starting a later package, committing, pushing or publishing.

## Commit attribution

- Every commit actually created by an AI coding agent must include `Co-Authored-By` attribution for the agent that contributed the changes. This applies to documentation, configuration, tests and product code commits.
- For Codex contributions, use this exact Git commit trailer at the end of the commit message:

  ```text
  Co-Authored-By: Codex <noreply@openai.com>
  ```

- Leave a blank line between the commit message body and the trailer block. If other agents actually contributed, add their own `Co-Authored-By` trailers using their real, explicitly established identities; do not invent contributors or identities.
- `Signed-off-by` does not replace `Co-Authored-By`. This rule does not authorize creating a commit, pushing, or rewriting existing history.
- Keep the current human Git author as the primary author unless the user explicitly requests a change. Append agent attribution as trailers; do not change the primary author identity to implement co-authorship. Do not amend or rewrite existing commits solely to add attribution.

## Source and migration

- Treat analyzed target repositories, Guard and IFX source checkouts as read-only. Preserve user changes. ArchSift product development files may be edited for an authorized implementation task.
- Guard owner reuse/distribution authorization is recorded in `docs/migration/source-inventory.json`. Before copying or adapting a selected asset, verify its source commit and SHA-256, restrict changes to the listed scope, and record the actual target paths/hashes and new tests in the inventory.
- Preserve third-party license and attribution text from `docs/migration/third-party-notices.md`. The Guard owner authorization does not cover third-party library rights or assign a public license to ArchSift.
- Rebuild fixture behavior for ArchSift's contracts. Guard fixture metadata and P05's technical 8/8 cannot serve as ArchSift correctness evidence. Retain P05's overall FAIL and safety/cleanup history.

## Host safety and execution

- Never modify persisted User/Machine environment variables, registry environment values, PowerShell Profiles or shell startup files. Never use `setx`, automatic PATH repair or automatic persistent environment repair.
- Run PowerShell task commands without loading Profiles (`pwsh -NoProfile`; tool shell `login: false`). Do not modify a Profile to fix task shell errors.
- When setting an isolated child `DOTNET_CLI_HOME`, also set child `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0`. Use child-only environment settings and argument arrays; do not alter the parent environment.
- Reports, real target build snapshots and real integration artifacts belong outside source roots, normally `D:\ArchSift-lab\runs`, `fixtures`, `packages`, `evidence` and `archives`. Use normal approval mechanisms for paths outside permitted writable roots. ArchSift's own development bin/obj may be generated and ignored inside the repository.
- Do not execute target application entry points. An isolated MSBuild snapshot is not a full sandbox; custom targets may execute commands. Restore defaults to offline and explicit sources; do not silently enable network or ignore restore failures.
- Capture a safe environment/Profile hash baseline before .NET build/install/real integration operations, compare at relevant checkpoints and completion, and print only hashes/equality, never raw environment values. Stop and preserve evidence on persistent drift; do not repair it automatically.
- Guard/IFX real integration remains operator-driven: provide one reviewed command block, wait for the operator's result, then proceed. Read-only metadata checks and ArchSift synthetic tests do not authorize running Guard installers or real IFX integration.
- Clean only precisely recorded tool artifacts after checking their absolute path and containment. Never recursively delete a repository/source root or broad directory. Prefer recoverable cleanup and the app's worktree lifecycle tools when appropriate.

## Verification and reporting

- Execute the checks appropriate to the authorized package and record real results, SDK/TFM/configuration, input identity, coverage and limitations. Cache existence or a prior report does not prove restore/build or current-source conformance.
- Keep pass, violation, inconclusive, not-applicable and error distinct. Zero source matches default to inconclusive; unbound assemblies cannot prove current-source compliance. Failed/cancelled runs must not display an old success as current.
- Machine contracts use stable English fields; Chinese readable documentation/UI is preferred. JSON is the executable rule/report authority and Markdown/HTML are projections.
- No subagents are required by this project; use them only when explicitly requested by the user or applicable instructions.
