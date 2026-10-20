using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace XoSoWeb.Chat
{
    // All keys share a Redis Cluster hash slot. Lua commits mutations and publishes together.
    public class ChatStore
    {
        public const string Prefix = "community:{chat}:";
        public const string Channel = Prefix + "events";
        public const int MaxMessages = 2000;
        public static readonly TimeSpan MessageRetention = TimeSpan.FromDays(3);
        public static readonly TimeSpan ReportRetention = TimeSpan.FromDays(7);
        private readonly IDatabase db;
        public ChatStore(IConnectionMultiplexer redis) => db = redis.GetDatabase();
        public static readonly JsonSerializerOptions Json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        public async Task<ChatPerson> Person(string id)
        {
            var key = Prefix + "person:" + id;
            var raw = await db.StringGetAsync(key);
            if (raw.HasValue) return JsonSerializer.Deserialize<ChatPerson>(raw, Json);
            var animals = new[] { ("🐯", "Hổ"), ("🐼", "Gấu Trúc"), ("🦊", "Cáo"), ("🐱", "Mèo") };
            var animal = animals[System.Security.Cryptography.RandomNumberGenerator.GetInt32(animals.Length)];
            var adjectives = new[] { "Vui Vẻ", "May Mắn", "Nhanh Nhẹn", "Thân Thiện", "Dễ Mến" };
            var person = new ChatPerson { Id = id, Avatar = animal.Item1, Name = animal.Item2 + " " + adjectives[System.Security.Cryptography.RandomNumberGenerator.GetInt32(adjectives.Length)] + " #" + System.Security.Cryptography.RandomNumberGenerator.GetInt32(1000, 10000), Color = new[] { "#ffead5", "#e2f3e5", "#e6e8ff", "#fce1ed" }[System.Security.Cryptography.RandomNumberGenerator.GetInt32(4)] };
            await db.StringSetAsync(key, JsonSerializer.Serialize(person, Json), TimeSpan.FromDays(365), when: When.NotExists);
            return JsonSerializer.Deserialize<ChatPerson>(await db.StringGetAsync(key), Json);
        }
        public async Task Rename(string id, string name)
        {
            var person = await Person(id); person.Name = ChatIdentity.Validate(name, true);
            await db.StringSetAsync(Prefix + "person:" + id, JsonSerializer.Serialize(person, Json), TimeSpan.FromDays(365));
        }
        public async Task Guard(string id, string ip, bool sending = false)
        {
            if (await db.KeyExistsAsync(Prefix + "ban:" + id)) throw new HubException("Bạn đã bị khóa quyền chat.");
            var bucket = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / (sending ? 10 : 60);
            var script = "local a=redis.call('INCR',KEYS[1]); redis.call('EXPIRE',KEYS[1],70); local b=redis.call('INCR',KEYS[2]); redis.call('EXPIRE',KEYS[2],70); if a>tonumber(ARGV[1]) or b>tonumber(ARGV[2]) then return 0 end return 1";
            var scope = sending ? "send:" : "action:";
            var result = await db.ScriptEvaluateAsync(script, new RedisKey[] { Prefix + scope + id + ":" + bucket, Prefix + scope + "ip:" + ip + ":" + bucket }, new RedisValue[] { sending ? 5 : 90, sending ? 20 : 300 });
            if ((int)result == 0) throw new HubException("Thao tác quá nhanh. Vui lòng chờ một chút.");
        }
        public async Task<JsonElement> History(long before = 0, long after = 0)
        {
            await Cleanup();
            var script = "local ids; if tonumber(ARGV[2])>0 then ids=redis.call('ZRANGEBYSCORE',KEYS[1],'('..ARGV[2],'+inf','LIMIT',0,50) else ids=redis.call('ZREVRANGEBYSCORE',KEYS[1],ARGV[1],'-inf','LIMIT',0,50) end; local out={}; for _,id in ipairs(ids) do local m=redis.call('HGET',KEYS[2],id); if m then table.insert(out,cjson.decode(m)) end end; if #out==0 then return '[]' end; return cjson.encode(out)";
            return Parse(await db.ScriptEvaluateAsync(script, new RedisKey[] { Prefix + "index", Prefix + "messages" }, new RedisValue[] { before > 0 ? "(" + before : "+inf", after }));
        }
        public async Task<JsonElement> Send(string id, string clientId, string text)
        {
            if (!Guid.TryParse(clientId, out var guid)) throw new HubException("Mã tin nhắn không hợp lệ.");
            text = ChatIdentity.Validate(text, false);
            var person = await Person(id);
            var payload = JsonSerializer.Serialize(new { user = person, clientId = guid.ToString(), text, time = DateTimeOffset.UtcNow, reactions = new object() }, Json);
            var script = @"local old=redis.call('GET',KEYS[4]); if old then local previous=cjson.decode(old); return redis.call('HGET',KEYS[2],previous.id) or old end
local m=cjson.decode(ARGV[1]); local id=redis.call('INCR',KEYS[1]); m.id=id; local raw=cjson.encode(m);
redis.call('HSET',KEYS[2],id,raw); redis.call('ZADD',KEYS[3],id,id); redis.call('SET',KEYS[4],raw,'EX',259200);
local expired=redis.call('ZRANGE',KEYS[3],0,-2001); for _,v in ipairs(expired) do redis.call('HDEL',KEYS[2],v); redis.call('ZREM',KEYS[3],v) end;
redis.call('PUBLISH',ARGV[2],cjson.encode({kind='message',message=m})); return raw";
            return Parse(await db.ScriptEvaluateAsync(script, new RedisKey[] { Prefix + "sequence", Prefix + "messages", Prefix + "index", Prefix + "dedup:" + id + ":" + guid }, new RedisValue[] { payload, Channel }));
        }
        public async Task Reaction(string id, long messageId, string emoji)
        {
            if (!new[] { "👍", "❤️", "😂", "🎉" }.Contains(emoji)) throw new HubException("Reaction không hợp lệ.");
            var script = @"local raw=redis.call('HGET',KEYS[1],ARGV[1]); if not raw then return 0 end
local m=cjson.decode(raw); if m.deleted then return 0 end; local users=m.reactions[ARGV[2]] or {}; if users[ARGV[3]] then users[ARGV[3]]=nil else users[ARGV[3]]=true end; m.reactions[ARGV[2]]=users;
redis.call('HSET',KEYS[1],ARGV[1],cjson.encode(m)); redis.call('PUBLISH',ARGV[4],cjson.encode({kind='update',message=m})); return 1";
            if ((int)await db.ScriptEvaluateAsync(script, new RedisKey[] { Prefix + "messages" }, new RedisValue[] { messageId, emoji, id, Channel }) == 0) throw new HubException("Tin nhắn không còn tồn tại.");
        }
        public async Task Report(string id, long messageId)
        {
            if (!await db.HashExistsAsync(Prefix + "messages", messageId)) throw new HubException("Tin nhắn không tồn tại.");
            await db.HashSetAsync(Prefix + "reports", messageId + ":" + id, DateTimeOffset.UtcNow.ToString("O"));
        }
        public async Task Cleanup()
        {
            // Existing UTC ISO timestamps are reused, so old data needs no schema migration.
            // Apply the count cap first to bound the message scan to 2,000 records.
            var script = @"local excess=redis.call('ZRANGE',KEYS[1],0,-2001);
for _,id in ipairs(excess) do redis.call('HDEL',KEYS[2],id); redis.call('ZREM',KEYS[1],id) end;
local ids=redis.call('ZRANGE',KEYS[1],0,-1);
for _,id in ipairs(ids) do local raw=redis.call('HGET',KEYS[2],id);
if not raw or string.sub(cjson.decode(raw).time,1,19)<=ARGV[1] then redis.call('HDEL',KEYS[2],id); redis.call('ZREM',KEYS[1],id) end end;
local reports=redis.call('HGETALL',KEYS[3]); for i=1,#reports,2 do
if string.sub(reports[i+1],1,19)<=ARGV[2] then redis.call('HDEL',KEYS[3],reports[i]) end end; return 1";
            var now = DateTimeOffset.UtcNow;
            await db.ScriptEvaluateAsync(script, new RedisKey[] { Prefix + "index", Prefix + "messages", Prefix + "reports" },
                new RedisValue[] { now.Subtract(MessageRetention).ToString("yyyy-MM-dd'T'HH:mm:ss"), now.Subtract(ReportRetention).ToString("yyyy-MM-dd'T'HH:mm:ss") });
        }
        public async Task Delete(long messageId)
        {
            await db.ScriptEvaluateAsync(@"local raw=redis.call('HGET',KEYS[1],ARGV[1]); if not raw then return 0 end; local m=cjson.decode(raw); m.text='Tin nhắn đã được quản trị viên xóa'; m.deleted=true; m.reactions={}; redis.call('HSET',KEYS[1],ARGV[1],cjson.encode(m)); redis.call('PUBLISH',ARGV[2],cjson.encode({kind='update',message=m})); return 1", new RedisKey[] { Prefix + "messages" }, new RedisValue[] { messageId, Channel });
        }
        public Task Ban(string id) => db.StringSetAsync(Prefix + "ban:" + id, "1");
        public async Task Presence(string connection, string id, bool remove = false)
        {
            var script = @"if ARGV[3]=='1' then redis.call('ZREM',KEYS[1],ARGV[1]) else redis.call('ZADD',KEYS[1],ARGV[2],ARGV[1]) end;
redis.call('ZREMRANGEBYSCORE',KEYS[1],'-inf',ARGV[4]); local all=redis.call('ZRANGE',KEYS[1],0,-1); local users={}; for _,v in ipairs(all) do users[string.match(v,'^(.-)|')]=true end; local n=0; for _ in pairs(users) do n=n+1 end; redis.call('PUBLISH',ARGV[5],cjson.encode({kind='presence',online=n})); return n";
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await db.ScriptEvaluateAsync(script, new RedisKey[] { Prefix + "presence" }, new RedisValue[] { id + "|" + connection, now, remove ? "1" : "0", now - 75, Channel });
        }
        private static JsonElement Parse(RedisResult result) => JsonDocument.Parse((string)result).RootElement.Clone();
    }
}
