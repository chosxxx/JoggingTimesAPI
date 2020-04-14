using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using RestSharp;
using Microsoft.Extensions.Options;
using JoggingTimesAPI.Helpers;
using System.Text;

namespace JoggingTimesAPI.WeatherProviders
{
    public interface IWeatherProvider
    {
        Task<JObject> GetCurrentWeather(double latitude, double longitude);
    }

    public abstract class WeatherProvider : IWeatherProvider
    {
        private readonly AppSettings _appSettings;

        protected WeatherProvider(IOptions<AppSettings> appSettings)
        {
            _appSettings = appSettings.Value;
        }

        protected string EndPoint => _appSettings.WeatherProviderEndpoint;

        protected IDictionary<string, string> Headers
        {
            get
            {
                return _appSettings.WeatherRequestHeaders;
            }
        }

        protected abstract string ParseRequestParameters(double latitude, double longitude);
        
        public async Task<JObject> GetCurrentWeather(double latitude, double longitude)
        {
            var requestUrl = $"{EndPoint}?{ParseRequestParameters(latitude, longitude)}";
            var client = new RestClient(requestUrl);
            var request = new RestRequest(Method.GET);
            request.AddHeaders(Headers);
            var response = await client.ExecuteAsync(request);
            return JObject.Parse(response.Content);
        }
    }
}
