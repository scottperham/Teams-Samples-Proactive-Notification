using Microsoft.Bot.Builder;
using IMiddleware = Microsoft.Bot.Builder.IMiddleware;

public class BotMiddleware : IMiddleware
{
    public async Task OnTurnAsync(ITurnContext turnContext, NextDelegate next, CancellationToken cancellationToken = default)
    {
        turnContext.OnSendActivities(async (ctx, activities, nextSend) =>
        {
            // run full pipeline
            var responses = await nextSend().ConfigureAwait(false);

            foreach (var activity in activities)
            {
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(activity, new System.Text.Json.JsonSerializerOptions
                {
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                    WriteIndented = true
                }));
            }

            return responses;
        });

        await next(cancellationToken);
    }
}