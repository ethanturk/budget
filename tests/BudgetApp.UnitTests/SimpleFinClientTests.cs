using System.Net;
using System.Text;
using BudgetApp.Infrastructure.SimpleFin;

namespace BudgetApp.UnitTests.SimpleFin;

public sealed class SimpleFinClientTests
{
    [Fact]
    public async Task ClaimAccessUrlAsync_PostsToDecodedSetupTokenUrl()
    {
        var setupToken = Convert.ToBase64String(Encoding.UTF8.GetBytes("https://bridge.simplefin.org/simplefin/claim/demo-token"));
        var handler = new StubHttpMessageHandler(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://bridge.simplefin.org/simplefin/claim/demo-token", request.RequestUri?.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("https://demo:secret@bridge.simplefin.org/simplefin")
            };
        });

        using var httpClient = new HttpClient(handler);
        var client = new SimpleFinClient(httpClient);

        var accessUrl = await client.ClaimAccessUrlAsync(setupToken, CancellationToken.None);

        Assert.Equal("https://demo:secret@bridge.simplefin.org/simplefin", accessUrl);
    }

    [Fact]
    public async Task GetAccountsAsync_UsesBasicAuthAndVersion2Query()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.StartsWith("https://bridge.simplefin.org/simplefin/accounts?", request.RequestUri?.ToString());
            Assert.Contains("version=2", request.RequestUri?.Query);
            Assert.Contains("start-date=", request.RequestUri?.Query);
            Assert.Contains("end-date=", request.RequestUri?.Query);
            Assert.Equal("Basic ZGVtbzpzZWNyZXQ=", request.Headers.Authorization?.ToString());

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "errlist": [],
                  "connections": [
                    {
                      "conn_id": "CON-1",
                      "name": "My Bank - Ethan",
                      "org_id": "ORG-1",
                      "org_url": "https://bank.example",
                      "sfin_url": "https://bank.example/simplefin"
                    }
                  ],
                  "accounts": [
                    {
                      "id": "CHK-1",
                      "name": "Checking",
                      "conn_id": "CON-1",
                      "currency": "USD",
                      "balance": "123.45",
                      "available-balance": "100.00",
                      "balance-date": 1715000000,
                      "transactions": [
                        {
                          "id": "TX-1",
                          "posted": 1715000000,
                          "amount": "-12.34",
                          "description": "Coffee Shop",
                          "pending": false
                        }
                      ]
                    }
                  ]
                }
                """)
            };
        });

        using var httpClient = new HttpClient(handler);
        var client = new SimpleFinClient(httpClient);

        var response = await client.GetAccountsAsync("https://demo:secret@bridge.simplefin.org/simplefin", CancellationToken.None);

        Assert.Single(response.Accounts);
        Assert.Equal("CHK-1", response.Accounts[0].Id);
        Assert.Single(response.Connections);
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
