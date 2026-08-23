using System.Net;
using System.Net.Http.Json;
using TicketFlow.Api.Contracts;
using TicketFlow.Api.Models;

namespace TicketFlow.Api.Tests;

public class SeatEndpointsTests(TicketFlowApiFactory factory) : IClassFixture<TicketFlowApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // most of these tests just need "an event with exactly one seat to poke at", so grabbing that setup here
    private async Task<(Guid EventId, Guid SeatId)> CreateEventWithOneSeatAsync()
    {
        var request = new CreateEventRequest(
            "Test Event", "Test Venue", DateTime.UtcNow.AddDays(5), null,
            new SeatMapRequest(["GA"], RowsPerSection: 1, SeatsPerRow: 1));

        var response = await _client.PostAsJsonAsync("/events", request);
        var created = await response.Content.ReadFromJsonAsync<EventResponse>();

        var seats = await _client.GetFromJsonAsync<List<SeatResponse>>($"/events/{created!.Id}/seats");

        return (created.Id, seats!.Single().Id);
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
