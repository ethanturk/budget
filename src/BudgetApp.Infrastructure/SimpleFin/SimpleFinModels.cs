using System.Text.Json.Serialization;

namespace BudgetApp.Infrastructure.SimpleFin;

public sealed record AccountSetResponse(
    [property: JsonPropertyName("errlist")] IReadOnlyList<SimpleFinErrorResponse> ErrList,
    [property: JsonPropertyName("connections")] IReadOnlyList<ConnectionResponse> Connections,
    [property: JsonPropertyName("accounts")] IReadOnlyList<AccountResponse> Accounts);

public sealed record SimpleFinErrorResponse(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("msg")] string Msg,
    [property: JsonPropertyName("conn_id")] string? ConnId = null,
    [property: JsonPropertyName("account_id")] string? AccountId = null);

public sealed record ConnectionResponse(
    [property: JsonPropertyName("conn_id")] string ConnId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("org_id")] string OrgId,
    [property: JsonPropertyName("org_url")] string? OrgUrl,
    [property: JsonPropertyName("sfin_url")] string SfinUrl);

public sealed record AccountResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("conn_id")] string ConnId,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("balance")] string Balance,
    [property: JsonPropertyName("available-balance")] string? AvailableBalance,
    [property: JsonPropertyName("balance-date")] long BalanceDate,
    [property: JsonPropertyName("transactions")] IReadOnlyList<TransactionResponse>? Transactions);

public sealed record TransactionResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("posted")] long Posted,
    [property: JsonPropertyName("amount")] string Amount,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("payee")] string? Payee,
    [property: JsonPropertyName("memo")] string? Memo,
    [property: JsonPropertyName("transacted_at")] long? TransactedAt,
    [property: JsonPropertyName("pending")] bool? Pending,
    [property: JsonPropertyName("extra")] IReadOnlyDictionary<string, object?>? Extra);
