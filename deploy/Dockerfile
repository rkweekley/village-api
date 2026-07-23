FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

COPY Village.sln ./
COPY src/Village.Api/Village.Api.csproj src/Village.Api/
COPY src/Village.Domain/Village.Domain.csproj src/Village.Domain/
COPY src/Village.Application/Village.Application.csproj src/Village.Application/
COPY src/Village.Infrastructure/Village.Infrastructure.csproj src/Village.Infrastructure/
COPY src/Village.Shared/Village.Shared.csproj src/Village.Shared/
COPY src/Village.Workers/Village.Workers.csproj src/Village.Workers/
RUN dotnet restore

COPY . .
RUN dotnet publish src/Village.Api -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

ENTRYPOINT ["dotnet", "Village.Api.dll"]
