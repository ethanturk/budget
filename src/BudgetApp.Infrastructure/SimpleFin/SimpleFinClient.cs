using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace BudgetApp.Infrastructure.SimpleFin;

public sealed class SimpleFinClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public SimpleFinClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> ClaimAccessUrlAsync(string setupToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(setupToken);

        var claimUrl = Encoding.UTF8.GetString(Convert.FromBase64String(setupToken));
        using var request = new HttpRequestMessage(HttpMethod.Post, claimUrl)
        {
            Content = new ByteArrayContent([])
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
    }

    public async Task<AccountSetResponse> GetAccountsAsync(string accessUrl, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessUrl);

        var accessUri = new Uri(accessUrl);
        var credentials = accessUri.UserInfo.Split(':', 2);
        if (credentials.Length != 2)
        {
            throw new InvalidOperationException("SimpleFIN access URL must include Basic Auth credentials.");
        }

        var builder = new UriBuilder(accessUri)
        {
            UserName = string.Empty,
            Password = string.Empty,
            Path = $"{accessUri.AbsolutePath.TrimEnd('/')}/accounts",
            Query = "version=2"
        };

        using var request = new HttpRequestMessage(HttpMethod.Get, builder.Uri);
        var rawCredentials = Encoding.UTF8.GetBytes($"{credentials[0]}:{credentials[1]}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(rawCredentials));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<AccountSetResponse>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("SimpleFIN account payload was empty.");

        return payload;
    }
}
