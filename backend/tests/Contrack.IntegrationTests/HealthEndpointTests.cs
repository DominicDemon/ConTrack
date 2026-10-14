using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Contrack.IntegrationTests;

/// <summary>
/// Test API chạy trong bộ nhớ. Endpoint /api/health trả 200 kể cả khi chưa bật PostgreSQL
/// (khi đó masterDb = "unreachable"). Test cần database thật sẽ dùng Testcontainers sau.
/// </summary>
public class HealthEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_endpoint_returns_ok()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
