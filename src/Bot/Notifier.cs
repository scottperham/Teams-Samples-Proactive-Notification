using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;

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

        public async Task<bool> SendProactiveNotification(string upnOrOid, string tenantId, IActivity activityToSend, CancellationToken cancellationToken = default)
        {
            var appToken = await _graphClient.GetAppToken(tenantId);

            var conversationId = await _graphClient.GetChannelIdByUpn(upnOrOid, appToken);

            if (conversationId == null)
            {
                return false;
            }

            var credentials = new MicrosoftAppCredentials(_configuration["MicrosoftAppId"], _configuration["MicrosoftAppPassword"]);

            var connectorClient = new ConnectorClient(new Uri(_configuration["ServiceUrl"]), credentials);

            var members = await connectorClient.Conversations.GetConversationMembersAsync(conversationId);

            var conversationParameters = new ConversationParameters
            {
                IsGroup = false,
                Bot = new ChannelAccount
                {
                    Id = "28:" + credentials.MicrosoftAppId,
                    Name = "This is your bot!"
                },
                Members = new ChannelAccount[] { members[0] },
                TenantId = tenantId
            };

            await ((CloudAdapter)_adapter).CreateConversationAsync(credentials.MicrosoftAppId, null, _configuration["ServiceUrl"], credentials.OAuthScope, conversationParameters, async (t1, c1) =>
            {
                var conversationReference = t1.Activity.GetConversationReference();
                await ((CloudAdapter)_adapter).ContinueConversationAsync(credentials.MicrosoftAppId, conversationReference, async (t2, c2) =>
                {
                    await t2.SendActivityAsync(activityToSend, c2);
                }, cancellationToken);
            }, cancellationToken);

            return true;
        }
    }
}
