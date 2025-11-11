FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine
WORKDIR /app

COPY MemoryScramble.API/MemoryScramble.API.csproj ./MemoryScramble.API/
RUN dotnet restore MemoryScramble.API/MemoryScramble.API.csproj

COPY MemoryScramble.API/ ./MemoryScramble.API/

EXPOSE 8080

ENTRYPOINT ["dotnet", "run", "--project", "MemoryScramble.API/MemoryScramble.API.csproj", "--launch-profile", "Host"]
