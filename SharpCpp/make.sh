#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
env_file="${script_dir}/.env"

load_env() {
  if [[ -f "${env_file}" ]]; then
    set -a
    # shellcheck disable=SC1090
    . "${env_file}"
    set +a
  fi
}

require_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Missing required command: $1" >&2
    exit 1
  fi
}

usage() {
  cat <<'EOF'
Usage:
  ./make.sh notify [options] <message>
  ./make.sh help

Commands:
  notify        Send a Pushover notification.
  help          Show this help text.

Notify options:
  --title <title>          Override PUSHOVER_DEFAULT_TITLE.
  --device <device>        Override PUSHOVER_DEVICE.
  --priority <priority>    Pass a Pushover priority value.
  --sound <sound>          Pass a Pushover sound value.
  --url <url>              Attach a URL to the notification.
  --url-title <title>      Attach a title for --url.
  --dry-run                Print the resolved payload without sending.

Environment (.env):
  PUSHOVER_API_TOKEN       Required app API token.
  PUSHOVER_USER_KEY        Required user or group key.
  PUSHOVER_DEFAULT_TITLE   Optional default title.
  PUSHOVER_DEVICE          Optional default device.
  PUSHOVER_API_URL         Optional override for the API endpoint.
EOF
}

notify() {
  load_env
  require_command curl

  local title="${PUSHOVER_DEFAULT_TITLE:-SharpCpp}"
  local device="${PUSHOVER_DEVICE:-}"
  local priority=""
  local sound=""
  local url=""
  local url_title=""
  local dry_run=0
  local -a message_parts=()

  while (($# > 0)); do
    case "$1" in
      --title)
        [[ $# -ge 2 ]] || { echo "Missing value for --title" >&2; exit 1; }
        title="$2"
        shift 2
        ;;
      --device)
        [[ $# -ge 2 ]] || { echo "Missing value for --device" >&2; exit 1; }
        device="$2"
        shift 2
        ;;
      --priority)
        [[ $# -ge 2 ]] || { echo "Missing value for --priority" >&2; exit 1; }
        priority="$2"
        shift 2
        ;;
      --sound)
        [[ $# -ge 2 ]] || { echo "Missing value for --sound" >&2; exit 1; }
        sound="$2"
        shift 2
        ;;
      --url)
        [[ $# -ge 2 ]] || { echo "Missing value for --url" >&2; exit 1; }
        url="$2"
        shift 2
        ;;
      --url-title)
        [[ $# -ge 2 ]] || { echo "Missing value for --url-title" >&2; exit 1; }
        url_title="$2"
        shift 2
        ;;
      --dry-run)
        dry_run=1
        shift
        ;;
      --help|-h)
        usage
        exit 0
        ;;
      --)
        shift
        while (($# > 0)); do
          message_parts+=("$1")
          shift
        done
        ;;
      -*)
        echo "Unknown option: $1" >&2
        exit 1
        ;;
      *)
        message_parts+=("$1")
        shift
        ;;
    esac
  done

  local message="${message_parts[*]}"
  if [[ -z "${message}" ]]; then
    echo "Missing notification message." >&2
    usage >&2
    exit 1
  fi

  local api_url="${PUSHOVER_API_URL:-https://api.pushover.net/1/messages.json}"
  if [[ -z "${PUSHOVER_API_TOKEN:-}" ]]; then
    echo "Missing PUSHOVER_API_TOKEN. Set it in ${env_file} or your environment." >&2
    exit 1
  fi

  if [[ -z "${PUSHOVER_USER_KEY:-}" ]]; then
    echo "Missing PUSHOVER_USER_KEY. Set it in ${env_file} or your environment." >&2
    exit 1
  fi

  local -a curl_args=(
    --silent
    --show-error
    --fail-with-body
    -X POST
    "${api_url}"
    --data-urlencode "token=${PUSHOVER_API_TOKEN}"
    --data-urlencode "user=${PUSHOVER_USER_KEY}"
    --data-urlencode "message=${message}"
  )

  if [[ -n "${title}" ]]; then
    curl_args+=(--data-urlencode "title=${title}")
  fi

  if [[ -n "${device}" ]]; then
    curl_args+=(--data-urlencode "device=${device}")
  fi

  if [[ -n "${priority}" ]]; then
    curl_args+=(--data-urlencode "priority=${priority}")
  fi

  if [[ -n "${sound}" ]]; then
    curl_args+=(--data-urlencode "sound=${sound}")
  fi

  if [[ -n "${url}" ]]; then
    curl_args+=(--data-urlencode "url=${url}")
  fi

  if [[ -n "${url_title}" ]]; then
    curl_args+=(--data-urlencode "url_title=${url_title}")
  fi

  if [[ ${dry_run} -eq 1 ]]; then
    printf 'Pushover endpoint: %s\n' "${api_url}"
    printf 'Title: %s\n' "${title}"
    printf 'Device: %s\n' "${device:-<default>}"
    printf 'Priority: %s\n' "${priority:-<unset>}"
    printf 'Sound: %s\n' "${sound:-<unset>}"
    printf 'URL: %s\n' "${url:-<unset>}"
    printf 'URL title: %s\n' "${url_title:-<unset>}"
    printf 'Message: %s\n' "${message}"
    exit 0
  fi

  curl "${curl_args[@]}"
  printf '\nNotification sent.\n'
}

main() {
  if (($# == 0)); then
    usage
    exit 1
  fi

  local command="$1"
  shift

  case "${command}" in
    notify)
      notify "$@"
      ;;
    help|--help|-h)
      usage
      ;;
    *)
      echo "Unknown command: ${command}" >&2
      usage >&2
      exit 1
      ;;
  esac
}

main "$@"