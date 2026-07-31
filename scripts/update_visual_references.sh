#!/usr/bin/env bash

set -euo pipefail

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "$script_directory/.." && pwd)"
reference_root="${1:-$repository_root/src/RedBolt/Tests/VisualRegression/References}"

if [[ ! -d "$reference_root" ]]; then
    printf 'Reference directory does not exist: %s\n' "$reference_root" >&2
    exit 1
fi

updated=0
missing=0
while IFS= read -r -d '' shot_file; do
    reference_file="${shot_file%_shot.png}.png"
    if [[ ! -f "$reference_file" ]]; then
        printf 'Missing reference for shot: %s\n' "$shot_file" >&2
        missing=1
        continue
    fi

    cp -- "$shot_file" "$reference_file"
    printf 'Updated %s from %s\n' "$reference_file" "$shot_file"
    updated=$((updated + 1))
done < <(find "$reference_root" -type f -name '*_shot.png' -print0 | sort -z)

if (( updated == 0 && missing == 0 )); then
    printf 'No *_shot.png files found under %s\n' "$reference_root"
fi

exit "$missing"
