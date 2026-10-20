using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace XoSoWeb.Chat
{
    [ApiController, Route("community-chat")]
    public class ChatController : ControllerBase
    {
        [HttpPost("identity")]
        public async Task<IActionResult> Identity([FromServices] ChatIdentity identity, [FromServices] ChatStore store)
        {
            Response.Headers["Cache-Control"] = "no-store";
            var id = identity.Read(HttpContext);
            await store.Guard(id ?? "new-guest", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
            id ??= identity.Issue(HttpContext);
            return Ok(await store.Person(id));
        }
    }

    public class ChatHub : Hub
    {
        private readonly ChatIdentity identity;
        private readonly ChatStore store;
        public ChatHub(ChatIdentity identity, ChatStore store) { this.identity = identity; this.store = store; }
        private string Id => identity.Read(Context.GetHttpContext()) ?? throw new HubException("Hãy tải lại danh tính chat.");
        private Task Guard(bool sending = false) => store.Guard(Id, Context.GetHttpContext().Connection.RemoteIpAddress?.ToString() ?? "unknown", sending);
        public override async Task OnConnectedAsync()
        {
            await Guard();
            await store.Presence(Context.ConnectionId, Id);
            await base.OnConnectedAsync();
        }
        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var id = identity.Read(Context.GetHttpContext());
            if (id != null) await store.Presence(Context.ConnectionId, id, true);
            await base.OnDisconnectedAsync(exception);
        }
        public async Task<object> History(long before = 0, long after = 0) { await Guard(); return await store.History(Math.Max(0, before), Math.Max(0, after)); }
        public async Task<object> Send(string clientId, string text) { await Guard(true); return await store.Send(Id, clientId, text); }
        public async Task React(long messageId, string emoji) { await Guard(); await store.Reaction(Id, messageId, emoji); }
        public async Task<object> Rename(string name) { await Guard(); await store.Rename(Id, name); return await store.Person(Id); }
        public async Task Report(long messageId) { await Guard(); await store.Report(Id, messageId); }
        public async Task Ping() { await Guard(); await store.Presence(Context.ConnectionId, Id); }
        // Never accept roles from the client or from the guest cookie.
        private void RequireAdmin()
        {
            if (Context.User?.Identity?.IsAuthenticated != true || !Context.User.IsInRole("ChatAdmin"))
                throw new HubException("Chỉ quản trị viên được phép thực hiện thao tác này.");
        }
        public async Task Delete(long messageId) { RequireAdmin(); await Guard(); await store.Delete(messageId); }
        public async Task Ban(string userId) { RequireAdmin(); await Guard(); if (userId == null || userId.Length > 80) throw new HubException("Định danh không hợp lệ."); await store.Ban(userId); }
    }

    // Redis pub/sub fans committed events to each instance's local SignalR connections.
    public class ChatEvents : BackgroundService
    {
        private readonly IServiceProvider services;
        private readonly IHubContext<ChatHub> hub;
        private readonly ILogger<ChatEvents> logger;
        public ChatEvents(IServiceProvider services, IHubContext<ChatHub> hub, ILogger<ChatEvents> logger) { this.services = services; this.hub = hub; this.logger = logger; }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Do not block the site's startup when the optional chat transport is unavailable.
            await Task.Yield();
            while (!stoppingToken.IsCancellationRequested)
            {
                try { await Subscribe(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception ex) { logger.LogWarning(ex, "Community chat subscription unavailable; retrying."); }
                try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
                catch (OperationCanceledException) { return; }
            }
        }
        private async Task Subscribe(CancellationToken stoppingToken)
        {
            var redis = services.GetRequiredService<IConnectionMultiplexer>();
            var queue = await redis.GetSubscriber().SubscribeAsync(RedisChannel.Literal(ChatStore.Channel));
            queue.OnMessage(async message =>
            {
                try { await hub.Clients.All.SendAsync("Event", System.Text.Json.JsonDocument.Parse(message.Message.ToString()).RootElement.Clone(), stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
                catch (Exception ex) { logger.LogWarning(ex, "Community chat event delivery failed."); }
            });
            try { await Task.Delay(Timeout.Infinite, stoppingToken); }
            catch (OperationCanceledException) { }
            finally { await queue.UnsubscribeAsync(); }
        }
    }
}
