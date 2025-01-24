& dotnet tool restore

& dotnet restore ./src/Mammoth.Extensions.DependencyInjection.sln

& dotnet tool run dotnet-gitversion /updateprojectfiles

& dotnet build ./src/Mammoth.Extensions.DependencyInjection.sln --configuration Release --no-restore -p:ContinuousIntegrationBuild=True

# & dotnet test ./src/Mammoth.Extensions.DependencyInjection.sln --configuration Release --no-build --verbosity normal

& dotnet pack ./src/Mammoth.Extensions.DependencyInjection.sln --configuration Release --no-build --output ./artifacts --include-symbols