FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["Consumer/Consumer.csproj", "Consumer/"]
COPY ["WebApp/WebApp.csproj", "WebApp/"]
COPY ["Common/Common.csproj", "Common/"]
COPY ["Messages/Messages.csproj", "Messages/"]
COPY ["Models/Models.csproj", "Models/"]

RUN dotnet restore "Consumer/Consumer.csproj"

COPY . .
WORKDIR /src/Consumer
RUN dotnet publish "Consumer.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Consumer.dll"]

