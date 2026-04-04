## PostgreSQL migration baseline

The previous migration set in this repository was generated for SQL Server and
referenced provider-specific APIs such as `SqlServerModelBuilderExtensions`.

After switching to Npgsql/PostgreSQL, those migration files were removed because
they do not compile against the PostgreSQL provider.

Generate a new PostgreSQL migration baseline locally:

```bash
dotnet ef migrations add InitialPostgresSchema
dotnet ef database update
```
