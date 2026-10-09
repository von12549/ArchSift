# template-project-reference

This is a read-only projection of JSON. Editing it does not change executable rules.

Source: project-reference.json; version: 1.0.0; SHA-256: 6cf7406952bb65063820e57a791c18d29f90cfb20cfeaed2bd15a68e21d14410

project-reference template. Review selectors and policy reasons before use.

## project-reference-01

Type: project-reference; enabled: true; severity: warning

Scope: project / glob / \*\*; allowEmpty=false

Reason: Describe the intended architecture policy.

- source: {           "kind": "project",           "match": "glob",           "value": "src/Domain/\*\*"         }
- target: {           "kind": "project",           "match": "glob",           "value": "src/Infrastructure/\*\*"         }

## Exceptions

None.
