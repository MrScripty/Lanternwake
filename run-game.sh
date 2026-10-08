#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
project_root="$PWD"
export DOTNET_CLI_HOME="$project_root/.toolchain/dotnet-home"
export NUGET_PACKAGES="$project_root/.toolchain/nuget-packages"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export XDG_DATA_HOME="$project_root/.toolchain/user-data"
export XDG_CACHE_HOME="$project_root/.toolchain/cache"
export XDG_CONFIG_HOME="$project_root/.toolchain/config"
export GODOT_MONO="$project_root/.toolchain/Godot_v4.6.3-stable_mono_linux_x86_64/Godot_v4.6.3-stable_mono_linux.x86_64"
mkdir -p "$DOTNET_CLI_HOME" "$NUGET_PACKAGES" "$XDG_DATA_HOME" "$XDG_CACHE_HOME" "$XDG_CONFIG_HOME"
bash scripts/setup.sh
if [[ ! -x "$GODOT_MONO" ]]; then
  echo "Missing local Godot .NET engine: $GODOT_MONO" >&2
  exit 1
fi
dotnet build Lanternwake.csproj --nologo
case "${1:-}" in
  --verify)
    shift
    bash scripts/verify.sh "$@"
    ;;
  *)
    exec "$GODOT_MONO" --path "$project_root" "$@"
    ;;
esac
