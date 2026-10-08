# Declared discovery, build inputs and read-only boundaries

Project discovery reads XML/solution declarations without MSBuild or target application execution. Supported inputs are SDK-style C# csproj, sln and slnx under TargetRoot. Solution members and unlisted projects are reported separately; external references are not recursively analyzed and unresolved edges are not invented as internal projects.

Entry is a root-relative solution/project path. All paths reject links/reparse points; output and target cannot contain each other. Project IDs are relative csproj paths; case-colliding identities reject. A project-only entry does not prove solution membership.

Frameworks come from unconditional literal declarations or nearest root-local Directory.Build.props. Conditions, expressions, imports, ambiguity, missing/unsupported TFMs remain limitations. Multi-TFM inputs require an explicit selection; results never imply other frameworks passed. Package checks read direct PackageReference IDs and resolvable nearest Directory.Packages.props literal versions; conditional/imported version facts are not fully evaluated.

InputCapture records relative path, length and SHA-256 in stable order. Ordinary .git/.vs/.idea/bin/obj/artifacts/.archsift directories are excluded. Explicit Compile/Content/None/EmbeddedResource/AdditionalFiles/Analyzer items are separately included, including referenced generated files. Root-external, conditional, expression-based and missing inputs remain limitations.

Relevant source edits/additions invalidate the snapshot; IDE and unrelated generated files do not. XML disables DTD and external entity resolution. A 100000-file ceiling rejects rather than silently truncating.

Assembly evidence records SDK/TFM/configuration, source/build inputs, DLL hashes and provenance. Worker validation checks assembly version/token closure and distinguishes source-bound from assemblies-only. A DLL's presence alone never proves current-source compliance. Release optimization can remove type dependencies and remains a limitation.

Isolated mode creates an external snapshot and runs a supported standard SDK build. Restore defaults offline with explicit sources. Unreviewed custom tasks/imports/output paths/package build scripts/generators are rejected. Full MSBuild input tracing remains deferred; an isolated build is not a complete sandbox.

Chains capture source/project facts once, freeze ruleset bytes, share captured build/assembly inputs, and record one build-input identity. Hash checks before/after children detect source or build-evidence drift. Drift stops remaining entries and preserves affected/skipped statuses instead of mixing source versions. Source identity, build identity and rule-file identities remain distinct.

The tool never repairs persistent PATH, environment or PowerShell Profiles. Each controlled dotnet child uses isolated DOTNET_CLI_HOME with DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0. Real IFX integration follows reviewed operator steps and external evidence directories.
