using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace XoSoWeb.Chat
{
    public class ChatRetentionService : BackgroundService
    {
        private readonly IServiceProvider services;
        private readonly ILogger<ChatRetentionService> logger;
        public ChatRetentionService(IServiceProvider services, ILogger<ChatRetentionService> logger)
        { this.services = services; this.logger = logger; }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            await Task.Yield();
            var migrated = false;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await services.GetRequiredService<ChatStore>().Cleanup();
                    if (!migrated)
                    {
                        var redis = services.GetRequiredService<IConnectionMultiplexer>();
                        var db = redis.GetDatabase();
                        // SCAN only the chat dedup namespace, never flush or change other Redis data.
                        foreach (var endpoint in redis.GetEndPoints())
                        {
                            var server = redis.GetServer(endpoint);
                            if (server.IsReplica) continue;
                            foreach (var key in server.Keys(db.Database, ChatStore.Prefix + "dedup:*", pageSize: 100))
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                await db.ScriptEvaluateAsync("local ttl=redis.call('TTL',KEYS[1]); if ttl>259200 or ttl==-1 then redis.call('EXPIRE',KEYS[1],259200) end; return 1", new RedisKey[] { key });
                            }
                        }
                        migrated = true;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
                catch (Exception ex) { logger.LogWarning(ex, "Community chat retention cleanup failed; retrying."); }
                try { await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken); }
                catch (OperationCanceledException) { return; }
            }
        }
    }
}
