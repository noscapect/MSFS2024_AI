# Publishing a GitHub release

## Release-candidate package

Create the exact release-candidate package without creating a tag or publishing
a GitHub release:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\tools\Publish-Release.ps1 -PackageOnly
```

This command requires a clean `main` checkout, restores dependencies, builds
and tests the desktop application, builds and type-checks the EFB companion,
creates the release ZIP and its SHA-256 checksum, and prints both paths and the
hash. It does not require GitHub credentials, create or push a Git tag, or call
the GitHub release API.

## Final publish

Releases are created from the current `main` branch with the project application
closed (unrelated applications named `Copilot` do not block publishing):

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\tools\Publish-Release.ps1
```

The script:

1. Requires the checked-out branch to be `main`.
2. Reads the application version from `src/Copilot/Copilot.csproj`.
3. Requires a clean Git working tree.
4. Restores dependencies, then builds the Release application.
5. Runs the automated tests, including released-aircraft isolation contracts.
6. Builds and type-checks the MSFS 2024 EFB companion.
7. Packages the desktop application, aircraft fallback assets and their
   `AIRCRAFT_FALLBACK_IMAGE_ATTRIBUTION.md` attribution/license information,
   optional EFB Community package, and installation instructions.
8. Generates a SHA-256 checksum.
9. Fetches `origin/main` and tags, and requires local `HEAD` to match
   `origin/main`.
10. Verifies that the matching `vX.Y.Z` tag does not already exist, then
    creates and pushes it.
11. Creates the public GitHub release and uploads both assets.

GitHub authentication is read from the existing Windows Git credential for
`https://github.com`. The access token is never printed or written into the
repository.

The release ZIP includes the two Microsoft-classified distributable SimConnect
client libraries copied into the application output during the build:

- `Microsoft.FlightSimulator.SimConnect.dll`
- `SimConnect.dll`

Users do not need to install the full MSFS 2024 SDK to run a release because
the release ZIP includes the required SimConnect libraries. The MSFS 2024 SDK
is required on the development/release machine to build the desktop SimConnect
project and the EFB package.
