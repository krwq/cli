#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

. "$PSScriptRoot\..\_common.ps1"

# Restore and compile the test app
dotnet restore "$RepoRoot\test\E2E" --runtime "osx.10.10-x64" --runtime "ubuntu.14.04-x64" --runtime "win7-x64"
dotnet publish --framework dnxcore50 --runtime "$Rid" --output "$RepoRoot\artifacts\$Rid\e2etest" "$RepoRoot\test\E2E"

# Run the app and check the exit code
pushd "$RepoRoot\artifacts\$Rid\e2etest"
& "CoreRun.exe" "xunit.console.netcore.exe" "E2E.dll"
if ($LASTEXITCODE -ne 0) {
    throw "E2E Test Failure"
}
popd
