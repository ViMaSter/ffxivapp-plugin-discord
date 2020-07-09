using Newtonsoft.Json;

using System.Net.Http;

namespace FFXIVAPP.Plugin.Discord
{
    class Pulseway
    {
        private const string Endpoint = "https://api.pulseway.com/v2/";
        private static readonly string Username = Properties.Settings.Default.Pulseway__Username;
        private static readonly string Password = Properties.Settings.Default.Pulseway__Password;

        // ReSharper disable InconsistentNaming
        public class NotifyRequest
        {
            public string instance_id { get; set; }
            public string title { get; set; }
            public string message { get; set; }
            public string priority { get; set; }
        }
        // ReSharper restore InconsistentNaming

        public enum Priority
        {
            Low,
            Normal,
            Elevated,
            Critical
        }

        public static bool IsActive()
        {
            return !string.IsNullOrEmpty(Username);
        }

        public static void SendMessage(string title, string message, Priority priority)
        {
            if (!IsActive())
            {
                return;
            }

            var client = new HttpClient();

            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Username + ":" + Password)));
            client.PostAsync(Endpoint + "notifications", new StringContent(JsonConvert.SerializeObject(new NotifyRequest
            {
                title = title,
                message = message,
                priority = priority.ToString(),
                instance_id = Properties.Settings.Default.Pulseway__InstanceID
            }), System.Text.Encoding.UTF8, "application/json")).Wait();
        }
    }
}
