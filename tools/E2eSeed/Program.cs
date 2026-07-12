using LexFlow.Infrastructure.Security;
using Npgsql;

// PRD §37: "seed factory ... producing a deterministic demo tenant ... used
// by all test tiers and demos." The `lexflow-database` repo's
// `16_Seed/008_Demo_Tenant.sql` already creates the tenant + one branch
// (fixed id '00000000-0000-0000-0000-000000000001') but stops there — no
// script anywhere creates a *user* to log in as (login needs a real
// Argon2id-hashed password, which can't be hand-written into a raw SQL
// seed file without running the app's own hasher). This tool is that
// missing piece: run it once against a freshly-migrated+seeded Postgres
// (docker-compose + the lexflow-database Scripts, in numeric order) and it
// idempotently provisions exactly one login-capable user plus the one
// piece of Fin reference data (a number series) that invoice creation
// needs and that 16_Seed never populated either.
//
// Usage: LEXFLOW_E2E_DB="Host=localhost;Port=5432;Database=lexflow;Username=postgres;Password=postgres" dotnet run --project tools/E2eSeed
// Prints the seeded tenant slug + email + password so Playwright's env can pick them up.

const string demoTenantId = "00000000-0000-0000-0000-000000000001";
const string demoBranchId = "00000000-0000-0000-0000-000000000002";
const string e2eUserId = "00000000-0000-0000-0000-e2e000000001";
const string e2eNumberSeriesId = "00000000-0000-0000-0000-e2e000000002";
const string e2ePortalClientId = "00000000-0000-0000-0000-e2e000000003";
const string e2ePortalUserId = "00000000-0000-0000-0000-e2e000000004";
const string tenantSlug = "lexflow-demo";
const string e2eEmail = "e2e.lawyer@lexflow-demo.test";
const string e2ePassword = "E2eTest!2025";
const string e2eName = "E2E Test Lawyer";
const string e2ePortalEmail = "e2e.client@lexflow-demo.test";
const string e2ePortalPassword = "E2ePortal!2025";
const string e2ePortalClientName = "E2E Portal Client";

var connectionString = Environment.GetEnvironmentVariable("LEXFLOW_E2E_DB")
    ?? "Host=localhost;Port=5432;Database=lexflow;Username=postgres;Password=postgres";

var passwordHasher = new Argon2PasswordHasher();
var passwordHash = passwordHasher.Hash(e2ePassword);

await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();

// 1. The E2E user — "owner" role (full permission reach, PRD §21) so every
// one of the 40 journeys can exercise any module without a second seeded
// identity. Upsert-by-id so re-running this tool (e.g. once per CI run
// against a fresh container) never fails on a duplicate-key error.
await using (var command = new NpgsqlCommand(
    """
    INSERT INTO core.users (id, tenant_id, email, password_hash, name, branch_id, status)
    VALUES (@id, @tenantId, @email, @passwordHash, @name, @branchId, 'Active')
    ON CONFLICT (id) DO UPDATE SET
      email = EXCLUDED.email,
      password_hash = EXCLUDED.password_hash,
      name = EXCLUDED.name,
      branch_id = EXCLUDED.branch_id,
      status = 'Active';
    """,
    connection))
{
    command.Parameters.AddWithValue("id", Guid.Parse(e2eUserId));
    command.Parameters.AddWithValue("tenantId", Guid.Parse(demoTenantId));
    command.Parameters.AddWithValue("email", e2eEmail);
    command.Parameters.AddWithValue("passwordHash", passwordHash);
    command.Parameters.AddWithValue("name", e2eName);
    command.Parameters.AddWithValue("branchId", Guid.Parse(demoBranchId));
    await command.ExecuteNonQueryAsync();
}

// 2. Role assignment — "owner" is one of the 10 system roles seeded by
// `16_Seed/002_System_Roles.sql`; look it up by (tenant_id, key) rather than
// hardcoding its generated uuid.
await using (var command = new NpgsqlCommand(
    """
    INSERT INTO core.user_roles (tenant_id, user_id, role_id)
    SELECT @tenantId, @userId, r.id
    FROM core.roles r
    WHERE r.tenant_id = @tenantId AND r.key = 'owner'
    ON CONFLICT (user_id, role_id) DO NOTHING;
    """,
    connection))
{
    command.Parameters.AddWithValue("tenantId", Guid.Parse(demoTenantId));
    command.Parameters.AddWithValue("userId", Guid.Parse(e2eUserId));
    await command.ExecuteNonQueryAsync();
}

// 3. Invoice number series — `fin.number_series` has no 16_Seed entry at
// all; without one, the first invoice created by the WIP->invoice->pay
// journey has nothing to draw a number from. One firm-wide ("branch_id"
// NULL) "INV" series for the current fiscal year, matching the demo
// tenant's April fiscal-year start (`008_Demo_Tenant.sql`:
// fiscal_year_start_month = 4).
var now = DateTime.UtcNow;
var fiscalYear = now.Month >= 4 ? now.Year : now.Year - 1;

await using (var command = new NpgsqlCommand(
    """
    INSERT INTO fin.number_series (id, tenant_id, branch_id, series_key, fiscal_year, next_seq)
    VALUES (@id, @tenantId, NULL, 'INV', @fiscalYear, 1)
    ON CONFLICT (id) DO NOTHING;
    """,
    connection))
{
    command.Parameters.AddWithValue("id", Guid.Parse(e2eNumberSeriesId));
    command.Parameters.AddWithValue("tenantId", Guid.Parse(demoTenantId));
    command.Parameters.AddWithValue("fiscalYear", fiscalYear);
    await command.ExecuteNonQueryAsync();
}

// 4. Portal-side identity for the flagged WIP->invoice->pay(Razorpay)->receipt
// journey, which is genuinely cross-app (staff generates the invoice,
// the CLIENT pays it via the portal's real Razorpay checkout). Client
// portal users are normally provisioned via an email invite
// (`ClientPortalUser.Status` "Invited" -> "Active" through `/auth/reset`,
// per Module 17), which this environment can't complete without a real
// mailbox to read the token from — so, exactly like the staff user above,
// this inserts an already-"Active" row directly rather than faking an
// email round-trip. `visible_matter_ids` stays NULL (all of this client's
// matters visible), matching an individual client's sole login.
await using (var command = new NpgsqlCommand(
    """
    INSERT INTO crm.clients (id, tenant_id, number, type, first_name, last_name, status, branch_id, portal_enabled)
    VALUES (@id, @tenantId, 'CL-E2E-0001', 'Individual', @firstName, @lastName, 'Active', @branchId, true)
    ON CONFLICT (id) DO UPDATE SET portal_enabled = true, status = 'Active';
    """,
    connection))
{
    command.Parameters.AddWithValue("id", Guid.Parse(e2ePortalClientId));
    command.Parameters.AddWithValue("tenantId", Guid.Parse(demoTenantId));
    command.Parameters.AddWithValue("firstName", "E2E");
    command.Parameters.AddWithValue("lastName", "Portal Client");
    command.Parameters.AddWithValue("branchId", Guid.Parse(demoBranchId));
    await command.ExecuteNonQueryAsync();
}

await using (var command = new NpgsqlCommand(
    """
    INSERT INTO crm.client_portal_users (id, tenant_id, client_id, email, password_hash, name, status)
    VALUES (@id, @tenantId, @clientId, @email, @passwordHash, @name, 'Active')
    ON CONFLICT (id) DO UPDATE SET
      email = EXCLUDED.email,
      password_hash = EXCLUDED.password_hash,
      name = EXCLUDED.name,
      status = 'Active';
    """,
    connection))
{
    command.Parameters.AddWithValue("id", Guid.Parse(e2ePortalUserId));
    command.Parameters.AddWithValue("tenantId", Guid.Parse(demoTenantId));
    command.Parameters.AddWithValue("clientId", Guid.Parse(e2ePortalClientId));
    command.Parameters.AddWithValue("email", e2ePortalEmail);
    command.Parameters.AddWithValue("passwordHash", passwordHasher.Hash(e2ePortalPassword));
    command.Parameters.AddWithValue("name", e2ePortalClientName);
    await command.ExecuteNonQueryAsync();
}

Console.WriteLine("E2E seed complete.");
Console.WriteLine($"  Tenant slug:        {tenantSlug}");
Console.WriteLine($"  Staff email:        {e2eEmail}");
Console.WriteLine($"  Staff password:     {e2ePassword}");
Console.WriteLine($"  Portal client id:   {e2ePortalClientId}");
Console.WriteLine($"  Portal email:       {e2ePortalEmail}");
Console.WriteLine($"  Portal password:    {e2ePortalPassword}");
