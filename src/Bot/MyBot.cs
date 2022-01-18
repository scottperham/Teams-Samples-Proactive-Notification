using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Microsoft.Identity.Client;
using System.Net.Http.Headers;
using System.Text.Json;

namespace TeamsBotTemplate.Bot
{
    public class Notifier
    {
        private readonly GraphClient _graphClient;
        private readonly IConfiguration _configuration;
        private readonly IBotFrameworkHttpAdapter _adapter;

        public Notifier(GraphClient graphClient, IConfiguration configuration, IBotFrameworkHttpAdapter adapter)
        {
            _graphClient = graphClient;
            _configuration = configuration;
            _adapter = adapter;
        }

        public async Task SendProactiveNotification(string upnOrOid, string tenantId, IActivity activityToSend, CancellationToken cancellationToken = default)
        {
            var appToken = await _graphClient.GetAppToken(tenantId);

            var conversationId = await _graphClient.GetChannelIdByUpn(upnOrOid, appToken);

            var credentials = new MicrosoftAppCredentials(_configuration["MicrosoftAppId"], _configuration["MicrosoftAppPassword"]);

            var connectorClient = new ConnectorClient(new Uri(_configuration["ServiceUrl"]), credentials);

            var members = await connectorClient.Conversations.GetConversationMembersAsync(conversationId);

            var conversationParameters = new ConversationParameters
            {
                IsGroup = false,
                Bot = new ChannelAccount
                {
                    Id = "28:" + _configuration["MicrosoftAppId"],
                    Name = "This is your bot!"
                },
                Members = new ChannelAccount[] { members[0] },
                TenantId = tenantId
            };

            await ((CloudAdapter)_adapter).CreateConversationAsync(credentials.MicrosoftAppId, null, _configuration["ServiceUrl"], credentials.OAuthScope, conversationParameters, async (t1, c1) =>
            {
                var conversationReference = t1.Activity.GetConversationReference();
                await ((CloudAdapter)_adapter).ContinueConversationAsync(_configuration["MicrosoftAppId"], conversationReference, async (t2, c2) =>
                {
                    await t2.SendActivityAsync(activityToSend, c2);
                }, cancellationToken);
            }, cancellationToken);
        }
    }

    public class MyBot : TeamsActivityHandler
    {
        const string GlobalAdministratorGuid = "62e90394-69f5-4237-9190-012177145e10";

        private readonly IStatePropertyAccessor<string?> _tokenAccessor;
        private readonly UserState _userState;
        private readonly GraphClient _graphClient;
        private readonly IConfiguration _configuration;

        public MyBot(UserState userState, GraphClient graphClient, IConfiguration configuration)
        {
            _tokenAccessor = userState.CreateProperty<string?>("UserToken");
            _userState = userState;
            _graphClient = graphClient;
            _configuration = configuration;
        }

        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            await base.OnTurnAsync(turnContext, cancellationToken);

            await _userState.SaveChangesAsync(turnContext, cancellationToken: cancellationToken);
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            var token = await _tokenAccessor.GetAsync(turnContext);

            if (string.IsNullOrEmpty(token))
            {
                await DoSSO(turnContext, cancellationToken);
                return;
            }

            var isAdmin = await IsAdmin(turnContext);

            if (isAdmin)
            {
                // Send adaptive card for admin consent
            }
            else
            {
                // Send response that you aren't an admin
            }

            var appToken = await _graphClient.GetAppToken(turnContext.Activity.Conversation.TenantId);

            var conversationId = await _graphClient.GetChannelIdByUpn("admin@perhamcorp2.onmicrosoft.com", appToken);

            var credentials = new MicrosoftAppCredentials(_configuration["MicrosoftAppId"], _configuration["MicrosoftAppPassword"]);

            var connectorClient = new ConnectorClient(new Uri(turnContext.Activity.ServiceUrl), credentials);

            var ttt = await connectorClient.Conversations.GetConversationMembersAsync(conversationId);

            var conversationParameters = new ConversationParameters
            {
                IsGroup = false,
                Bot = turnContext.Activity.Recipient,
                Members = new ChannelAccount[] { ttt[0] },
                TenantId = turnContext.Activity.Conversation.TenantId
            };

            await ((CloudAdapter)turnContext.Adapter).CreateConversationAsync(credentials.MicrosoftAppId, null, turnContext.Activity.ServiceUrl, credentials.OAuthScope, conversationParameters, async (t1, c1) =>
            {
                var conversationReference = t1.Activity.GetConversationReference();
                await ((CloudAdapter)turnContext.Adapter).ContinueConversationAsync(_configuration["MicrosoftAppId"], conversationReference, async (t2, c2) =>
                {
                    await t2.SendActivityAsync(MessageFactory.Text("WOOHOO!!!"), c2);
                }, cancellationToken);
            }, cancellationToken);
        }

        async Task<bool> IsAdmin(ITurnContext turnContext)
        {
            var token = await _tokenAccessor.GetAsync(turnContext);

            if (token is null)
            {
                return false;
            }

            var secToken = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(token);

            var widsValues = secToken.Claims.Where(x => x.Type == "wids").Select(x => x.Value);

            return widsValues.Any(x => x.Equals(GlobalAdministratorGuid, StringComparison.OrdinalIgnoreCase));
        }

        async Task DoSSO(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var attachment = new Attachment
            {
                Content = new OAuthCard
                {
                    TokenExchangeResource = new TokenExchangeResource
                    {
                        Id = Guid.NewGuid().ToString()
                    },
                    ConnectionName = "AAD"
                },
                ContentType = OAuthCard.ContentType
            };

            var activity = MessageFactory.Attachment(attachment);

            await turnContext.SendActivityAsync(activity, cancellationToken);
        }

        protected override async Task OnTeamsSigninVerifyStateAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
        {
            var activityValue = turnContext.Activity.Value?.ToString();

            if (activityValue is null)
            {
                return;
            }

            var result = JsonSerializer.Deserialize<TeamsSigninVerifyStateActivityValue>(activityValue, new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase});

            if (result is null)
            {
                return;
            }

            await _tokenAccessor.SetAsync(turnContext, result.Token, cancellationToken);

            if (result.Token is null)
            {
                return;
            }

            await turnContext.SendActivityAsync(MessageFactory.Text(result.Token));
        }
    }

    public class TeamsSigninVerifyStateActivityValue
    {
        public string? Id { get; set; }
        public string? Token { get; set; }
        public string? ConnectionName { get; set; }
    }

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
