namespace ArenaBackend.Configs;
public class MongoDbSettings
{
    public string ConnectionString { get; set; }
    public string DatabaseName { get; set; }
    public bool IsDevelopment { get; set; }
    public string TestDatabaseSuffix { get; set; } = "_test";
}