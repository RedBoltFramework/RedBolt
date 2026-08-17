# External Dependencies

When a RedBolt change requires a fix in SharpCpp, commit and push the SharpCpp
fix first. Then update `SharpCpp.cmake` in the same RedBolt change so `GIT_TAG`
pins the latest commit on SharpCpp's `master` branch.
