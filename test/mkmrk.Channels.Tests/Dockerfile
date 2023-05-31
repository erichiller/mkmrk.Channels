FROM mcr.microsoft.com/dotnet/sdk:7.0
WORKDIR /app

COPY ["InterThread/BroadcastQueueTests/BroadcastQueueTests.csproj", "InterThread/BroadcastQueueTests/"]
COPY ["Common/Common.csproj", "Common/"]
RUN #dotnet restore "InterThread/BroadcastQueueTests/BroadcastQueueTests.csproj"
COPY . .

WORKDIR "/app/InterThread/BroadcastQueueTests"
RUN dotnet restore

ENTRYPOINT ["dotnet", "test", "--logger", "console;verbosity=detailed"]
