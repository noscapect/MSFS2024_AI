# Publishing a GitHub release

Releases are created from the current `main` branch with:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\tools\Publish-Release.ps1
```

The script:

1. Reads the application version from `src/Copilot/Copilot.csproj`.
2. Requires a clean Git working tree.
3. Builds the Release application.
4. Runs the automated tests.
5. Packages the application and installation instructions.
6. Generates a SHA-256 checksum.
7. Creates and pushes the matching `vX.Y.Z` Git tag.
8. Creates the public GitHub release and uploads both assets.

GitHub authentication is read from the existing Windows Git credential for
`https://github.com`. The access token is never printed or written into the
repository.

The release ZIP includes the two Microsoft-classified distributable SimConnect
client libraries copied into the application output during the build:

- `Microsoft.FlightSimulator.SimConnect.dll`
- `SimConnect.dll`

Users do not need to install the full MSFS 2024 SDK to run a release.
