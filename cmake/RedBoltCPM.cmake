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

function(redbolt_cpm_checkout_matches_patches repository patches output)
  find_program(PATCH_EXECUTABLE patch)
  if(NOT PATCH_EXECUTABLE)
    message(FATAL_ERROR "CPM: Could not find the patch executable.")
  endif()

  set(reversed_patches ${patches})
  list(REVERSE reversed_patches)
  set(removed_patches)
  set(matches TRUE)
  foreach(patch_file IN LISTS reversed_patches)
    get_filename_component(patch_file "${patch_file}" ABSOLUTE)
    execute_process(
      COMMAND ${PATCH_EXECUTABLE} --dry-run --force --reverse -p1 --input "${patch_file}"
      RESULT_VARIABLE patch_result
      OUTPUT_QUIET
      ERROR_QUIET
      WORKING_DIRECTORY "${repository}"
    )
    if(patch_result)
      set(matches FALSE)
      break()
    endif()
    execute_process(
      COMMAND ${PATCH_EXECUTABLE} --force --reverse --no-backup-if-mismatch -p1
              --input "${patch_file}"
      RESULT_VARIABLE patch_result
      OUTPUT_VARIABLE reverse_output
      ERROR_VARIABLE reverse_error
      OUTPUT_STRIP_TRAILING_WHITESPACE
      ERROR_STRIP_TRAILING_WHITESPACE
      WORKING_DIRECTORY "${repository}"
    )
    if(patch_result)
      message(FATAL_ERROR "CPM: Could not validate patched state at '${repository}': ${reverse_error}${reverse_output}")
    endif()
    list(APPEND removed_patches "${patch_file}")
  endforeach()

  if(matches)
    execute_process(
      COMMAND ${GIT_EXECUTABLE} status --porcelain --ignore-submodules=none
      RESULT_VARIABLE status_result
      OUTPUT_VARIABLE status
      OUTPUT_STRIP_TRAILING_WHITESPACE
      ERROR_VARIABLE status_error
      ERROR_STRIP_TRAILING_WHITESPACE
      WORKING_DIRECTORY "${repository}"
    )
    if(status_result)
      message(FATAL_ERROR "CPM: Could not compare patched state at '${repository}': ${status_error}")
    endif()
    if(status)
      set(matches FALSE)
    endif()
  endif()

  list(REVERSE removed_patches)
  foreach(patch_file IN LISTS removed_patches)
    execute_process(
      COMMAND ${PATCH_EXECUTABLE} --force --no-backup-if-mismatch -p1
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
          "CPM: Could not restore patched state at '${repository}' after validation: ${patch_error}${patch_output}"
      )
    endif()
  endforeach()

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
        redbolt_cpm_checkout_matches_patches("${checkout}" "${patches}" patches_match)
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
      redbolt_cpm_checkout_matches_patches("${checkout}" "${patches}" patches_match)
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

  if(status)
    set(patches_match FALSE)
    if(patches)
      redbolt_cpm_checkout_matches_patches("${checkout}" "${patches}" patches_match)
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
    endif()
  endif()
  message(STATUS "CPM: Updated ${package_name} from ${local_commit} to pinned ref ${git_tag}")
  set(${updated}
      TRUE
      PARENT_SCOPE
  )
endfunction()

find_package(Git REQUIRED)

macro(redbolt_cpm_add_package)
  set(redbolt_cpm_one_value_args
      NAME GIT_TAG GIT_REPOSITORY GITHUB_REPOSITORY GITLAB_REPOSITORY BITBUCKET_REPOSITORY
      DOWNLOAD_ONLY EXCLUDE_FROM_ALL)
  set(redbolt_cpm_multi_value_args PATCHES)
  cmake_parse_arguments(REDBOLT_CPM "" "${redbolt_cpm_one_value_args}"
                        "${redbolt_cpm_multi_value_args}" ${ARGN})

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

  if(REDBOLT_CPM_NAME AND REDBOLT_CPM_GIT_TAG AND redbolt_cpm_repository)
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
endmacro()
