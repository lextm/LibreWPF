#!/usr/bin/env bash
set -euo pipefail

if [[ $# -gt 1 ]]; then
  echo "Usage: $0 [--macos-fonts]" >&2
  exit 2
fi

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dotnet_command="${repo_root}/.dotnet/dotnet"
if [[ ! -x "${dotnet_command}" || -d "${dotnet_command}" ]]; then
  dotnet_command="$(command -v dotnet)"
fi
configuration="${CONFIGURATION:-Release}"
assembly="${repo_root}/artifacts/bin/PresentationCore.Tests/${configuration}/net10.0-windows/PresentationCore.Tests.dll"
# Bash 3.2 rejects an empty array under nounset; the class filter is always required.
test_filters=(--filter-class System.Windows.Media.CompositeFontCjkFallbackTests)
minimum_tests=4

case "${1:-}" in
  '') test_filters+=(--filter-method '*HongKongFallbackRetainsItsSpecificMap') ;;
  --macos-fonts)
    if [[ "$(uname -s)" != Darwin ]]; then
      echo "The physical CJK face gate requires the actual macOS font collection." >&2
      exit 2
    fi
    native_dir="${PROGPU_NATIVE_BUILD_DIR:?Stage the exact qualified ProGPU native package first.}"
    runtime_dir="${PROGPU_NATIVE_RUNTIME_DIR:?Stage the matching WebGPU dependency first.}"
    test -s "${native_dir}/libprogpu_native.dylib"
    test -s "${runtime_dir}/libwgpu_native.dylib"
    export DYLD_LIBRARY_PATH="${native_dir}:${runtime_dir}${DYLD_LIBRARY_PATH:+:${DYLD_LIBRARY_PATH}}"
    "${dotnet_command}" build \
      "${repo_root}/src/Microsoft.DotNet.Wpf/tests/UnitTests/PresentationCore.Tests/PresentationCore.Tests.csproj" \
      --configuration "${configuration}" -m:1 -p:UseSharedCompilation=false --verbosity minimal
    minimum_tests=36
    ;;
  *) echo "Usage: $0 [--macos-fonts]" >&2; exit 2 ;;
esac
if [[ ! -f "${assembly}" ]]; then
  echo "Build the original PresentationCore.Tests source project before checking CJK maps." >&2
  exit 1
fi

export LIBREWPF_TEST_MEDIA_BACKEND=Portable
exec "${dotnet_command}" "${assembly}" \
  "${test_filters[@]}" --minimum-expected-tests "${minimum_tests}" \
  --fail-skips on --timeout 60s --no-progress
