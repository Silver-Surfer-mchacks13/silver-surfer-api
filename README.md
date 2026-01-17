# dotnet-teamcity-template

.NET project template with a sample pre-configured Teamcity CI/CD pipeline using Kotlin (DSL).
This repository aims to provide a "good enough" template which can be expanded down the line to meet specific needs.

The project contains two main .NET applications: a [backend API](src/WebApi) and a [Blazor WASM frontend](src/WebFrontend).
These two applications are intended to be hosted as docker containers, with the whole application available to be locally orchestrated via [docker compose](docker-compose.yml).

> [!CAUTION]
> Although I've tried my best to include as much as possible to make this setup standalone, it still has some [prerequisites and assumptions](docs/prerequisites_and_assumptions.md).