using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Chaincase.Common.Contracts
{
    public interface IWeatherForecastFetcher
    {
        Task<IEnumerable<WeatherForecast>> Fetch();
    }
    
    
    public class WeatherForecast
    {
        public DateTime Date { get; set; }

        public int TemperatureC { get; set; }

        public string Summary { get; set; }

        public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
    }
}