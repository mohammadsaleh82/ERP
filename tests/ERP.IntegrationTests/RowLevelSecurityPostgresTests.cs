using Npgsql;
using Xunit;

namespace ERP.IntegrationTests;

/// <summary>
/// تست RLS (Row-Level Security) روی PostgreSQL واقعی — در صورت فعال بودن RLS با
/// Connection Pooling و تراکنش‌ها نباید Context بین Tenantها نشت کند.
/// الگوی اعمال زمینه: SET LOCAL داخل تراکنش (اتوماتیک ریست با Commit/Rollback).
/// در نبود سرور، تست‌ها Skip می‌شوند.
/// </summary>
public class RowLevelSecurityPostgresTests : IAsyncLifetime
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5433;Username=erp_user;Password=erp_test_password";

    private readonly string _databaseName = $"erp_rls_test_{Guid.NewGuid():N}"[..30];
    private string? _connectionString;
    private bool _available;

    public async Task InitializeAsync()
    {
        var baseConnection = Environment.GetEnvironmentVariable("ERP_TEST_POSTGRES") ?? DefaultConnectionString;
        try
        {
            await using var boot = new NpgsqlConnection(baseConnection);
            await boot.OpenAsync();
            await using (var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\"", boot))
                await cmd.ExecuteNonQueryAsync();
            await using (var cmd = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", boot))
                await cmd.ExecuteNonQueryAsync();
            _available = true;
            _connectionString = new NpgsqlConnectionStringBuilder(baseConnection) { Database = _databaseName }.ConnectionString;
        }
        catch
        {
            _available = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (!_available) return;
        try
        {
            await using var boot = new NpgsqlConnection(
                new NpgsqlConnectionStringBuilder(_connectionString!) { Database = "postgres" }.ConnectionString);
            await boot.OpenAsync();
            await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\"", boot);
            await cmd.ExecuteNonQueryAsync();
        }
        catch { /* cleanup best-effort */ }
    }

    [Fact]
    public async Task PooledConnections_WithSetLocalTenantContext_DoNotLeakAcrossTenants()
    {
        if (!_available)
        {
            // Skip without a server
            return;
        }

        const string tenantA = "11111111-1111-1111-1111-111111111111";
        const string tenantB = "22222222-2222-2222-2222-222222222222";

        await using var admin = new NpgsqlConnection(_connectionString);
        await admin.OpenAsync();
        await using (var cmd = new NpgsqlCommand("""
            CREATE TABLE rls_documents (id serial primary key, tenant_id uuid not null, amount int not null);
            ALTER TABLE rls_documents ENABLE ROW LEVEL SECURITY;
            CREATE POLICY tenant_isolation ON rls_documents
                USING (tenant_id = current_setting('app.tenant_id', true)::uuid);
            """, admin))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        // Connection Pool فعال: هر تراکنش زمینه خودش را با SET LOCAL ست می‌کند
        await using var dataSource = NpgsqlDataSource.Create(_connectionString);

        // درج دو رکورد در دو تراکنش با زمینه‌های متفاوت
        await using (var tx = await dataSource.OpenConnectionAsync())
        await using (var t = await tx.BeginTransactionAsync())
        {
            await using var setCtx = new NpgsqlCommand($"SET LOCAL app.tenant_id = '{tenantA}'", tx, t);
            await setCtx.ExecuteNonQueryAsync();
            await using var insert = new NpgsqlCommand(
                "INSERT INTO rls_documents (tenant_id, amount) VALUES ('11111111-1111-1111-1111-111111111111', 100)", tx, t);
            await insert.ExecuteNonQueryAsync();
            await t.CommitAsync();
        }

        await using (var tx = await dataSource.OpenConnectionAsync())
        await using (var t = await tx.BeginTransactionAsync())
        {
            await using var setCtx = new NpgsqlCommand($"SET LOCAL app.tenant_id = '{tenantB}'", tx, t);
            await setCtx.ExecuteNonQueryAsync();
            await using var insert = new NpgsqlCommand(
                "INSERT INTO rls_documents (tenant_id, amount) VALUES ('22222222-2222-2222-2222-222222222222', 200)", tx, t);
            await insert.ExecuteNonQueryAsync();
            await t.CommitAsync();
        }

        // خواندن هم‌زمان (بین‌راهی): هر زمینه فقط رکورد خودش را می‌بیند
        async Task<int> ReadCountInContext(string tenantId)
        {
            await using var conn = await dataSource.OpenConnectionAsync();
            await using var tx = await conn.BeginTransactionAsync();
            await using var setCtx = new NpgsqlCommand($"SET LOCAL app.tenant_id = '{tenantId}'", conn, tx);
            await setCtx.ExecuteNonQueryAsync();
            await using var select = new NpgsqlCommand("SELECT count(*) FROM rls_documents", conn, tx);
            return Convert.ToInt32(await select.ExecuteScalarAsync());
        }

        (await ReadCountInContext(tenantA)).Should_be(1, tenantA);
        (await ReadCountInContext(tenantB)).Should_be(1, tenantB);

        // نشت Context: اتصال بازگشته به Pool بدون SET LOCAL نباید رکوردی ببیند
        for (var i = 0; i < 10; i++)
        {
            await using var conn = await dataSource.OpenConnectionAsync();
            await using var select = new NpgsqlCommand("SELECT count(*) FROM rls_documents", conn);
            var count = Convert.ToInt32(await select.ExecuteScalarAsync());
            if (count != 0)
                throw new InvalidOperationException(
                    $"نشت Context بین Tenantها در اتصال Pool: اتصال {i} بدون زمینه {count} رکورد دید");
        }
    }

    [Fact]
    public async Task PooledTransactions_InterleavedTenants_StayIsolated()
    {
        if (!_available) return;

        const string tenantA = "33333333-3333-3333-3333-333333333333";
        const string tenantB = "44444444-4444-4444-4444-444444444444";

        await using var admin = new NpgsqlConnection(_connectionString);
        await admin.OpenAsync();
        await using (var cmd = new NpgsqlCommand("""
            CREATE TABLE rls_ledger (id serial primary key, tenant_id uuid not null, balance int not null);
            ALTER TABLE rls_ledger ENABLE ROW LEVEL SECURITY;
            CREATE POLICY tenant_isolation ON rls_ledger
                USING (tenant_id = current_setting('app.tenant_id', true)::uuid);
            INSERT INTO rls_ledger (tenant_id, balance) VALUES ('33333333-3333-3333-3333-333333333333', 10);
            INSERT INTO rls_ledger (tenant_id, balance) VALUES ('44444444-4444-4444-4444-444444444444', 20);
            """, admin))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        await using var dataSource = NpgsqlDataSource.Create(_connectionString);

        // ۲۰ تراکنش بین‌راهی روی Pool: هر تراکنش فقط داده Tenant خودش را می‌بیند
        for (var i = 0; i < 20; i++)
        {
            var tenant = i % 2 == 0 ? tenantA : tenantB;
            var expected = i % 2 == 0 ? 10 : 20;

            await using var conn = await dataSource.OpenConnectionAsync();
            await using var tx = await conn.BeginTransactionAsync();
            await using var setCtx = new NpgsqlCommand($"SET LOCAL app.tenant_id = '{tenant}'", conn, tx);
            await setCtx.ExecuteNonQueryAsync();
            await using var select = new NpgsqlCommand("SELECT balance FROM rls_ledger", conn, tx);
            await using var reader = await select.ExecuteReaderAsync();
            var seen = new List<int>();
            while (await reader.ReadAsync())
                seen.Add(reader.GetInt32(0));
            await tx.CommitAsync();

            if (seen.Count != 1 || seen[0] != expected)
                throw new InvalidOperationException(
                    $"نشت RLS در تراکنش {i}: انتظار [{expected}]، دید [{string.Join(',', seen)}]");
        }
    }
}

internal static class RlsAssertionExtensions
{
    public static void Should_be(this int actual, int expected, string tenant)
    {
        if (actual != expected)
            throw new InvalidOperationException($"RLS نشت کرده: Tenant {tenant} دید {actual} رکورد به‌جای {expected}");
    }
}
