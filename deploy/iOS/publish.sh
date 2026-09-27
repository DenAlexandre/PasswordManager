#!/bin/bash
# Publishes a signed iOS .ipa archive. MUST be run on macOS with Xcode installed - .NET MAUI
# cannot build/sign iOS apps on Windows or Linux. See README.md for prerequisites.
set -euo pipefail

CONFIGURATION="${1:-Release}"
CODESIGN_KEY="${2:?Usage: publish.sh [Configuration] <CodesignKey> <ProvisioningProfileName>}"
PROVISIONING_PROFILE="${3:?Usage: publish.sh [Configuration] <CodesignKey> <ProvisioningProfileName>}"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CSPROJ="$REPO_ROOT/src/PasswordManager.Maui/PasswordManager.Maui.csproj"
OUT_DIR="$(dirname "${BASH_SOURCE[0]}")/output"

dotnet publish "$CSPROJ" \
    -f net10.0-ios \
    -c "$CONFIGURATION" \
    -p:ArchiveOnBuild=true \
    -p:CodesignKey="$CODESIGN_KEY" \
    -p:CodesignProvision="$PROVISIONING_PROFILE" \
    -o "$OUT_DIR"

echo ""
echo "Published to: $OUT_DIR"
echo "Look for the .ipa archive to distribute via TestFlight or ad-hoc install."
