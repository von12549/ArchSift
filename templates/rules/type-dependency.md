# template-type-dependency

This is a read-only projection of JSON. Editing it does not change executable rules.

Source: type-dependency.json; version: 1.0.0; SHA-256: 1e95548b867bc818b4b077141cd93db2b947273a2b0b854b2890f9540c0f2032

type-dependency template. Review selectors and policy reasons before use.

## type-dependency-01

Type: type-dependency; enabled: true; severity: warning

Scope: namespace / glob / \*; allowEmpty=false

Reason: Describe the intended architecture policy.

- source: {           "kind": "namespace",           "match": "glob",           "value": "Sample.Domain\*"         }
- forbiddenTarget: {           "kind": "namespace",           "match": "glob",           "value": "Sample.Infrastructure\*"         }

## Exceptions

None.
