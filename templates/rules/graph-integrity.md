# template-graph-integrity

This is a read-only projection of JSON. Editing it does not change executable rules.

Source: graph-integrity.json; version: 1.0.0; SHA-256: 0b3a78bdd44c12ad9a590ae879b27098aab4499a20e8db25b72a6336b0197b8a

graph-integrity template. Review selectors and policy reasons before use.

## graph-integrity-01

Type: graph-integrity; enabled: true; severity: warning

Scope: project / glob / \*\*; allowEmpty=false

Reason: Describe the intended architecture policy.

- check: resolved-references

## Exceptions

None.
