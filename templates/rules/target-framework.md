# template-target-framework

This is a read-only projection of JSON. Editing it does not change executable rules.

Source: target-framework.json; version: 1.0.0; SHA-256: 9e0c29b4f5f8a3eac0d481074d3ab1255d3d9e6a23fef1eb38c47f8812cf36c0

target-framework template. Review selectors and policy reasons before use.

## target-framework-01

Type: target-framework; enabled: true; severity: warning

Scope: project / glob / \*\*; allowEmpty=false

Reason: Describe the intended architecture policy.

- allowedFrameworks: \[           "net8.0",           "net9.0",           "net10.0"         \]

## Exceptions

None.
