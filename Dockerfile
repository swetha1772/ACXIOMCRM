# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files and restore dependencies first (layer caching)
COPY ["AcxiomCRM/AcxiomCRM.csproj", "AcxiomCRM/"]
COPY ["AcxiomCRM.Tests/AcxiomCRM.Tests.csproj", "AcxiomCRM.Tests/"]
COPY ["AcxiomCRM.sln", "./"]
RUN dotnet restore "AcxiomCRM/AcxiomCRM.csproj"

# Copy complete repository and publish
COPY . .
WORKDIR "/src/AcxiomCRM"
RUN dotnet publish "AcxiomCRM.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Production Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

RUN mkdir -p /app/.keys

ENTRYPOINT ["dotnet", "AcxiomCRM.dll"]
