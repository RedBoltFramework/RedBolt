# External dependencies

When a RedBolt implementation change requires a SharpCpp fix, committing and
pushing that fix to SharpCpp's configured origin/master is authorized. Validate
the fix and inspect the remote and branch before pushing; do not force-push or
include unrelated changes. Then pin the exact validated commit in `SharpCpp.cmake`
in the RedBolt change. Do not select a newer, untested master commit merely
because it appeared meanwhile. Instruction-only edits do not trigger this
publication workflow. Other remote destinations require task authorization.
