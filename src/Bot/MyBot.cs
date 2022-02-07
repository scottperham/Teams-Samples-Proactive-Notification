using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using System.Text.Json;

namespace TeamsBotTemplate.Bot
{
    public class MyBot : TeamsActivityHandler
    {
        const string GlobalAdministratorGuid = "62e90394-69f5-4237-9190-012177145e10";

        private readonly IStatePropertyAccessor<string?> _tokenAccessor;
        private readonly UserState _userState;
        private readonly Notifier _notifier;

        public MyBot(UserState userState, Notifier notifier)
        {
            _tokenAccessor = userState.CreateProperty<string?>("UserToken");
            _userState = userState;
            _notifier = notifier;
        }

        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            await base.OnTurnAsync(turnContext, cancellationToken);

            await _userState.SaveChangesAsync(turnContext, cancellationToken: cancellationToken);
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            var text = turnContext.Activity.Text.Trim();

            if (text.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                var message = @"

    You can proactively send a notification to a user in the current tenant using the following command:
        notify {userId or upn}

    Alternatively, you can send a notification to a user in another tenant using an HTTP POST request:
        POST api/postmessage
        {
            ""id"": ""{userId or upn}"",
            ""tenantId"": ""{tenantId}""
        }

    (Coming soon) You can install the bot for a user in this tenant using the following command:
        install {userid or upn}

    (Coming soon) Or to install this bot for a user in another tenant use the following HTTP POST request:
        POST api/install
        {
            ""id"": ""{userId or upn}"",
            ""tenantId"": ""{tenantId}""
        }";

                await turnContext.SendActivityAsync(MessageFactory.Text(message));

                return;
            }


            var token = await _tokenAccessor.GetAsync(turnContext);

            if (string.IsNullOrEmpty(token))
            {
                await DoSSO(turnContext, cancellationToken);
                return;
            }

            if (text.StartsWith("notify", StringComparison.OrdinalIgnoreCase))
            {
                var tenantId = turnContext.Activity.Conversation.TenantId;
                var user = string.Join(' ', text.Split(' ').Skip(1));

                var success = await _notifier.SendProactiveNotification(user, tenantId, MessageFactory.Text("This is a proactive message from within Teams"), cancellationToken);

                if (!success)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text(user + " doesn't seem to have the bot installed"));
                }

                return;
            }

            if (text.StartsWith("install", StringComparison.OrdinalIgnoreCase))
            {
                var isAdmin = await IsAdmin(turnContext);

                var tenantId = turnContext.Activity.Conversation.TenantId;
                var user = string.Join(' ', text.Split(' ').Skip(1));

                // Attempt proactive installation
                // Check for admin consent requirement
                // Prompt for admin consent
                // Try again

                await turnContext.SendActivityAsync(MessageFactory.Text("Proactive installation is coming soon!"));

                return;
            }

            await turnContext.SendActivityAsync(MessageFactory.Text("Sorry, I didn't understand that command, type 'help' to see what you can send"));
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

            await turnContext.SendActivityAsync(MessageFactory.Text("You have been signed in, please send your command again"));
        }
    }

    public class TeamsSigninVerifyStateActivityValue
    {
        public string? Id { get; set; }
        public string? Token { get; set; }
        public string? ConnectionName { get; set; }
    }
}
