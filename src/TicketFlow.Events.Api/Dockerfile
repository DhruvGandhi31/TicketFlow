# build context is the repo root (see docker-compose.yml), so paths here are relative to that

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# csproj copied and restored before the rest of the source, so this layer stays cached across
# rebuilds unless a package reference actually changes - editing a .cs file won't bust it
COPY ["src/TicketFlow.Events.Api/TicketFlow.Events.Api.csproj", "src/TicketFlow.Events.Api/"]
RUN dotnet restore "src/TicketFlow.Events.Api/TicketFlow.Events.Api.csproj"

COPY src/TicketFlow.Events.Api/ src/TicketFlow.Events.Api/
RUN dotnet publish "src/TicketFlow.Events.Api/TicketFlow.Events.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TicketFlow.Events.Api.dll"]
