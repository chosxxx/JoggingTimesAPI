using System.Collections.Generic;

namespace JoggingTimesAPI.Helpers
{
    public class AppSettings
    {
        public string Secret { get; set; }
        public string WeatherProviderName { get; set; }
        public string WeatherProviderEndpoint { get; set; }
        public IDictionary<string, string> WeatherRequestHeaders { get; set; }
    }
}