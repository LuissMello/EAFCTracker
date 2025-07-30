using System;
using System.Text.Json;

public class EAFCService : IEAFCService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public EAFCService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<List<Match>> GetClubMatchesAsync()
    {

        HttpClientHandler handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;


        var httpClient = new HttpClient(handler);


        var baseUrl = _configuration["EAFCSettings:BaseUrl"];
        var endpoint = _configuration["EAFCSettings:ClubMatchesEndpoint"];

        endpoint = string.Format(endpoint, "3463149");


        httpClient.Timeout = TimeSpan.FromMinutes(2);

        var response = await httpClient.GetAsync($"{baseUrl + endpoint}");

        if (!response.IsSuccessStatusCode)
            return new List<Match>();

        var content = await response.Content.ReadAsStringAsync();
        var matches = JsonSerializer.Deserialize<List<Match>>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return matches ?? new List<Match>();
    }
}