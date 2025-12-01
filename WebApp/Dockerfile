FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Common/Common.csproj Common/
COPY Messages/Messages.csproj Messages/
COPY Migrations/Migrations.csproj Migrations/
COPY Models/Models.csproj Models/
COPY WebApp/WebApp.csproj WebApp/

RUN dotnet restore "WebApp/WebApp.csproj"

COPY Common/ Common/
COPY Messages/ Messages/
COPY Migrations/ Migrations/
COPY Models/ Models/
COPY WebApp/ WebApp/

WORKDIR /src/WebApp
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "WebApp.dll"]