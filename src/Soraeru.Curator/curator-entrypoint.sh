#!/bin/sh
set -eu

keys_path="${DataProtection__KeysPath:-}"

if [ -n "$keys_path" ]; then
    case "$keys_path" in
        /*) ;;
        *)
            echo "DataProtection__KeysPath must be an absolute path." >&2
            exit 1
            ;;
    esac

    if [ "$keys_path" = "/" ]; then
        echo "DataProtection__KeysPath must not be the filesystem root." >&2
        exit 1
    fi

    mkdir -p -- "$keys_path"
    resolved_keys_path="$(readlink -f -- "$keys_path")"

    if [ -z "$resolved_keys_path" ] || [ "$resolved_keys_path" = "/" ]; then
        echo "DataProtection__KeysPath resolves to an unsafe path." >&2
        exit 1
    fi

    chown app:app -- "$resolved_keys_path"
fi

exec gosu app "$@"
