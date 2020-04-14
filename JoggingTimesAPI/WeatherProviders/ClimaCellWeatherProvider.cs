using JoggingTimesAPI.Helpers;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JoggingTimesAPI.WeatherProviders
{
    public class ClimaCellWeatherProvider : WeatherProvider
    {
        public ClimaCellWeatherProvider(IOptions<AppSettings> appSettings) : base(appSettings)
        {

        }

        protected override string ParseRequestParameters(double latitude, double longitude)
        {
            var requestParameters = new StringBuilder();
            requestParameters.Append($"lat={latitude}&");
            requestParameters.Append($"lon={longitude}&");
            requestParameters.Append("unit_system=si&");
            requestParameters.Append("fields=precipitation,temp,feels_like,wind_speed,humidity,cloud_cover");

            return requestParameters.ToString();
        }
    }
}
