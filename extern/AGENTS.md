# Core external dependencies

- SharpCpp is an owned dependency. Make source changes directly in
  `extern/SharpCpp`; do not carry owned SharpCpp changes as local CPM patches.
- For a RedBolt change that requires a SharpCpp fix, validate the fix, inspect
  the current branch and `origin/master`, then publish it to `origin/master`
  when the task authorizes publication.
- Do not force-push or include unrelated changes. If `origin/master` moved,
  reconcile and revalidate rather than overwriting it.
- After publishing, update the exact `GIT_TAG` commit in `extern/SharpCpp.cmake`
  to the validated commit. Never pin an unpushed commit or a mutable branch.
- Do not replace the pin with a newer, untested master commit.
- Do not publish instruction-only edits through this workflow.
- Require explicit task authorization for other remote destinations.
