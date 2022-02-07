using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;

namespace TeamsBotTemplate
{
    public class TeamsBotHttpAdapter : CloudAdapter
    {
        public TeamsBotHttpAdapter(IWebHostEnvironment env, BotFrameworkAuthentication auth, ILogger<TeamsBotHttpAdapter> logger)
            : base(auth, logger)
        {
            OnTurnError = async (turnContext, exception) =>
            {
                logger.LogCritical(exception, exception.Message);
                await turnContext.SendActivityAsync("Sorry, it looks like something went wrong.");

                if (env.IsDevelopment())
                {
                    await turnContext.SendActivityAsync(exception.ToString());
                }
            };

            Use(new ShowTypingMiddleware());
        }
    }
}
