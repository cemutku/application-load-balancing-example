FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
RUN apt-get update && apt-get install -y curl
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy projects
COPY ServiceA/ServiceA.csproj ServiceA/
COPY Common/Common.csproj Common/

# Restore
RUN dotnet restore ServiceA/ServiceA.csproj

# Copy source
COPY . .

WORKDIR /src/ServiceA
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ServiceA.dll"]
