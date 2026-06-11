FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Learn2Code.Core/Learn2Code.Core.csproj Learn2Code.Core/
COPY Learn2Code.Infrastructure/Learn2Code.Infrastructure.csproj Learn2Code.Infrastructure/
COPY Learn2Code.Services/Learn2Code.Services.csproj Learn2Code.Services/
COPY Learn2Code.Api/Learn2Code.Api.csproj Learn2Code.Api/
RUN dotnet restore Learn2Code.Api/Learn2Code.Api.csproj

COPY . .
RUN dotnet publish Learn2Code.Api/Learn2Code.Api.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    python3 \
    python3-pip \
    && rm -rf /var/lib/apt/lists/*

RUN ln -s /usr/bin/python3 /usr/bin/python

WORKDIR /app

COPY --from=build /app/publish .

COPY sandbox/ /app/sandbox/

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development
ENV Python__Path=python
ENV Python__SandboxDir=/app/sandbox
ENV Database__SkipSeed=false

ENTRYPOINT ["dotnet", "Learn2Code.Api.dll"]