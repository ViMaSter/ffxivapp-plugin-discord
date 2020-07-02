using Newtonsoft.Json;
using System;
using System.Net.Http;

namespace FFXIVAPP.Plugin.Log
{
    class Pulseway
    {
        private const string ENDPOINT = "https://api.pulseway.com/v2/";
        private static readonly string USERNAME = Properties.Settings.Default.Pulseway__Username;
        private static readonly string PASSWORD = Properties.Settings.Default.Pulseway__Password;

        public class NotifyRequest
        {
            public string instance_id { get; set; }
            public string title { get; set; }
            public string message { get; set; }
            public string priority { get; set; }
        }

        public enum Priority
        {
            low,
            normal,
            elevated,
            critical
        }

        public static bool IsActive()
        {
            return !string.IsNullOrEmpty(USERNAME);
        }

        public static void SendMessage(string title, string message, Priority priority)
        {
            if (!IsActive())
            {
                return;
            }

            var client = new HttpClient();

            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(USERNAME + ":" + PASSWORD)));
            var response = client.PostAsync(ENDPOINT + "notifications", new StringContent(JsonConvert.SerializeObject(new NotifyRequest
            {
                title = title,
                message = message,
                priority = priority.ToString(),
                instance_id = Properties.Settings.Default.Pulseway__InstanceID
            }), System.Text.Encoding.UTF8, "application/json")).Result;

            Console.WriteLine(response);
        }
    }
}
