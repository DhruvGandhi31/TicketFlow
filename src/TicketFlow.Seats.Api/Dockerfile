# build context is the repo root (see docker-compose.yml), so paths here are relative to that

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# csproj copied and restored before the rest of the source, so this layer stays cached across
# rebuilds unless a package reference actually changes - editing a .cs file won't bust it
COPY ["src/TicketFlow.Seats.Api/TicketFlow.Seats.Api.csproj", "src/TicketFlow.Seats.Api/"]
RUN dotnet restore "src/TicketFlow.Seats.Api/TicketFlow.Seats.Api.csproj"

COPY src/TicketFlow.Seats.Api/ src/TicketFlow.Seats.Api/
RUN dotnet publish "src/TicketFlow.Seats.Api/TicketFlow.Seats.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TicketFlow.Seats.Api.dll"]
