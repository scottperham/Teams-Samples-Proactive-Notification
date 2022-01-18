using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Schema;
using TeamsBotTemplate.Bot;

namespace TeamsBotTemplate.Controllers
{
    [ApiController]
    public class BotController : ControllerBase
    {
        private readonly IBot _bot;
        private readonly IBotFrameworkHttpAdapter _adapter;
        private readonly Notifier _notifier;

        public BotController(IBot bot, IBotFrameworkHttpAdapter adapter, Notifier notifier)
        {
            _bot = bot;
            _adapter = adapter;
            _notifier = notifier;
        }

        [Route("api/messages")]
        [HttpPost]
        public Task PostAsync(CancellationToken cancellationToken)
            => _adapter.ProcessAsync(Request, Response, _bot, cancellationToken);

        [Route("api/postmessage")]
        [HttpPost]
        public async Task PostMessageAsync([FromBody] ProactiveMessageRequest request, CancellationToken cancellationToken)
        {
            var activity = MessageFactory.Text(request.Message);

            await _notifier.SendProactiveNotification(request.Id, request.TenantId, activity, cancellationToken);
        }
    }

    public class ProactiveMessageRequest
    {
        public string Id { get; set; }
        public string Message { get; set; }
        public string TenantId { get; set; }
    }
}
