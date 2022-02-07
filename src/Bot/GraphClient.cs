using Microsoft.Identity.Client;
using System.Net.Http.Headers;
using System.Text.Json;

namespace TeamsBotTemplate.Bot
{
    public class GraphClient
    {
        private readonly IConfiguration _configuration;

        private HttpClient _httpClient = new();

        public GraphClient(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<string?> GetChannelIdByUpn(string upn, string appToken)
        {
            /***** THIS IS WHAT WE NEED TO GET *****/
            // Call https://graph.microsoft.com/v1.0/users/{upn}/teamwork/installedapps?$expand=teamsApp&$filter=teamsApp/externalId eq '{Teams App Id}'
            // If no results - app is not installed for that user
            // Get the id of the first result...
            // Call https://graph.microsoft.com/v1.0/users/{upn}/teamwork/installedapps/{The id from the previous query}/chat
            // Get the id of the first result...
            // DONE!!!

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", appToken);

            var appResult = await _httpClient.GetAsync($"https://graph.microsoft.com/v1.0/users/{upn}/teamwork/installedApps/?$expand=teamsApp&$filter=teamsApp/externalId eq '{_configuration["TeamsAppId"]}'");

            var response = await appResult.Content.ReadAsStringAsync();

            var teamsApps = JsonSerializer.Deserialize<TeamsAppResult>(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            var appInstallationId = teamsApps?.Value?.FirstOrDefault()?.Id;

            if (appInstallationId is null)
            {
                return null;
            }            

            var chatResult = await _httpClient.GetAsync($"https://graph.microsoft.com/v1.0/users/{upn}/teamwork/installedApps/{appInstallationId}/chat");

            response = await chatResult.Content.ReadAsStringAsync();

            var chat = JsonSerializer.Deserialize<ChatResult>(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            return chat?.Id;
        }

        public async Task<string> GetOnBehalfOfToken(string token)
        {
            var builder = ConfidentialClientApplicationBuilder.Create(_configuration["MicrosoftAppId"])
                .WithClientSecret(_configuration["MicrosoftAppPassword"]);

            var client = builder.Build();

            //Calls the /oauth2/v2.0/token endpoint to swap the given AAD token for another with different scopes
            var tokenBuilder = client.AcquireTokenOnBehalfOf(new[] { "User.Read", "Chat.ReadBasic", "TeamsAppInstallation.ReadForUser" }, new UserAssertion(token));

            var result = await tokenBuilder.ExecuteAsync();

            return result.AccessToken;
        }

        public async Task<string> GetAppToken(string teamsTenantId)
        {
            var builder = ConfidentialClientApplicationBuilder.Create(_configuration["MicrosoftAppId"])
                .WithClientSecret(_configuration["MicrosoftAppPassword"])
                .WithTenantId(teamsTenantId);

            var client = builder.Build();

            var tokenBuilder = client.AcquireTokenForClient(new[] { "https://graph.microsoft.com/.default" });

            var result = await tokenBuilder.ExecuteAsync();

            return result.AccessToken;
        }

        public async Task<GraphMeResult> GetMe(string token)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var meResult = await _httpClient.GetAsync("https://graph.microsoft.com/v1.0/me");

            return JsonSerializer.Deserialize<GraphMeResult>(await meResult.Content.ReadAsStringAsync(), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }

        public async Task<GraphOrganizationResult> GetOrganisation(string token)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var orgResult = await _httpClient.GetAsync("https://graph.microsoft.com/v1.0/organization");

            return JsonSerializer.Deserialize<GraphOrganizationResult>(await orgResult.Content.ReadAsStringAsync(), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }
    }

    public class GraphMeResult : IdResult
    {
        public string DisplayName { get; set; }
        public string Mail { get; set; }
        public string GivenName { get; set; }
        public string Surname { get; set; }
    }

    public class GraphOrganizationResult
    {
        public GraphOrganization[] Value { get; set; }
    }

    public class GraphOrganization : IdResult
    {
    }

    public class TeamsAppResult
    {
        public IdResult[] Value { get; set; }
    }

    public class IdResult
    {
        public string Id { get; set; }
    }

    public class ChatResult : IdResult
    {
    }
}
