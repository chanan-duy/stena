using StackExchange.Redis;
using Stena.Canvas.Domain;
using Stena.Contracts;

namespace Stena.Canvas.Redis;

public class WallStore(IConnectionMultiplexer redis)
{
	private const string BitmapScript = """
		local id = '0-0'
		if redis.call('EXISTS', KEYS[2]) == 1 then
			local info = redis.call('XINFO', 'STREAM', KEYS[2])
			for i = 1, #info, 2 do
				if info[i] == 'last-generated-id' then
					id = info[i + 1]
				end
			end
		end
		return {redis.call('GET', KEYS[1]), id}
		""";

	public async Task SyncAsync(Wall wall)
	{
		var db = redis.GetDatabase();
		await db.HashSetAsync(Keys.Wall(wall.Id), [
			new HashEntry("w", wall.Width),
			new HashEntry("h", wall.Height),
			new HashEntry("colors", wall.Palette.Length),
			new HashEntry("charge_ms", wall.ChargeSeconds * 1000L),
			new HashEntry("max", wall.MaxCharges),
			new HashEntry("start_ms", wall.StartsAt.ToUnixTimeMilliseconds()),
			new HashEntry("end_ms", wall.EndsAt.ToUnixTimeMilliseconds()),
		]);
		if (await db.StringLengthAsync(Keys.Pixels(wall.Id)) != wall.Width * wall.Height)
		{
			await db.StringSetAsync(Keys.Pixels(wall.Id), new byte[wall.Width * wall.Height]);
		}

		await db.SetAddAsync(Keys.Walls, wall.Id);
	}

	public async Task<(byte[] Pixels, string StreamId)?> BitmapAsync(int wallId)
	{
		var res = (RedisResult[])(await redis.GetDatabase().ScriptEvaluateAsync(BitmapScript, [Keys.Pixels(wallId), Keys.Stream(wallId)]))!;
		return res[0].IsNull ? null : ((byte[])res[0]!, (string)res[1]!);
	}
}
