FROM mcr.microsoft.com/dotnet/core/aspnet:3.1
WORKDIR /app
COPY ./src/Game.Services.EventProcessor.API/bin/Docker .
ENV ASPNETCORE_URLS http://*:7001
ENV ASPNETCORE_ENVIRONMENT docker  
ENTRYPOINT dotnet Game.Services.EventProcessor.API.dll