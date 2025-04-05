namespace ArenaBackend.Services
{
    public interface IRiotApiKeyManager
    {
        string GetApiKey();
        void UpdateApiKey(string newApiKey);
    }
}