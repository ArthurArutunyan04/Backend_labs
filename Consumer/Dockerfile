FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Common/Common.csproj Common/
COPY Messages/Messages.csproj Messages/
COPY Models/Models.csproj Models/
COPY Migrations/Migrations.csproj Migrations/
COPY Consumer/Consumer.csproj Consumer/

RUN dotnet restore "Consumer/Consumer.csproj"

COPY Common/ Common/
COPY Messages/ Messages/
COPY Models/ Models/
COPY Migrations/ Migrations/
COPY Consumer/ Consumer/

WORKDIR /src/Consumer
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Consumer.dll"]