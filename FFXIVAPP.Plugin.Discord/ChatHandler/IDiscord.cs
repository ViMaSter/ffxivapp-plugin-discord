namespace FFXIVAPP.Plugin.Discord.ChatHandler
{
    using System.Threading.Tasks;

    public interface IDiscord
    {
        Task Broadcast(string message);
        void SetIsActive(bool active);
    }
}