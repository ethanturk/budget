namespace BudgetApp.Infrastructure.SimpleFin;

public sealed record SimpleFinImportSummary(
    int AccountsSeen,
    int TransactionsSeen,
    int TransactionsInserted,
    int TransactionsUpdated);
