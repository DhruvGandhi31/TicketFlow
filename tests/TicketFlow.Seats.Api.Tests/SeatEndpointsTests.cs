using System.Net;
using System.Net.Http.Json;
using TicketFlow.Seats.Api.Contracts;
using TicketFlow.Seats.Api.Models;

namespace TicketFlow.Seats.Api.Tests;

public class SeatEndpointsTests(TicketFlowSeatsApiFactory factory) : IClassFixture<TicketFlowSeatsApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // this service doesn't check the event actually exists in the Events service (can't, not
    // without gRPC yet - see phase 3), so any random guid works fine as an event id here
    private async Task<(Guid EventId, Guid SeatId)> CreateEventWithOneSeatAsync()
    {
        var eventId = Guid.NewGuid();
        var request = new GenerateSeatMapRequest(["GA"], RowsPerSection: 1, SeatsPerRow: 1);

        await _client.PostAsJsonAsync($"/events/{eventId}/seats", request);

        var seats = await _client.GetFromJsonAsync<List<SeatResponse>>($"/events/{eventId}/seats");

        return (eventId, seats!.Single().Id);
    }

    [Fact]
    public async Task GenerateSeatMap_CreatesExpectedSeatCount()
    {
        var eventId = Guid.NewGuid();
        var request = new GenerateSeatMapRequest(["GA", "Balcony"], RowsPerSection: 3, SeatsPerRow: 10);

        var response = await _client.PostAsJsonAsync($"/events/{eventId}/seats", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var seats = await _client.GetFromJsonAsync<List<SeatResponse>>($"/events/{eventId}/seats");
        Assert.Equal(60, seats!.Count); // 2 sections * 3 rows * 10 seats
    }

    [Fact]
    public async Task Reserve_AvailableSeat_TransitionsToHeld()
    {
        var (eventId, seatId) = await CreateEventWithOneSeatAsync();

        var response = await _client.PostAsync($"/events/{eventId}/seats/{seatId}/reserve", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var seat = await response.Content.ReadFromJsonAsync<SeatResponse>();
        Assert.Equal(SeatStatus.Held, seat!.Status);
        Assert.NotNull(seat.HeldUntilUtc);
    }

    [Fact]
    public async Task Reserve_AlreadyHeldSeat_ReturnsConflict()
    {
        var (eventId, seatId) = await CreateEventWithOneSeatAsync();
        await _client.PostAsync($"/events/{eventId}/seats/{seatId}/reserve", null);

        var response = await _client.PostAsync($"/events/{eventId}/seats/{seatId}/reserve", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Book_SeatWithActiveHold_TransitionsToBooked()
    {
        var (eventId, seatId) = await CreateEventWithOneSeatAsync();
        await _client.PostAsync($"/events/{eventId}/seats/{seatId}/reserve", null);

        var response = await _client.PostAsync($"/events/{eventId}/seats/{seatId}/book", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var seat = await response.Content.ReadFromJsonAsync<SeatResponse>();
        Assert.Equal(SeatStatus.Booked, seat!.Status);
        Assert.Null(seat.HeldUntilUtc);
    }

    [Fact]
    public async Task Book_SeatWithoutHold_ReturnsConflict()
    {
        var (eventId, seatId) = await CreateEventWithOneSeatAsync();

        var response = await _client.PostAsync($"/events/{eventId}/seats/{seatId}/book", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Release_HeldSeat_ReturnsToAvailable()
    {
        var (eventId, seatId) = await CreateEventWithOneSeatAsync();
        await _client.PostAsync($"/events/{eventId}/seats/{seatId}/reserve", null);

        var response = await _client.PostAsync($"/events/{eventId}/seats/{seatId}/release", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var seat = await response.Content.ReadFromJsonAsync<SeatResponse>();
        Assert.Equal(SeatStatus.Available, seat!.Status);
    }

    // the important one - fires 10 reserves at the same seat at once and makes sure only 1 wins.
    // sequential tests above don't actually prove the race guard works, this does
    [Fact]
    public async Task Reserve_ConcurrentRequestsForSameSeat_OnlyOneSucceeds()
    {
        var (eventId, seatId) = await CreateEventWithOneSeatAsync();

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => _client.PostAsync($"/events/{eventId}/seats/{seatId}/reserve", null)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(9, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }
}
