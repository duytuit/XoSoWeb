using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using XoSoWeb.Chat;

// Isolated real Redis, real SignalR, real popup. Never use a production Redis here.
var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = root, WebRootPath = Path.Combine(root, "wwwroot"), EnvironmentName = "Development" });
builder.WebHost.UseUrls("http://127.0.0.1:5189");
builder.Logging.ClearProviders(); builder.Logging.AddConsole();
builder.Services.AddSingleton<IConnectionMultiplexer>(await ConnectionMultiplexer.ConnectAsync("127.0.0.1:6389,abortConnect=true"));
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
builder.Services.AddSingleton<ChatIdentity>(); builder.Services.AddSingleton<ChatStore>(); builder.Services.AddHostedService<ChatEvents>();
builder.Services.AddHostedService<ChatRetentionService>();
builder.Services.AddSignalR(o => { o.MaximumReceiveMessageSize = 4096; o.MaximumParallelInvocationsPerClient = 1; });
builder.Services.AddControllers().AddApplicationPart(typeof(ChatController).Assembly);
var app = builder.Build();
if (args.Contains("--retention-check"))
{
    var store = app.Services.GetRequiredService<ChatStore>();
    var db = app.Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
    void Check(bool condition, string label) { if (!condition) throw new Exception(label); Console.WriteLine("PASS: " + label); }
    var old = await store.Send("g-retention", Guid.NewGuid().ToString(), "Old message");
    var recent = await store.Send("g-retention", Guid.NewGuid().ToString(), "Recent message");
    var oldId = old.GetProperty("id").GetInt64();
    var node = System.Text.Json.Nodes.JsonNode.Parse(old.GetRawText());
    node["time"] = DateTimeOffset.UtcNow.AddDays(-4).ToString("O");
    await db.HashSetAsync(ChatStore.Prefix + "messages", oldId, node.ToJsonString());
    await db.HashSetAsync(ChatStore.Prefix + "reports", "old-fixture", DateTimeOffset.UtcNow.AddDays(-8).ToString("O"));
    await db.HashSetAsync(ChatStore.Prefix + "reports", "recent-fixture", DateTimeOffset.UtcNow.ToString("O"));
    await store.Cleanup();
    Check(!await db.HashExistsAsync(ChatStore.Prefix + "messages", oldId), "message older than three days removed");
    Check((await db.SortedSetScoreAsync(ChatStore.Prefix + "index", oldId)) == null, "expired message removed from pagination index");
    Check(await db.HashExistsAsync(ChatStore.Prefix + "messages", recent.GetProperty("id").GetInt64()), "recent message retained");
    Check(!await db.HashExistsAsync(ChatStore.Prefix + "reports", "old-fixture"), "report older than seven days removed");
    Check(await db.HashExistsAsync(ChatStore.Prefix + "reports", "recent-fixture"), "recent report retained");
    var clientId = Guid.NewGuid().ToString();
    var first = await store.Send("g-retention", clientId, "Retry check");
    var retry = await store.Send("g-retention", clientId, "Retry check");
    Check(first.GetProperty("id").GetInt64() == retry.GetProperty("id").GetInt64(), "retry remains idempotent");
    var ttl = await db.KeyTimeToLiveAsync(ChatStore.Prefix + "dedup:g-retention:" + clientId);
    Check(ttl > TimeSpan.FromDays(2.99) && ttl <= TimeSpan.FromDays(3), "new dedup TTL is three days");
    for (var i = 0; i < 2001; i++) await store.Send("g-retention", Guid.NewGuid().ToString(), "Cap fixture " + i);
    Check(await db.HashLengthAsync(ChatStore.Prefix + "messages") == ChatStore.MaxMessages, "message hash capped at 2000");
    Check(await db.SortedSetLengthAsync(ChatStore.Prefix + "index") == ChatStore.MaxMessages, "pagination index capped at 2000");
    var legacyKey = ChatStore.Prefix + "dedup:legacy-fixture";
    await db.StringSetAsync(legacyKey, "legacy", TimeSpan.FromDays(30));
    var retention = app.Services.GetServices<IHostedService>().OfType<ChatRetentionService>().Single();
    await retention.StartAsync(default);
    for (var i = 0; i < 100 && await db.KeyTimeToLiveAsync(legacyKey) > TimeSpan.FromDays(3); i++) await Task.Delay(100);
    await retention.StopAsync(default);
    Check(await db.KeyTimeToLiveAsync(legacyKey) <= TimeSpan.FromDays(3), "legacy dedup TTL shortened");
    await app.DisposeAsync();
    return;
}
app.UseStaticFiles();
app.UseMiddleware<ChatOriginMiddleware>();
app.MapControllers(); app.MapHub<ChatHub>("/community-chat/hub");
app.MapPost("/fixture/seed/{count:int}", async (int count, ChatStore store) => {
    for (var i = 0; i < Math.Clamp(count, 1, 220); i++) await store.Send("g-fixture", Guid.NewGuid().ToString(), "History fixture " + Guid.NewGuid().ToString("N"));
    return Results.Ok();
});
app.MapGet("/fixture", async () => Results.Content("<!doctype html><html lang='vi'><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1'><link rel='stylesheet' href='/lib/bootstrap/css/bootstrap.min.css'><link rel='stylesheet' href='/css/style.css'><body><h1>Chat integration fixture</h1><nav class='bottom-nav' style='position:fixed;bottom:0;height:60px'>Điều hướng</nav>" + (await File.ReadAllTextAsync(Path.Combine(root, "Views/Shared/_Chat.cshtml"))).Replace("~/", "/") + "</body></html>", "text/html"));
await app.RunAsync();
