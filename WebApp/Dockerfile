FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["WebApp/WebApp.csproj", "WebApp/"]
COPY ["Common/Common.csproj", "Common/"]
COPY ["Messages/Messages.csproj", "Messages/"]
COPY ["Models/Models.csproj", "Models/"]
COPY ["Migrations/Migrations.csproj", "Migrations/"]

RUN dotnet restore "WebApp/WebApp.csproj"

COPY . .
WORKDIR /src/WebApp
RUN dotnet publish "WebApp.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "WebApp.dll"]

