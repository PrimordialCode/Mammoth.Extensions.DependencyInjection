$ErrorActionPreference = 'Stop'
# Check native exit codes explicitly on Windows PowerShell 5.1 and PowerShell 7.
# Do not let an inherited PowerShell 7 preference replace the native exit code.
$PSNativeCommandUseErrorActionPreference = $false

& dotnet tool restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& dotnet restore ./src/Mammoth.Extensions.DependencyInjection.sln
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& dotnet tool run dotnet-gitversion /updateprojectfiles
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& dotnet build ./src/Mammoth.Extensions.DependencyInjection.sln --configuration Release --no-restore -p:ContinuousIntegrationBuild=True
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# This local script builds packages only. Run the test suite separately before use.
# CI runs all supported test targets before its independently gated pack/publish stages.
# See CONTRIBUTING.md for the test commands.

& dotnet pack ./src/Mammoth.Extensions.DependencyInjection.sln --configuration Release --no-build --output ./artifacts --include-symbols
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

exit 0
