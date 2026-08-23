using System.Net;
using System.Net.Http.Json;
using TicketFlow.Api.Contracts;

namespace TicketFlow.Api.Tests;

public class EventEndpointsTests(TicketFlowApiFactory factory) : IClassFixture<TicketFlowApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreateEvent_WithSeatMap_GeneratesExpectedSeatCount()
    {
        var request = new CreateEventRequest(
            "Indie Rock Night", "The Grand Hall", DateTime.UtcNow.AddDays(30), "Local bands",
            new SeatMapRequest(["GA", "Balcony"], RowsPerSection: 3, SeatsPerRow: 10));

        var response = await _client.PostAsJsonAsync("/events", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<EventResponse>();
        Assert.NotNull(created);
        Assert.Equal(60, created.TotalSeats); // 2 sections * 3 rows * 10 seats, just doing the math here so it's obvious
        Assert.Equal(60, created.AvailableSeats);
    }

    [Fact]
    public async Task CreateEvent_MissingName_ReturnsValidationProblem()
    {
        var request = new CreateEventRequest("", "The Grand Hall", DateTime.UtcNow.AddDays(30), null, null);

        var response = await _client.PostAsJsonAsync("/events", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetEventById_UnknownId_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/events/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetEvents_IncludesJustCreatedEvent()
    {
        var request = new CreateEventRequest("Jazz Evening", "Blue Note", DateTime.UtcNow.AddDays(10), null, null);
        var createResponse = await _client.PostAsJsonAsync("/events", request);
        var created = await createResponse.Content.ReadFromJsonAsync<EventResponse>();

        var events = await _client.GetFromJsonAsync<List<EventResponse>>("/events");

        Assert.Contains(events!, e => e.Id == created!.Id);
    }
}
