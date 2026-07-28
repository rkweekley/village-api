# ── Build stage ──
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy project files — layer so restore is cached
COPY src/Village.Domain/Village.Domain.csproj src/Village.Domain/
COPY src/Village.Shared/Village.Shared.csproj src/Village.Shared/
COPY src/Village.Application/Village.Application.csproj src/Village.Application/
COPY src/Village.Infrastructure/Village.Infrastructure.csproj src/Village.Infrastructure/
COPY src/Village.Api/Village.Api.csproj src/Village.Api/
COPY src/Village.Workers/Village.Workers.csproj src/Village.Workers/

RUN dotnet restore src/Village.Api/Village.Api.csproj

# Copy all source and publish the API project
COPY src/ src/
RUN dotnet publish src/Village.Api/Village.Api.csproj -c Release -o /app/publish

# ── Runtime image ──
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Village.Api.dll"]
