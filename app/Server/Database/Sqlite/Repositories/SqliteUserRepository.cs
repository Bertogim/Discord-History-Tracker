using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DHT.Server.Data;
using DHT.Server.Database.Repositories;
using DHT.Server.Database.Sqlite.Utils;
using DHT.Server.Download;
using DHT.Utils.Logging;
using Microsoft.Data.Sqlite;

namespace DHT.Server.Database.Sqlite.Repositories;

sealed class SqliteUserRepository : BaseSqliteRepository, IUserRepository {
	private static readonly Log Log = Log.ForType<SqliteUserRepository>();
	
	private readonly SqliteConnectionPool pool;
	private readonly SqliteDownloadRepository downloads;
	
	public SqliteUserRepository(SqliteConnectionPool pool, SqliteDownloadRepository downloads) : base(Log) {
		this.pool = pool;
		this.downloads = downloads;
	}
	
	public async Task Add(IReadOnlyList<User> users) {
		await using (var conn = await pool.Take()) {
			await conn.BeginTransactionAsync();
			
			await using var cmd = conn.Upsert("users", [
				("id", SqliteType.Integer),
				("name", SqliteType.Text),
				("display_name", SqliteType.Text),
				("avatar_url", SqliteType.Text),
				("discriminator", SqliteType.Text),
			]);
			
			await using var downloadCollector = new SqliteDownloadRepository.NewDownloadCollector(downloads, conn);
			
			foreach (User user in users) {
				cmd.Set(":id", user.Id);
				cmd.Set(":name", user.Name);
				cmd.Set(":display_name", user.DisplayName);
				cmd.Set(":avatar_url", user.AvatarUrl);
				cmd.Set(":discriminator", user.Discriminator);
				await cmd.ExecuteNonQueryAsync();
				
				if (user.AvatarUrl is {} avatarUrl) {
					await downloadCollector.Add(DownloadLinkExtractor.FromUserAvatar(user.Id, avatarUrl));
				}
			}
			
			await conn.CommitTransactionAsync();
			downloadCollector.OnCommitted();
		}
		
		UpdateTotalCount();
	}
	
	public override async Task<long> Count(CancellationToken cancellationToken) {
		await using var conn = await pool.Take();
		return await conn.ExecuteReaderAsync("SELECT COUNT(*) FROM users", static reader => reader?.GetInt64(0) ?? 0L, cancellationToken);
	}
	
	public async IAsyncEnumerable<User> Get([EnumeratorCancellation] CancellationToken cancellationToken) {
		await using var conn = await pool.Take();
		
		await using var cmd = conn.Command("SELECT id, name, display_name, avatar_url, discriminator FROM users");
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
		
		while (await reader.ReadAsync(cancellationToken)) {
			yield return new User {
				Id = reader.GetUint64(0),
				Name = reader.GetString(1),
				DisplayName = reader.IsDBNull(2) ? null : reader.GetString(2),
				AvatarUrl = reader.IsDBNull(3) ? null : reader.GetString(3),
				Discriminator = reader.IsDBNull(4) ? null : reader.GetString(4),
			};
		}
	}
}
