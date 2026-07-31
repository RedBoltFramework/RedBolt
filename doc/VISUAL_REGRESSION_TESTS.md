# Visual regression tests

The prototype visual test is opt-in because it needs a desktop graphics
context. It renders three static scenes into a hidden 96x64 OpenGL window,
downsamples the screenshot to 48x32, and compares the result with the checked-in
PPM reference using channel and pixel-ratio tolerances.

Run it on a desktop session with:

```bash
REDBOLT_RUN_VISUAL_TESTS=1 \
  dotnet test src/RedBolt/Tests/RedBolt.Tests.csproj --configuration Debug \
  --filter FullyQualifiedName~VisualRegressionTests
```

To intentionally refresh the reference, point the test at the source reference
directory and enable update mode:

```bash
REDBOLT_RUN_VISUAL_TESTS=1 \
REDBOLT_UPDATE_VISUAL_REFERENCES=1 \
REDBOLT_VISUAL_REFERENCE_ROOT="$PWD/src/RedBolt/Tests/VisualRegression/References" \
  dotnet test src/RedBolt/Tests/RedBolt.Tests.csproj --configuration Debug \
  --filter FullyQualifiedName~Static_scenes_capture_low_resolution_reference
```

On mismatch, the low-resolution actual capture and an amplified difference PNG
are written to `/tmp/RedBoltVisualRegression` by default. Set
`REDBOLT_VISUAL_ARTIFACTS` to use another directory.
