FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Install JRE required by Antlr4BuildTasks to generate parsers from .g4 grammars
RUN apt-get update && apt-get install -y --no-install-recommends default-jre-headless \
    && rm -rf /var/lib/apt/lists/*

COPY MockDynamoDB.slnx .
COPY src/MockDynamoDB.Core/MockDynamoDB.Core.csproj src/MockDynamoDB.Core/
COPY src/MockDynamoDB.Server/MockDynamoDB.Server.csproj src/MockDynamoDB.Server/
RUN dotnet restore src/MockDynamoDB.Server/MockDynamoDB.Server.csproj

COPY src/ src/
RUN dotnet publish src/MockDynamoDB.Server/MockDynamoDB.Server.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8000
EXPOSE 8000

ENTRYPOINT ["dotnet", "MockDynamoDB.Server.dll"]
