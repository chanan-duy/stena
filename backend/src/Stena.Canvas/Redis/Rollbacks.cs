using StackExchange.Redis;
using Stena.Contracts;

namespace Stena.Canvas.Redis;

public class Rollbacks(IConnectionMultiplexer redis)
{
	private static readonly TimeSpan LockTime = TimeSpan.FromMinutes(1);

	private const string LockScript = """
		if redis.call('SET', KEYS[1], ARGV[1], 'NX', 'PX', ARGV[2]) or redis.call('GET', KEYS[1]) == ARGV[1] then
			return 1
		end
		return 0
		""";

	private const string UnlockScript = """
		if redis.call('GET', KEYS[1]) == ARGV[1] then
			redis.call('DEL', KEYS[1])
		end
		return 0
		""";

	private const string ApplyScript = """
		local saved = redis.call('GET', KEYS[4])
		if saved then
			return tonumber(saved)
		end
		if redis.call('GET', KEYS[1]) ~= ARGV[1] then
			return -1
		end

		local w = tonumber(redis.call('HGET', KEYS[5], 'w'))
		local x0, y0, aw, ah = tonumber(ARGV[2]), tonumber(ARGV[3]), tonumber(ARGV[4]), tonumber(ARGV[5])
		local area = ARGV[6]
		local changed = 0
		for row = 0, ah - 1 do
			local offset = (y0 + row) * w + x0
			local current = redis.call('GETRANGE', KEYS[2], offset, offset + aw - 1)
			for col = 1, aw do
				local target = string.sub(area, row * aw + col, row * aw + col)
				if string.sub(current, col, col) ~= target then
					redis.call('SETRANGE', KEYS[2], offset + col - 1, target)
					redis.call('XADD', KEYS[3], '*', 'x', x0 + col - 1, 'y', y0 + row, 'c', string.byte(target), 'u', ARGV[7], 'n', ARGV[8])
					changed = changed + 1
				end
			end
		end
		redis.call('SET', KEYS[4], changed, 'EX', 86400)
		return changed
		""";

	public async Task<bool> LockAsync(int wallId, Guid rollbackId)
	{
		var res = await redis.GetDatabase().ScriptEvaluateAsync(LockScript, [Keys.Lock(wallId)], [rollbackId.ToString(), (long)LockTime.TotalMilliseconds]);
		return (int)res == 1;
	}

	public Task UnlockAsync(int wallId, Guid rollbackId)
	{
		return redis.GetDatabase().ScriptEvaluateAsync(UnlockScript, [Keys.Lock(wallId)], [rollbackId.ToString()]);
	}

	public async Task<int?> ApplyAsync(Guid rollbackId, int wallId, int x, int y, int width, int height, byte[] pixels, Guid adminId, string adminName)
	{
		RedisKey[] keys = [Keys.Lock(wallId), Keys.Pixels(wallId), Keys.Stream(wallId), Keys.Rollback(rollbackId), Keys.Wall(wallId)];
		RedisValue[] args = [rollbackId.ToString(), x, y, width, height, pixels, adminId.ToString(), adminName];
		var changed = (int)await redis.GetDatabase().ScriptEvaluateAsync(ApplyScript, keys, args);
		return changed < 0 ? null : changed;
	}
}
