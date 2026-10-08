# template-nuget-denylist

This is a read-only projection of JSON. Editing it does not change executable rules.

Source: nuget-denylist.json; version: 1.0.0; SHA-256: 76aff393703211b79f472633ee5396dc8e1624bcc7c4dbc92b17a7007c776020

nuget-denylist template. Review selectors and policy reasons before use.

## nuget-denylist-01

Type: nuget-denylist; enabled: true; severity: warning

Scope: project / glob / \*\*; allowEmpty=false

Reason: Describe the intended architecture policy.

- forbiddenPackageIds: \[           "Forbidden.Package"         \]

## Exceptions

None.
