FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
RUN apt-get update && apt-get install -y curl
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy projects
COPY ServiceB/ServiceB.csproj ServiceB/
COPY Common/Common.csproj Common/

# Restore
RUN dotnet restore ServiceB/ServiceB.csproj

# Copy source
COPY . .

WORKDIR /src/ServiceB
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ServiceB.dll"]
