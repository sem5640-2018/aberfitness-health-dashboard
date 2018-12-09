using IdentityModel.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace health_dashboard.Services
{
    public interface IApiClient
    {
        Task<HttpResponseMessage> DeleteAsync(string path);
        Task<HttpResponseMessage> GetAsync(string path);
        Task<HttpResponseMessage> PostAsync<T>(string path, T content);
    }

    public class ApiClient : IApiClient
    {
        private readonly HttpClient client;
        private readonly IConfigurationSection appConfig;
        private readonly DiscoveryCache discoveryCache;
        private readonly ILogger logger;

        public ApiClient(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<ApiClient> log)
        {
            appConfig = configuration.GetSection("Health_Dashboard");
            discoveryCache = new DiscoveryCache(appConfig.GetValue<string>("GatekeeperUrl"));
            client = httpClientFactory.CreateClient("healthDashboardHttpClient");
            logger = log;
        }

        private async Task<string> GetTokenAsync()
        {
            var discovery = await discoveryCache.GetAsync();
            if (discovery.IsError)
            {
                logger.LogError(discovery.Error);
                throw new ApiClientException("Couldn't read discovery document.");
            }

            var tokenRequest = new ClientCredentialsTokenRequest
            {
                Address = discovery.TokenEndpoint,
                ClientId = appConfig.GetValue<string>("ClientId"),
                ClientSecret = appConfig.GetValue<string>("ClientSecret"),

                // The ApiResourceName of the resources you want to access.
                // Other valid values might be `comms`, `health_data_repository`, etc.
                // Ask in #dev-gatekeeper for help
                Scope = "gatekeeper health_data_repository user_groups challenges"
            };
            var response = await client.RequestClientCredentialsTokenAsync(tokenRequest);
            if (response.IsError)
            {
                logger.LogError(response.Error);
                throw new ApiClientException("Couldn't retrieve access token.");
            }
            return response.AccessToken;
        }

        public async Task<HttpResponseMessage> DeleteAsync(string uri)
        {
            client.SetBearerToken(await GetTokenAsync());
            return await client.DeleteAsync(uri);
        }

        public async Task<HttpResponseMessage> GetAsync(string uri)
        {
            client.SetBearerToken(await GetTokenAsync());
            return await client.GetAsync(uri);
        }

        public async Task<HttpResponseMessage> PostAsync<T>(string uri, T content)
        {
            client.SetBearerToken(await GetTokenAsync());
            return await client.PostAsJsonAsync(uri, content);
        }
    }

    public class ApiClientException : Exception
    {
        public ApiClientException(string message) : base(message)
        {
        }
    }
}