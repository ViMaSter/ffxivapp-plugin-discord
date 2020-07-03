namespace FFXIVAPP.Plugin.Discord.ChatHandler
{
    public interface IDiscord
    {
        void Broadcast(string message);
        void SetIsActive(bool active);
    }
}