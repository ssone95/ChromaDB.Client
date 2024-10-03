using ChromaDB.Client.Models;

namespace ChromaDB.Client.Common.Constants;

public static class ClientConstants
{
	public const string DefaultTenantName = "default_tenant";
	public const string DefaultDatabaseName = "default_database";
	public const string DefaultUri = "http://localhost:8000/api/v1/";

	public static Tenant DefaultTenant { get; } = new(DefaultTenantName);
	public static Database DefaultDatabase { get; } = new(DefaultDatabaseName);
}
