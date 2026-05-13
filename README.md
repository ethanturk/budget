# Budget App

Self-hosted ASP.NET Core / Blazor budgeting app with PostgreSQL persistence.

## Prerequisites

- .NET 8 SDK
- PostgreSQL database

If `dotnet` is installed locally instead of on your system `PATH`, use:

```bash
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$HOME/.local/dotnet-tools:$PATH"
```

## Local development setup

This project uses **ASP.NET User Secrets** for the Development database connection string so credentials do not live in tracked config files.

Set your local Development connection string with:

```bash
dotnet user-secrets set "ConnectionStrings:BudgetApp" "<your-connection-string>" --project src/BudgetApp.Web/BudgetApp.Web.csproj
```

Example format:

```text
Server=<host>;Port=<port>;Database=<database>;User Id=<user>;Password=<password>;
```

You can confirm the secret is set with:

```bash
dotnet user-secrets list --project src/BudgetApp.Web/BudgetApp.Web.csproj
```

## Database

Apply migrations with:

```bash
dotnet-ef database update \
  --project src/BudgetApp.Infrastructure/BudgetApp.Infrastructure.csproj \
  --startup-project src/BudgetApp.Web/BudgetApp.Web.csproj
```

## Run the app

```bash
dotnet run --project src/BudgetApp.Web/BudgetApp.Web.csproj --no-launch-profile
```

## Notes

- `appsettings.json` contains only safe defaults.
- `appsettings.Development.json` does **not** contain the database credential.
- The Development DB connection should be stored only in local User Secrets or environment variables.
