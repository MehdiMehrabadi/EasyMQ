using Api.Models;
using Common;
using EasyMQ.Abstractions;
using Mapster;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("message")]
    [ApiController]
    public class NotificationController : Controller
    {
        private readonly IMessagePublisher _messagePublisher;

        public NotificationController(IMessagePublisher messagePublisher)
        {
            this._messagePublisher = messagePublisher;
        }
        
        [HttpPost]
        public async Task<IActionResult> SendMessageAsync([FromBody] MessageRequest request)
        {
            await _messagePublisher.PublishAsync(request.Adapt<MessageModel>(), priority: 1, keepAliveTime: TimeSpan.FromSeconds(120));

            return Ok();
        }
    }
}
