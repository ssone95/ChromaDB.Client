using ChromaDB.Client.Models;

namespace ChromaDB.Client;

public class ClientConstants
{
    public const string DefaultDatabaseName = "default_database";
    public const string DefaultTenantName = "default_tenant";
    public const string DefaultUri = "http://localhost:8000/api/v1/";

    private static Tenant? _defaultTenant = null;
    public static Tenant DefaultTenant => _defaultTenant!;

    private static Database? _defaultDatabase = null;
    public static Database DefaultDatabase => _defaultDatabase!;

	#region Initialization
	private static object _lock = new();
    private static void InitializeDefaults()
    {
        lock(_lock)
        {
            _defaultDatabase ??= new(DefaultDatabaseName);
			_defaultTenant ??= new(DefaultTenantName);
        }
    }

    static ClientConstants()
    {
        InitializeDefaults();
    }
	#endregion
}
