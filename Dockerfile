FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
WORKDIR /src
COPY . .
RUN dotnet restore LagoaSportRpa.csproj
RUN dotnet publish LagoaSportRpa.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0-bookworm-slim
RUN apt-get update \
    && apt-get install -y --no-install-recommends chromium chromium-driver \
    && rm -rf /var/lib/apt/lists/*
ENV CHROME_BIN=/usr/bin/chromium
ENV CHROMEDRIVER_PATH=/usr/bin
ENV HEADLESS=true
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "LagoaSportRpa.dll"]
