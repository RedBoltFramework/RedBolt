# TODO

## Build system

- Serialize CMake build invocations across the MSBuild graph so only one CMake build job runs at a time, while keeping that job's internal parallel count tied to `MSBuildNodeCount` (`-m`).
- Verify that the build dependency graph is correct: targets must not build multiple times, different targets must not write the same output files, and incremental builds must remain fast and reliable.
