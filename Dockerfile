# CI container: builds the cross-platform game engine, runs the startup/crash test suite,
# and renders the 4K screenshots. (The MAUI app itself is built per-platform: Android on Linux/Windows, .exe on Windows.)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet test tests/ArcadeMania.Tests -c Release --logger "console;verbosity=minimal"
RUN dotnet run --project tools/ArcadeMania.Shots -c Release -- /out/screenshots

FROM scratch AS screenshots
COPY --from=build /out/screenshots /screenshots
