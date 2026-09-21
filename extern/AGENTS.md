# Core external dependencies

- Publish a SharpCpp fix to its configured `origin/master` only for a RedBolt
  implementation change that requires it.
- Validate the fix and inspect the remote and branch before pushing.
- Do not force-push or include unrelated changes.
- Pin the exact validated commit in RedBolt's `SharpCpp.cmake`.
- Do not replace it with a newer, untested master commit.
- Do not publish instruction-only edits through this workflow.
- Require explicit task authorization for other remote destinations.
