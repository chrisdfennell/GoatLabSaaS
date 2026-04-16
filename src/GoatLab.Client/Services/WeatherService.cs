using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;

namespace GoatLab.Client.Services;

public class WeatherService
{
    private readonly IJSRuntime _js;
    private readonly HttpClient _http;

    public WeatherService(IJSRuntime js)
    {
        _js = js;
        // Separate HttpClient — not our API — since Open-Meteo is external
        _http = new HttpClient();
    }

    public async Task<(double lat, double lng)?> GetLocationAsync()
    {
        try
        {
            var loc = await _js.InvokeAsync<Geolocation?>("goatWeather.getLocation");
            if (loc == null) return null;
            return (loc.Lat, loc.Lng);
        }
        catch { return null; }
    }

    public async Task<WeatherSnapshot?> GetCurrentAsync(double lat, double lng)
    {
        try
        {
            var url = $"https://api.open-meteo.com/v1/forecast" +
                      $"?latitude={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                      $"&longitude={lng.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                      $"&current=temperature_2m,relative_humidity_2m,weather_code,wind_speed_10m,apparent_temperature" +
                      $"&daily=temperature_2m_max,temperature_2m_min,precipitation_probability_max,weather_code" +
                      $"&forecast_days=3&timezone=auto&temperature_unit=fahrenheit&wind_speed_unit=mph&precipitation_unit=inch";
            return await _http.GetFromJsonAsync<WeatherSnapshot>(url);
        }
        catch
        {
            return null;
        }
    }

    public static string CodeToDescription(int code) => code switch
    {
        0 => "Clear",
        1 or 2 or 3 => "Partly cloudy",
        45 or 48 => "Fog",
        51 or 53 or 55 => "Drizzle",
        56 or 57 => "Freezing drizzle",
        61 or 63 or 65 => "Rain",
        66 or 67 => "Freezing rain",
        71 or 73 or 75 => "Snow",
        77 => "Snow grains",
        80 or 81 or 82 => "Rain showers",
        85 or 86 => "Snow showers",
        95 => "Thunderstorm",
        96 or 99 => "Storm w/ hail",
        _ => "—"
    };

    public static string CodeToIcon(int code) => code switch
    {
        0 => "wb_sunny",
        1 or 2 or 3 => "partly_cloudy_day",
        45 or 48 => "foggy",
        51 or 53 or 55 or 56 or 57 => "rainy_light",
        61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => "rainy",
        71 or 73 or 75 or 77 or 85 or 86 => "ac_unit",
        95 or 96 or 99 => "thunderstorm",
        _ => "cloud"
    };

    private class Geolocation
    {
        [JsonPropertyName("lat")] public double Lat { get; set; }
        [JsonPropertyName("lng")] public double Lng { get; set; }
    }
}

public class WeatherSnapshot
{
    [JsonPropertyName("current")] public WeatherCurrent? Current { get; set; }
    [JsonPropertyName("daily")] public WeatherDaily? Daily { get; set; }
    [JsonPropertyName("timezone")] public string? Timezone { get; set; }
}

public class WeatherCurrent
{
    [JsonPropertyName("temperature_2m")] public double Temperature { get; set; }
    [JsonPropertyName("apparent_temperature")] public double FeelsLike { get; set; }
    [JsonPropertyName("relative_humidity_2m")] public double Humidity { get; set; }
    [JsonPropertyName("weather_code")] public int WeatherCode { get; set; }
    [JsonPropertyName("wind_speed_10m")] public double WindSpeed { get; set; }
}

public class WeatherDaily
{
    [JsonPropertyName("time")] public List<string>? Time { get; set; }
    [JsonPropertyName("temperature_2m_max")] public List<double>? TempMax { get; set; }
    [JsonPropertyName("temperature_2m_min")] public List<double>? TempMin { get; set; }
    [JsonPropertyName("precipitation_probability_max")] public List<int>? PrecipChance { get; set; }
    [JsonPropertyName("weather_code")] public List<int>? WeatherCode { get; set; }
}
