#!/usr/bin/env bash
set -euo pipefail

# The preceding source gates build both original assemblies. No native window,
# package substitution, or second build is needed for these ownership contracts.
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dotnet_command="${PROGPU_WPF_POINTER_OWNERSHIP_DOTNET:-${repo_root}/.dotnet/dotnet}"
if [[ ! -x "${dotnet_command}" || -d "${dotnet_command}" ]]; then
  if [[ -n "${PROGPU_WPF_POINTER_OWNERSHIP_DOTNET+set}" ]]; then
    echo "PROGPU_WPF_POINTER_OWNERSHIP_DOTNET must name an executable file." >&2
    exit 2
  fi
  dotnet_command="$(command -v dotnet)"
fi
configuration="${CONFIGURATION:-Release}"
framework_assembly="${repo_root}/artifacts/bin/PresentationFramework.Tests/${configuration}/net10.0-windows/PresentationFramework.Tests.dll"
core_assembly="${repo_root}/artifacts/bin/PresentationCore.Tests/${configuration}/net10.0-windows/PresentationCore.Tests.dll"
if [[ ! -f "${framework_assembly}" || ! -f "${core_assembly}" ]]; then
  echo "Build the original source framework and core test assemblies before testing pointer ownership." >&2
  exit 1
fi
export LIBREWPF_TEST_MEDIA_BACKEND=Portable
"${dotnet_command}" "${framework_assembly}" \
  --filter-class System.Windows.PortableWindowActivationServiceTests \
  --filter-method '*PointerSource*' \
  --minimum-expected-tests 5 --fail-skips on --timeout 60s --no-progress
"${dotnet_command}" "${core_assembly}" \
  --filter-class System.Windows.Input.PortableInputOwnershipTests \
  --filter-method '*PointerSource*' \
  --minimum-expected-tests 3 --fail-skips on --timeout 60s --no-progress
"${dotnet_command}" "${core_assembly}" \
  --filter-class System.Windows.PortablePresentationSourceTests \
  --filter-method '*HostInputHitQuery*' \
  --minimum-expected-tests 3 --fail-skips on --timeout 60s --no-progress
