using System.Net;
using System.Net.Http.Json;
using TicketFlow.Events.Api.Contracts;

namespace TicketFlow.Events.Api.Tests;

public class EventEndpointsTests(TicketFlowEventsApiFactory factory) : IClassFixture<TicketFlowEventsApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreateEvent_ValidRequest_ReturnsCreated()
    {
        var request = new CreateEventRequest("Indie Rock Night", "The Grand Hall", DateTime.UtcNow.AddDays(30), "Local bands");

        var response = await _client.PostAsJsonAsync("/events", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<EventResponse>();
        Assert.NotNull(created);
        Assert.Equal("Indie Rock Night", created.Name);
    }

    [Fact]
    public async Task CreateEvent_MissingName_ReturnsValidationProblem()
    {
        var request = new CreateEventRequest("", "The Grand Hall", DateTime.UtcNow.AddDays(30), null);

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
        var request = new CreateEventRequest("Jazz Evening", "Blue Note", DateTime.UtcNow.AddDays(10), null);
        var createResponse = await _client.PostAsJsonAsync("/events", request);
        var created = await createResponse.Content.ReadFromJsonAsync<EventResponse>();

        var events = await _client.GetFromJsonAsync<List<EventResponse>>("/events");

        Assert.Contains(events!, e => e.Id == created!.Id);
    }
}
