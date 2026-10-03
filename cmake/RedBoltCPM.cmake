function(redbolt_cpm_normalize_repository repository output)
  string(STRIP "${repository}" normalized)
  string(REGEX REPLACE "^git@([^:]+):" "\\1/" normalized "${normalized}")
  string(REGEX REPLACE "^[A-Za-z]+://([^/]+)/(.*)$" "\\1/\\2" normalized "${normalized}")
  string(REGEX REPLACE "^[^/]+@" "" normalized "${normalized}")
  string(REGEX REPLACE "\\.git$" "" normalized "${normalized}")
  string(TOLOWER "${normalized}" normalized)
  set(${output}
      "${normalized}"
      PARENT_SCOPE
  )
endfunction()

function(redbolt_cpm_get_git_commit repository ref output)
  execute_process(
    COMMAND ${GIT_EXECUTABLE} rev-parse --verify "${ref}^{commit}"
    RESULT_VARIABLE result
    OUTPUT_VARIABLE commit
    OUTPUT_STRIP_TRAILING_WHITESPACE
    ERROR_QUIET
    WORKING_DIRECTORY "${repository}"
  )
  if(result EQUAL 0)
    set(${output}
        "${commit}"
        PARENT_SCOPE
    )
  else()
    set(${output}
        ""
        PARENT_SCOPE
    )
  endif()
endfunction()

function(redbolt_cpm_find_checkout package_name repository output)
  if(NOT CPM_SOURCE_CACHE OR CPM_SOURCE_CACHE STREQUAL "OFF")
    set(${output}
        ""
        PARENT_SCOPE
    )
    return()
  endif()

  redbolt_cpm_normalize_repository("${repository}" expected_repository)
  set(candidate "${CPM_SOURCE_CACHE}/${package_name}")
  if(EXISTS "${candidate}/.git")
    execute_process(
      COMMAND ${GIT_EXECUTABLE} config --get remote.origin.url
      OUTPUT_VARIABLE candidate_repository
      OUTPUT_STRIP_TRAILING_WHITESPACE
      ERROR_QUIET
      WORKING_DIRECTORY "${candidate}"
    )
    redbolt_cpm_normalize_repository("${candidate_repository}" normalized_candidate_repository)
    if(normalized_candidate_repository STREQUAL expected_repository)
      set(${output}
          "${candidate}"
          PARENT_SCOPE
      )
      return()
    endif()
  endif()

  file(GLOB_RECURSE git_markers LIST_DIRECTORIES true "${CPM_SOURCE_CACHE}/*/.git")
  foreach(git_marker IN LISTS git_markers)
    get_filename_component(checkout "${git_marker}" DIRECTORY)
    execute_process(
      COMMAND ${GIT_EXECUTABLE} config --get remote.origin.url
      OUTPUT_VARIABLE checkout_repository
      OUTPUT_STRIP_TRAILING_WHITESPACE
      ERROR_QUIET
      WORKING_DIRECTORY "${checkout}"
    )
    if(checkout_repository)
      redbolt_cpm_normalize_repository("${checkout_repository}" normalized_checkout_repository)
      if(normalized_checkout_repository STREQUAL expected_repository)
        set(${output}
            "${checkout}"
            PARENT_SCOPE
        )
        return()
      endif()
    endif()
  endforeach()

  set(${output}
      ""
      PARENT_SCOPE
  )
endfunction()

function(redbolt_cpm_apply_patches repository)
  if(NOT ARGN)
    return()
  endif()

  find_program(PATCH_EXECUTABLE patch)
  if(NOT PATCH_EXECUTABLE)
    message(FATAL_ERROR "CPM: Could not find the patch executable.")
  endif()

  set(applied_patches)
  foreach(patch_file IN LISTS ARGN)
    get_filename_component(patch_file "${patch_file}" ABSOLUTE)
    execute_process(
      COMMAND ${PATCH_EXECUTABLE} --dry-run --batch --forward -p1 --input "${patch_file}"
      RESULT_VARIABLE patch_result
      OUTPUT_VARIABLE patch_output
      ERROR_VARIABLE patch_error
      OUTPUT_STRIP_TRAILING_WHITESPACE
      ERROR_STRIP_TRAILING_WHITESPACE
      WORKING_DIRECTORY "${repository}"
    )
    if(patch_result)
      list(REVERSE applied_patches)
      foreach(applied_patch IN LISTS applied_patches)
        execute_process(
          COMMAND ${PATCH_EXECUTABLE} --force --reverse --no-backup-if-mismatch -p1
                  --input "${applied_patch}"
          OUTPUT_QUIET
          ERROR_QUIET
          WORKING_DIRECTORY "${repository}"
        )
      endforeach()
      message(
        FATAL_ERROR
          "CPM: Patch '${patch_file}' is incompatible with the source revision at '${repository}': ${patch_error}${patch_output}"
      )
    endif()
    execute_process(
      COMMAND ${PATCH_EXECUTABLE} --batch --forward --no-backup-if-mismatch -p1
              --input "${patch_file}"
      RESULT_VARIABLE patch_result
      OUTPUT_VARIABLE patch_output
      ERROR_VARIABLE patch_error
      OUTPUT_STRIP_TRAILING_WHITESPACE
      ERROR_STRIP_TRAILING_WHITESPACE
      WORKING_DIRECTORY "${repository}"
    )
    if(patch_result)
      message(
        FATAL_ERROR
          "CPM: Could not apply preflighted patch '${patch_file}' to '${repository}': ${patch_error}${patch_output}"
      )
    endif()
    list(APPEND applied_patches "${patch_file}")
  endforeach()
endfunction()

function(redbolt_cpm_unapply_patches package_name repository)
  find_program(PATCH_EXECUTABLE patch)
  if(NOT PATCH_EXECUTABLE)
    message(FATAL_ERROR "CPM: Could not find the patch executable.")
  endif()

  set(reversed_patches ${ARGN})
  list(REVERSE reversed_patches)
  foreach(patch_file IN LISTS reversed_patches)
    get_filename_component(patch_file "${patch_file}" ABSOLUTE)
    execute_process(
      COMMAND ${PATCH_EXECUTABLE} --force --reverse --no-backup-if-mismatch -p1
              --input "${patch_file}"
      RESULT_VARIABLE patch_result
      OUTPUT_VARIABLE patch_output
      ERROR_VARIABLE patch_error
      OUTPUT_STRIP_TRAILING_WHITESPACE
      ERROR_STRIP_TRAILING_WHITESPACE
      WORKING_DIRECTORY "${repository}"
    )
    if(patch_result)
      message(
        FATAL_ERROR
          "CPM: Could not remove patch '${patch_file}' from ${package_name} at '${repository}' before updating it: ${patch_error}${patch_output}"
      )
    endif()
  endforeach()
endfunction()

function(redbolt_cpm_get_git_path repository path output)
  execute_process(
    COMMAND ${GIT_EXECUTABLE} rev-parse --git-path "${path}"
    RESULT_VARIABLE path_result
    OUTPUT_VARIABLE git_path
    OUTPUT_STRIP_TRAILING_WHITESPACE
    ERROR_VARIABLE path_error
    ERROR_STRIP_TRAILING_WHITESPACE
    WORKING_DIRECTORY "${repository}"
  )
  if(path_result)
    message(FATAL_ERROR "CPM: Could not resolve Git path for '${repository}': ${path_error}")
  endif()
  if(NOT IS_ABSOLUTE "${git_path}")
    get_filename_component(git_path "${git_path}" ABSOLUTE BASE_DIR "${repository}")
  endif()
  set(${output}
      "${git_path}"
      PARENT_SCOPE
  )
endfunction()

# Whether a text can be split into a list of lines without altering its entries.
function(redbolt_cpm_is_list_safe text output)
  set(safe TRUE)
  foreach(unsafe_character ";" "[" "]")
    string(FIND "${text}" "${unsafe_character}" unsafe_position)
    if(NOT unsafe_position EQUAL -1)
      set(safe FALSE)
    endif()
  endforeach()
  set(${output}
      ${safe}
      PARENT_SCOPE
  )
endfunction()

# Paths that differ from HEAD in the working tree of a repository, including its submodules (their
# paths are prefixed). Only reads. ok is FALSE for a state a patched checkout cannot be in: staged
# changes, renames, unmerged or quoted paths, a changed file mode or type, or a submodule at another
# commit. It is also FALSE when git fails or the output has characters unsafe in a list.
function(redbolt_cpm_list_changed_paths repository prefix output ok)
  set(${output}
      ""
      PARENT_SCOPE
  )
  set(${ok}
      FALSE
      PARENT_SCOPE
  )
  execute_process(
    COMMAND ${GIT_EXECUTABLE} --no-optional-locks status --porcelain=v2 --untracked-files=all
            --ignore-submodules=none
    RESULT_VARIABLE status_result
    OUTPUT_VARIABLE status
    OUTPUT_STRIP_TRAILING_WHITESPACE
    ERROR_QUIET
    WORKING_DIRECTORY "${repository}"
  )
  redbolt_cpm_is_list_safe("${status}" status_safe)
  if(status_result)
    message(WARNING "CPM: cannot validate '${repository}': git status failed")
    return()
  endif()
  if(NOT status_safe)
    message(WARNING "CPM: cannot validate '${repository}': git status output is not list safe")
    return()
  endif()

  set(changed)
  string(REPLACE "\n" ";" status_lines "${status}")
  foreach(line IN LISTS status_lines)
    if(line MATCHES "^# ")
      continue()
    endif()
    if(line MATCHES "^\\? (.+)$")
      set(path "${CMAKE_MATCH_1}")
      set(submodule "")
    elseif(line MATCHES "^1 (.)(.) (....) ([0-7]+) [0-7]+ ([0-7]+) [0-9a-f]+ [0-9a-f]+ (.+)$")
      set(staged "${CMAKE_MATCH_1}")
      set(unstaged "${CMAKE_MATCH_2}")
      set(submodule "${CMAKE_MATCH_3}")
      set(head_mode "${CMAKE_MATCH_4}")
      set(worktree_mode "${CMAKE_MATCH_5}")
      set(path "${CMAKE_MATCH_6}")
      if(NOT staged STREQUAL ".")
        return()
      endif()
      # Mode and type changes (chmod, symlinks, intent to add) are not what a patch leaves behind.
      if(NOT unstaged STREQUAL "D" AND NOT head_mode STREQUAL worktree_mode)
        return()
      endif()
    else()
      return()
    endif()
    if(path MATCHES "^\"")
      message(WARNING "CPM: cannot validate '${repository}': quoted path in git status")
      return()
    endif()

    if(submodule MATCHES "^S(.)")
      if(CMAKE_MATCH_1 STREQUAL "C" OR NOT EXISTS "${repository}/${path}/.git")
        return()
      endif()
      redbolt_cpm_list_changed_paths(
        "${repository}/${path}" "${prefix}${path}/" submodule_changed submodule_ok
      )
      if(NOT submodule_ok)
        return()
      endif()
      list(APPEND changed ${submodule_changed})
    else()
      list(APPEND changed "${prefix}${path}")
    endif()
  endforeach()

  set(${output}
      "${changed}"
      PARENT_SCOPE
  )
  set(${ok}
      TRUE
      PARENT_SCOPE
  )
endfunction()

# Compares the checkout with its pinned commit plus patches without touching it: the patched files
# are copied to work_dir, the patches are reversed there and the result must equal the committed
# files. The working tree may only differ from the commit in files the patches touch. Patches that
# rename files are not supported and never validate.
function(redbolt_cpm_checkout_compare_patched repository patches work_dir output)
  set(${output}
      FALSE
      PARENT_SCOPE
  )

  set(touched)
  foreach(patch_file IN LISTS patches)
    get_filename_component(patch_file "${patch_file}" ABSOLUTE)
    execute_process(
      COMMAND ${GIT_EXECUTABLE} apply --numstat "${patch_file}"
      RESULT_VARIABLE numstat_result
      OUTPUT_VARIABLE numstat
      OUTPUT_STRIP_TRAILING_WHITESPACE
      ERROR_QUIET
      WORKING_DIRECTORY "${repository}"
    )
    redbolt_cpm_is_list_safe("${numstat}" numstat_safe)
    if(numstat_result)
      message(WARNING "CPM: cannot validate '${repository}': git apply --numstat failed")
      return()
    endif()
    if(NOT numstat_safe)
      message(WARNING "CPM: cannot validate '${repository}': patch paths are not list safe")
      return()
    endif()
    string(REPLACE "\n" ";" numstat_lines "${numstat}")
    foreach(line IN LISTS numstat_lines)
      if(NOT line MATCHES "^[0-9-]+\t[0-9-]+\t(.+)$")
        return()
      endif()
      set(path "${CMAKE_MATCH_1}")
      if(path MATCHES "^\"")
        message(WARNING "CPM: cannot validate '${repository}': quoted path in patch")
        return()
      endif()
      list(APPEND touched "${path}")
    endforeach()
  endforeach()
  list(REMOVE_DUPLICATES touched)

  redbolt_cpm_list_changed_paths("${repository}" "" changed changed_ok)
  if(NOT changed_ok)
    return()
  endif()
  foreach(path IN LISTS changed)
    if(NOT path IN_LIST touched)
      return()
    endif()
  endforeach()

  foreach(path IN LISTS touched)
    if(EXISTS "${repository}/${path}" AND NOT IS_DIRECTORY "${repository}/${path}")
      get_filename_component(directory "${work_dir}/${path}" DIRECTORY)
      file(MAKE_DIRECTORY "${directory}")
      file(COPY_FILE "${repository}/${path}" "${work_dir}/${path}")
    endif()
  endforeach()

  set(reversed_patches ${patches})
  list(REVERSE reversed_patches)
  foreach(patch_file IN LISTS reversed_patches)
    get_filename_component(patch_file "${patch_file}" ABSOLUTE)
    execute_process(
      COMMAND ${PATCH_EXECUTABLE} --force --reverse --no-backup-if-mismatch -p1
              --input "${patch_file}"
      RESULT_VARIABLE patch_result
      OUTPUT_QUIET
      ERROR_QUIET
      WORKING_DIRECTORY "${work_dir}"
    )
    if(patch_result)
      return()
    endif()
  endforeach()

  foreach(path IN LISTS touched)
    # The repository that owns a file is the innermost one around it: ask git in its directory.
    get_filename_component(directory "${repository}/${path}" DIRECTORY)
    get_filename_component(owner_path "${path}" NAME)
    while(NOT IS_DIRECTORY "${directory}")
      get_filename_component(directory_name "${directory}" NAME)
      set(owner_path "${directory_name}/${owner_path}")
      get_filename_component(directory "${directory}" DIRECTORY)
    endwhile()
    execute_process(
      COMMAND ${GIT_EXECUTABLE} rev-parse --show-toplevel
      RESULT_VARIABLE owner_result
      OUTPUT_VARIABLE owner
      OUTPUT_STRIP_TRAILING_WHITESPACE
      ERROR_QUIET
      WORKING_DIRECTORY "${directory}"
    )
    execute_process(
      COMMAND ${GIT_EXECUTABLE} rev-parse --show-prefix
      RESULT_VARIABLE prefix_result
      OUTPUT_VARIABLE owner_prefix
      OUTPUT_STRIP_TRAILING_WHITESPACE
      ERROR_QUIET
      WORKING_DIRECTORY "${directory}"
    )
    if(owner_result OR prefix_result)
      message(WARNING "CPM: cannot validate '${repository}': cannot find the git owner of '${path}'")
      return()
    endif()
    set(owner_path "${owner_prefix}${owner_path}")

    execute_process(
      COMMAND ${GIT_EXECUTABLE} rev-parse --verify --quiet "HEAD:${owner_path}"
      RESULT_VARIABLE pristine_result
      OUTPUT_VARIABLE pristine_id
      OUTPUT_STRIP_TRAILING_WHITESPACE
      ERROR_QUIET
      WORKING_DIRECTORY "${owner}"
    )
    if(pristine_result EQUAL 0)
      set(pristine TRUE)
    elseif(pristine_result EQUAL 1)
      set(pristine FALSE)
    else()
      message(WARNING "CPM: cannot validate '${repository}': git rev-parse failed for '${path}'")
      return()
    endif()

    set(restored FALSE)
    if(EXISTS "${work_dir}/${path}")
      set(restored TRUE)
      execute_process(
        COMMAND ${GIT_EXECUTABLE} hash-object "--path=${owner_path}" "${work_dir}/${path}"
        RESULT_VARIABLE hash_result
        OUTPUT_VARIABLE restored_id
        OUTPUT_STRIP_TRAILING_WHITESPACE
        ERROR_QUIET
        WORKING_DIRECTORY "${owner}"
      )
      if(hash_result)
        message(WARNING "CPM: cannot validate '${repository}': git hash-object failed for '${path}'")
        return()
      endif()
    endif()

    if(pristine AND restored)
      if(NOT pristine_id STREQUAL restored_id)
        return()
      endif()
    elseif(pristine OR restored)
      return()
    endif()
  endforeach()

  set(${output}
      TRUE
      PARENT_SCOPE
  )
endfunction()

# Checks that a checkout equals its pinned commit plus the patches. Never writes below the
# repository: another platform may be compiling from it at the same moment.
function(redbolt_cpm_checkout_matches_patches repository patches output)
  find_program(PATCH_EXECUTABLE patch)
  if(NOT PATCH_EXECUTABLE)
    message(FATAL_ERROR "CPM: Could not find the patch executable.")
  endif()

  string(RANDOM LENGTH 12 unique)
  set(work_dir "${CMAKE_CURRENT_BINARY_DIR}/redbolt-cpm-validate/${unique}")
  file(MAKE_DIRECTORY "${work_dir}")
  redbolt_cpm_checkout_compare_patched("${repository}" "${patches}" "${work_dir}" matches)
  file(REMOVE_RECURSE "${work_dir}")

  set(${output}
      ${matches}
      PARENT_SCOPE
  )
endfunction()

# Fingerprint of a checkout's state: HEAD, the patch files and every change in the working tree,
# including submodules and untracked files. Only reads: it never modifies the working tree.
function(redbolt_cpm_checkout_fingerprint repository patches output)
  execute_process(
    COMMAND ${GIT_EXECUTABLE} rev-parse HEAD
    RESULT_VARIABLE head_result
    OUTPUT_VARIABLE head
    OUTPUT_STRIP_TRAILING_WHITESPACE
    ERROR_VARIABLE head_error
    ERROR_STRIP_TRAILING_WHITESPACE
    WORKING_DIRECTORY "${repository}"
  )
  if(head_result)
    message(FATAL_ERROR "CPM: Could not resolve HEAD of '${repository}': ${head_error}")
  endif()

  set(patch_hashes)
  foreach(patch_file IN LISTS patches)
    get_filename_component(patch_file "${patch_file}" ABSOLUTE)
    file(SHA256 "${patch_file}" patch_hash)
    list(APPEND patch_hashes "${patch_hash}")
  endforeach()

  execute_process(
    COMMAND ${GIT_EXECUTABLE} status --porcelain --ignore-submodules=none
    RESULT_VARIABLE status_result
    OUTPUT_VARIABLE status
    ERROR_VARIABLE status_error
    ERROR_STRIP_TRAILING_WHITESPACE
    WORKING_DIRECTORY "${repository}"
  )
  if(status_result)
    message(FATAL_ERROR "CPM: Could not fingerprint status of '${repository}': ${status_error}")
  endif()

  execute_process(
    COMMAND
      ${GIT_EXECUTABLE} -c diff.mnemonicPrefix=false -c diff.noprefix=false -c diff.algorithm=myers
      diff HEAD --submodule=diff --ignore-submodules=none --no-color --no-ext-diff --no-textconv
      --full-index
    RESULT_VARIABLE diff_result
    OUTPUT_VARIABLE diff
    ERROR_VARIABLE diff_error
    ERROR_STRIP_TRAILING_WHITESPACE
    WORKING_DIRECTORY "${repository}"
  )
  if(diff_result)
    message(FATAL_ERROR "CPM: Could not fingerprint changes of '${repository}': ${diff_error}")
  endif()

  execute_process(
    COMMAND ${GIT_EXECUTABLE} ls-files --others --exclude-standard
    RESULT_VARIABLE untracked_result
    OUTPUT_VARIABLE untracked
    ERROR_VARIABLE untracked_error
    ERROR_STRIP_TRAILING_WHITESPACE
    WORKING_DIRECTORY "${repository}"
  )
  if(untracked_result)
    message(FATAL_ERROR "CPM: Could not list untracked files of '${repository}': ${untracked_error}")
  endif()
  execute_process(
    COMMAND ${GIT_EXECUTABLE} ls-files --others --exclude-standard
    COMMAND ${GIT_EXECUTABLE} hash-object --stdin-paths
    RESULTS_VARIABLE untracked_results
    OUTPUT_VARIABLE untracked_hashes
    ERROR_VARIABLE untracked_hash_error
    ERROR_STRIP_TRAILING_WHITESPACE
    WORKING_DIRECTORY "${repository}"
  )
  # A failed pipeline (a nested repository or a dangling symlink) is hashed, not fatal: the stamp
  # then mismatches and the patch validation reports the real problem.
  set(state "${head}\n${patch_hashes}\n${status}\n${diff}\n${untracked}\n${untracked_hashes}")
  string(APPEND state "\n${untracked_results}\n${untracked_hash_error}")
  string(SHA256 fingerprint "${state}")
  set(${output}
      "${fingerprint}"
      PARENT_SCOPE
  )
endfunction()

# The patch validation reverses every patch on copies of the patched files and compares the result
# with the commit, so it does not write the working tree (the fingerprint's git status and git diff
# may refresh .git/index). A stamp of the validated state in the git
# directory lets a configure of an unchanged checkout skip it.
function(redbolt_cpm_checkout_verify_patches repository patches output)
  redbolt_cpm_get_git_path("${repository}" "redbolt-cpm-patched.stamp" stamp)
  redbolt_cpm_checkout_fingerprint("${repository}" "${patches}" fingerprint)
  if(EXISTS "${stamp}")
    file(READ "${stamp}" stamped_fingerprint)
    if(stamped_fingerprint STREQUAL fingerprint)
      set(${output}
          TRUE
          PARENT_SCOPE
      )
      return()
    endif()
  endif()

  redbolt_cpm_checkout_matches_patches("${repository}" "${patches}" matches)
  if(matches)
    redbolt_cpm_checkout_fingerprint("${repository}" "${patches}" fingerprint)
    file(WRITE "${stamp}" "${fingerprint}")
  else()
    file(REMOVE "${stamp}")
  endif()
  set(${output}
      ${matches}
      PARENT_SCOPE
  )
endfunction()

function(redbolt_cpm_checkout_contains_patches repository patches output)
  find_program(PATCH_EXECUTABLE patch)
  if(NOT PATCH_EXECUTABLE)
    message(FATAL_ERROR "CPM: Could not find the patch executable.")
  endif()

  set(contains TRUE)
  foreach(patch_file IN LISTS patches)
    get_filename_component(patch_file "${patch_file}" ABSOLUTE)
    execute_process(
      COMMAND ${PATCH_EXECUTABLE} --dry-run --force --reverse -p1 --input "${patch_file}"
      RESULT_VARIABLE patch_result
      OUTPUT_QUIET
      ERROR_QUIET
      WORKING_DIRECTORY "${repository}"
    )
    if(patch_result)
      set(contains FALSE)
      break()
    endif()
  endforeach()
  set(${output}
      ${contains}
      PARENT_SCOPE
  )
endfunction()

function(redbolt_cpm_update_submodules package_name checkout)
  execute_process(
    COMMAND ${GIT_EXECUTABLE} submodule sync --recursive
    RESULT_VARIABLE sync_result
    ERROR_VARIABLE sync_error
    ERROR_STRIP_TRAILING_WHITESPACE
    WORKING_DIRECTORY "${checkout}"
  )
  if(sync_result)
    message(FATAL_ERROR "CPM: Could not synchronize ${package_name} submodules: ${sync_error}")
  endif()
  execute_process(
    COMMAND ${GIT_EXECUTABLE} submodule update --init --recursive --force
    RESULT_VARIABLE update_result
    ERROR_VARIABLE update_error
    ERROR_STRIP_TRAILING_WHITESPACE
    WORKING_DIRECTORY "${checkout}"
  )
  if(update_result)
    message(FATAL_ERROR "CPM: Could not update ${package_name} submodules: ${update_error}")
  endif()
endfunction()

function(redbolt_cpm_update_checkout package_name repository git_tag checkout patches updated)
  set(${updated}
      FALSE
      PARENT_SCOPE
  )
  if(NOT checkout)
    return()
  endif()

  redbolt_cpm_get_git_path("${checkout}" "redbolt-cpm-prepare.lock" prepare_lock)
  file(LOCK "${prepare_lock}" GUARD FUNCTION TIMEOUT 300 RESULT_VARIABLE lock_result)
  if(lock_result)
    message(
      FATAL_ERROR
        "CPM: Could not lock ${package_name} checkout '${checkout}' for preparation: ${lock_result}"
    )
  endif()

  execute_process(
    COMMAND ${GIT_EXECUTABLE} status --porcelain
    RESULT_VARIABLE status_result
    OUTPUT_VARIABLE status
    OUTPUT_STRIP_TRAILING_WHITESPACE
    ERROR_VARIABLE status_error
    WORKING_DIRECTORY "${checkout}"
  )
  if(status_result)
    message(FATAL_ERROR "CPM: Could not inspect ${package_name} at '${checkout}': ${status_error}")
  endif()

  redbolt_cpm_get_git_commit("${checkout}" HEAD local_commit)
  redbolt_cpm_get_git_commit("${checkout}" "${git_tag}" pinned_commit)

  # A branch pin moves. The local ref only records the last fetch (the checkout's own branch always
  # equals HEAD), so ask the remote where the branch is now. Tags and commit hashes do not move
  # and are resolved locally. Without network access the existing checkout is used as is.
  # A pin is a commit hash only when it abbreviates the commit it resolves to: a branch may have
  # a name that is all hex digits. A full-length id is always an object id: it may name an annotated
  # tag object, which resolves to another commit.
  redbolt_cpm_get_git_commit("${checkout}" "refs/tags/${git_tag}" pinned_tag_commit)
  set(pinned_is_hash FALSE)
  string(LENGTH "${git_tag}" git_tag_length)
  if(git_tag MATCHES "^[0-9a-fA-F]+$" AND (git_tag_length EQUAL 40 OR git_tag_length EQUAL 64))
    set(pinned_is_hash TRUE)
  elseif(git_tag MATCHES "^[0-9a-fA-F]+$")
    string(TOLOWER "${git_tag}" git_tag_lower)
    string(FIND "${pinned_commit}" "${git_tag_lower}" hash_prefix)
    if(hash_prefix EQUAL 0)
      set(pinned_is_hash TRUE)
    endif()
  endif()
  if(local_commit AND pinned_commit AND NOT pinned_tag_commit AND NOT pinned_is_hash)
    execute_process(
      COMMAND ${GIT_EXECUTABLE} ls-remote --heads "${repository}" "refs/heads/${git_tag}"
      RESULT_VARIABLE remote_result
      OUTPUT_VARIABLE remote_heads
      OUTPUT_STRIP_TRAILING_WHITESPACE
      ERROR_VARIABLE remote_error
      ERROR_STRIP_TRAILING_WHITESPACE
      TIMEOUT 30
      WORKING_DIRECTORY "${checkout}"
    )
    if(remote_result)
      message(
        WARNING
          "CPM: Could not query ${package_name} branch '${git_tag}' at '${repository}'; using the existing checkout at ${local_commit}: ${remote_error}"
      )
      set(pinned_commit "${local_commit}")
    elseif(remote_heads MATCHES "^([0-9a-fA-F]+)[ \t]")
      set(pinned_commit "${CMAKE_MATCH_1}")
    else()
      message(
        WARNING
          "CPM: ${package_name} branch '${git_tag}' was not found at '${repository}'; using the existing checkout at ${local_commit}."
      )
      set(pinned_commit "${local_commit}")
    endif()
  endif()

  if(local_commit AND pinned_commit AND local_commit STREQUAL pinned_commit)
    if(patches)
      if(status)
        redbolt_cpm_checkout_verify_patches("${checkout}" "${patches}" patches_match)
        if(NOT patches_match)
          message(
            FATAL_ERROR
              "CPM: ${package_name} at '${checkout}' does not exactly match its configured patches; reset unexpected source changes before configuring."
          )
        endif()
        return()
      endif()
      redbolt_cpm_checkout_contains_patches("${checkout}" "${patches}" patches_present)
      if(patches_present)
        return()
      endif()
      redbolt_cpm_apply_patches("${checkout}" ${patches})
      redbolt_cpm_checkout_verify_patches("${checkout}" "${patches}" patches_match)
      if(NOT patches_match)
        message(FATAL_ERROR "CPM: ${package_name} at '${checkout}' did not reach the expected patched state.")
      endif()
    elseif(status)
      message(
        FATAL_ERROR
          "CPM: ${package_name} at '${checkout}' has unexpected source changes; reset them before configuring."
      )
    endif()
    return()
  endif()

  set(patches_match FALSE)
  if(status)
    if(patches)
      redbolt_cpm_checkout_verify_patches("${checkout}" "${patches}" patches_match)
    endif()
    if(NOT patches_match)
      message(
        FATAL_ERROR
          "CPM: Cannot update ${package_name} from '${local_commit}' to pinned ref '${git_tag}': '${checkout}' has uncommitted changes."
      )
    endif()
  endif()

  execute_process(
    COMMAND ${GIT_EXECUTABLE} fetch --force --tags "${repository}" "${git_tag}"
    RESULT_VARIABLE fetch_result
    ERROR_VARIABLE fetch_error
    OUTPUT_QUIET
    ERROR_STRIP_TRAILING_WHITESPACE
    WORKING_DIRECTORY "${checkout}"
  )
  if(fetch_result)
    message(
      FATAL_ERROR
        "CPM: Could not fetch ${package_name} ref '${git_tag}' from '${repository}': ${fetch_error}"
    )
  endif()

  redbolt_cpm_get_git_commit("${checkout}" FETCH_HEAD pinned_commit)
  if(NOT pinned_commit)
    redbolt_cpm_get_git_commit("${checkout}" "${git_tag}" pinned_commit)
  endif()
  if(NOT pinned_commit)
    message(FATAL_ERROR "CPM: Fetched ${package_name}, but could not resolve ref '${git_tag}'.")
  endif()

  # Files the patches created are untracked and would survive the checkout: take the patches out
  # first. The checkout rewrites the tree anyway.
  if(patches AND patches_match)
    redbolt_cpm_unapply_patches("${package_name}" "${checkout}" ${patches})
  endif()

  execute_process(
    COMMAND ${GIT_EXECUTABLE} checkout --detach --force "${pinned_commit}"
    RESULT_VARIABLE checkout_result
    ERROR_VARIABLE checkout_error
    OUTPUT_QUIET
    ERROR_STRIP_TRAILING_WHITESPACE
    WORKING_DIRECTORY "${checkout}"
  )
  if(checkout_result)
    message(
      FATAL_ERROR
        "CPM: Could not check out ${package_name} at pinned ref '${git_tag}': ${checkout_error}"
    )
  endif()

  redbolt_cpm_update_submodules("${package_name}" "${checkout}")
  if(patches)
    redbolt_cpm_checkout_contains_patches("${checkout}" "${patches}" patches_present)
    if(NOT patches_present)
      redbolt_cpm_apply_patches("${checkout}" ${patches})
      redbolt_cpm_checkout_verify_patches("${checkout}" "${patches}" patches_match)
      if(NOT patches_match)
        message(FATAL_ERROR "CPM: ${package_name} at '${checkout}' did not reach the expected patched state.")
      endif()
    endif()
  endif()
  message(STATUS "CPM: Updated ${package_name} from ${local_commit} to pinned ref ${git_tag}")
  set(${updated}
      TRUE
      PARENT_SCOPE
  )
endfunction()

# CPMAddPackage clones and patches a missing checkout itself: stamp that state right away.
function(redbolt_cpm_stamp_new_checkout package_name checkout patches)
  redbolt_cpm_get_git_path("${checkout}" "redbolt-cpm-prepare.lock" prepare_lock)
  file(LOCK "${prepare_lock}" GUARD FUNCTION TIMEOUT 300 RESULT_VARIABLE lock_result)
  if(lock_result)
    message(
      FATAL_ERROR
        "CPM: Could not lock ${package_name} checkout '${checkout}' for preparation: ${lock_result}"
    )
  endif()
  redbolt_cpm_checkout_verify_patches("${checkout}" "${patches}" patches_match)
  if(NOT patches_match)
    message(FATAL_ERROR "CPM: ${package_name} at '${checkout}' did not reach the expected patched state.")
  endif()
endfunction()

find_package(Git REQUIRED)

macro(redbolt_cpm_add_package)
  # The keyword lists of CPMAddPackage, so that PATCHES ends where CPM ends it.
  set(redbolt_cpm_one_value_args
      NAME FORCE VERSION GIT_TAG DOWNLOAD_ONLY GITHUB_REPOSITORY GITLAB_REPOSITORY
      BITBUCKET_REPOSITORY GIT_REPOSITORY SOURCE_DIR FIND_PACKAGE_ARGUMENTS NO_CACHE SYSTEM
      GIT_SHALLOW EXCLUDE_FROM_ALL SOURCE_SUBDIR CUSTOM_CACHE_KEY)
  set(redbolt_cpm_multi_value_args URL OPTIONS DOWNLOAD_COMMAND PATCHES)
  cmake_parse_arguments(REDBOLT_CPM "" "${redbolt_cpm_one_value_args}"
                        "${redbolt_cpm_multi_value_args}" ${ARGN})

  foreach(redbolt_cpm_patch IN LISTS REDBOLT_CPM_PATCHES)
    get_filename_component(redbolt_cpm_patch_path "${redbolt_cpm_patch}" REALPATH)
    if(NOT EXISTS "${redbolt_cpm_patch_path}" OR IS_DIRECTORY "${redbolt_cpm_patch_path}")
      message(FATAL_ERROR
        "CPM: ${REDBOLT_CPM_NAME} lists '${redbolt_cpm_patch}' under PATCHES, which is not a patch file. "
        "An argument that follows PATCHES and is not a CPMAddPackage keyword is read as one.")
    endif()
  endforeach()

  if(REDBOLT_CPM_GIT_REPOSITORY)
    set(redbolt_cpm_repository "${REDBOLT_CPM_GIT_REPOSITORY}")
  elseif(REDBOLT_CPM_GITHUB_REPOSITORY)
    set(redbolt_cpm_repository "https://github.com/${REDBOLT_CPM_GITHUB_REPOSITORY}.git")
  elseif(REDBOLT_CPM_GITLAB_REPOSITORY)
    set(redbolt_cpm_repository "https://gitlab.com/${REDBOLT_CPM_GITLAB_REPOSITORY}.git")
  elseif(REDBOLT_CPM_BITBUCKET_REPOSITORY)
    set(redbolt_cpm_repository "https://bitbucket.org/${REDBOLT_CPM_BITBUCKET_REPOSITORY}.git")
  else()
    set(redbolt_cpm_repository "")
  endif()

  set(redbolt_cpm_checkout "")
  set(redbolt_cpm_tracked FALSE)
  if(REDBOLT_CPM_NAME AND REDBOLT_CPM_GIT_TAG AND redbolt_cpm_repository)
    set(redbolt_cpm_tracked TRUE)
    redbolt_cpm_find_checkout("${REDBOLT_CPM_NAME}" "${redbolt_cpm_repository}" redbolt_cpm_checkout)
    redbolt_cpm_update_checkout(
      "${REDBOLT_CPM_NAME}"
      "${redbolt_cpm_repository}"
      "${REDBOLT_CPM_GIT_TAG}"
      "${redbolt_cpm_checkout}"
      "${REDBOLT_CPM_PATCHES}"
      redbolt_cpm_updated
    )
  endif()

  CPMAddPackage(${ARGN})

  if(redbolt_cpm_tracked AND NOT redbolt_cpm_checkout AND REDBOLT_CPM_PATCHES)
    redbolt_cpm_find_checkout("${REDBOLT_CPM_NAME}" "${redbolt_cpm_repository}" redbolt_cpm_checkout)
    if(redbolt_cpm_checkout)
      redbolt_cpm_stamp_new_checkout(
        "${REDBOLT_CPM_NAME}" "${redbolt_cpm_checkout}" "${REDBOLT_CPM_PATCHES}"
      )
    endif()
  endif()
endmacro()
