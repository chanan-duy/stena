using StackExchange.Redis;
using Stena.Contracts;

namespace Stena.Canvas.Redis;

public enum PlaceStatus
{
	Ok,
	NotFound,
	NotLive,
	OutOfRange,
	Banned,
	Locked,
	NoCharges,
}

public record PlaceResult(PlaceStatus Status, int Charges, long NextChargeMs, string? StreamId);

public record Charges(int Count, long NextChargeMs);

public class Placement(IConnectionMultiplexer redis)
{
	private const string Refill = """
		local function refill(charge, max, bucket, now)
			local n, ts = tonumber(bucket[1]), tonumber(bucket[2])
			if not n or n >= max then
				return max, now
			end
			local gained = math.floor(math.max(0, now - ts) / charge)
			if n + gained >= max then
				return max, now
			end
			return n + gained, ts + gained * charge
		end

		local function next_in(n, max, ts, charge, now)
			if n >= max then
				return 0
			end
			return ts + charge - now
		end

		local t = redis.call('TIME')
		local now = tonumber(t[1]) * 1000 + math.floor(tonumber(t[2]) / 1000)
		""";

	private const string PlaceScript = Refill + """
		local saved = redis.call('GET', KEYS[6])
		if saved then
			return cjson.decode(saved)
		end

		local cfg = redis.call('HMGET', KEYS[1], 'w', 'h', 'colors', 'charge_ms', 'max', 'start_ms', 'end_ms')
		if not cfg[1] then
			return {'NotFound'}
		end
		local w, h, colors = tonumber(cfg[1]), tonumber(cfg[2]), tonumber(cfg[3])
		local charge, max = tonumber(cfg[4]), tonumber(cfg[5])
		if now < tonumber(cfg[6]) or now >= tonumber(cfg[7]) then
			return {'NotLive'}
		end

		local x, y, c = tonumber(ARGV[1]), tonumber(ARGV[2]), tonumber(ARGV[3])
		if x < 0 or x >= w or y < 0 or y >= h or c < 0 or c >= colors then
			return {'OutOfRange'}
		end
		if redis.call('SISMEMBER', KEYS[7], ARGV[4]) == 1 then
			return {'Banned'}
		end
		if redis.call('EXISTS', KEYS[4]) == 1 then
			return {'Locked'}
		end

		local n, ts = refill(charge, max, redis.call('HMGET', KEYS[5], 'n', 'ts'), now)
		if n < 1 then
			return {'NoCharges', 0, next_in(n, max, ts, charge, now)}
		end
		n = n - 1

		redis.call('HSET', KEYS[5], 'n', n, 'ts', ts)
		redis.call('SETRANGE', KEYS[2], y * w + x, string.char(c))
		local id = redis.call('XADD', KEYS[3], '*', 'x', x, 'y', y, 'c', c, 'u', ARGV[4], 'n', ARGV[5])
		local res = {'Ok', n, next_in(n, max, ts, charge, now), id}
		redis.call('SET', KEYS[6], cjson.encode(res), 'EX', 600)
		return res
		""";

	private const string ChargesScript = Refill + """
		local cfg = redis.call('HMGET', KEYS[1], 'charge_ms', 'max')
		if not cfg[1] then
			return nil
		end
		local charge, max = tonumber(cfg[1]), tonumber(cfg[2])
		local n, ts = refill(charge, max, redis.call('HMGET', KEYS[2], 'n', 'ts'), now)
		return {n, next_in(n, max, ts, charge, now)}
		""";

	public async Task<PlaceResult> PlaceAsync(int wallId, int x, int y, int color, Guid requestId, string userId, string userName)
	{
		RedisKey[] keys =
		[
			Keys.Wall(wallId), Keys.Pixels(wallId), Keys.Stream(wallId), Keys.Lock(wallId),
			Keys.Charges(wallId, userId), Keys.Request(wallId, userId, requestId), Keys.Bans,
		];
		var res = (RedisResult[])(await redis.GetDatabase().ScriptEvaluateAsync(PlaceScript, keys, [x, y, color, userId, userName]))!;
		return new PlaceResult(
			Enum.Parse<PlaceStatus>((string)res[0]!),
			res.Length > 1 ? (int)res[1] : 0,
			res.Length > 2 ? (long)res[2] : 0,
			res.Length > 3 ? (string?)res[3] : null);
	}

	public async Task<Charges?> ChargesAsync(int wallId, string userId)
	{
		var res = await redis.GetDatabase().ScriptEvaluateAsync(ChargesScript, [Keys.Wall(wallId), Keys.Charges(wallId, userId)]);
		return res.IsNull ? null : new Charges((int)res[0], (long)res[1]);
	}
}
