# template-project-reference-allowlist

This is a read-only projection of JSON. Editing it does not change executable rules.

Source: project-reference-allowlist.json; version: 1.0.0; SHA-256: ecb9d5402a7e5a7937c84b282e3b66da7b915960f8f8210bf91c24069b6b373d

project-reference-allowlist template. Review selectors and policy reasons before use.

## project-reference-allowlist-01

Type: project-reference-allowlist; enabled: true; severity: warning

Scope: project / glob / \*\*; allowEmpty=false

Reason: Describe the intended architecture policy.

- source: {           "kind": "project",           "match": "glob",           "value": "src/Application/\*\*"         }
- allowedTargets: \[           {             "kind": "project",             "match": "glob",             "value": "src/Domain/\*\*"           }         \]

## Exceptions

None.
