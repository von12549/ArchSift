# template-naming

This is a read-only projection of JSON. Editing it does not change executable rules.

Source: naming.json; version: 1.0.0; SHA-256: 0addf281d2988ff731a899bcb8dd47278832cf64ff9bc6b69a34915b10c29602

naming template. Review selectors and policy reasons before use.

## naming-01

Type: naming; enabled: true; severity: warning

Scope: project / glob / \*\*; allowEmpty=false

Reason: Describe the intended architecture policy.

- subjectKind: project
- requiredName: {           "match": "glob",           "value": "Sample.\*"         }

## Exceptions

None.
