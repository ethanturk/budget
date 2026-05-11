using System.Net;
using BudgetApp.Infrastructure.Persistence;
using BudgetApp.Infrastructure.SimpleFin;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.IntegrationTests.SimpleFin;

public sealed class SimpleFinConnectionServiceTests
{
    [Fact]
    public async Task ConnectAsync_ClaimsAccessUrl_AndStoresConnection()
    {
        var dbOptions = new DbContextOptionsBuilder<BudgetAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new BudgetAppDbContext(dbOptions);
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("https://demo:secret@bridge.simplefin.org/simplefin")
        }));

        var client = new SimpleFinClient(httpClient);
        var service = new SimpleFinConnectionService(dbContext, client);
        var setupToken = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("https://bridge.simplefin.org/simplefin/claim/demo-token"));

        var connectionId = await service.ConnectAsync(setupToken, CancellationToken.None);

        var connection = await dbContext.SimpleFinConnections.SingleAsync();
        Assert.Equal(connection.Id, connectionId);
        Assert.Equal("https://demo:secret@bridge.simplefin.org/simplefin", connection.AccessUrlCiphertext);
        Assert.Equal("active", connection.Status);
        Assert.Equal("bridge.simplefin.org", connection.AccessUrlHint);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_responseFactory(request));
    }
}
