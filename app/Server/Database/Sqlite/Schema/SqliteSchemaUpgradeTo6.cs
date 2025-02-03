using System.Collections.Generic;
using System.Threading.Tasks;
using DHT.Server.Database.Sqlite.Utils;
using DHT.Server.Download;
using Microsoft.Data.Sqlite;

namespace DHT.Server.Database.Sqlite.Schema;

sealed class SqliteSchemaUpgradeTo6 : ISchemaUpgrade {
	async Task ISchemaUpgrade.Run(ISqliteConnection conn, ISchemaUpgradeCallbacks.IProgressReporter reporter) {
		await reporter.MainWork("Applying schema changes...", finishedItems: 0, totalItems: 3);
		await conn.ExecuteAsync("ALTER TABLE attachments ADD download_url TEXT");
		await conn.ExecuteAsync("ALTER TABLE downloads ADD download_url TEXT");
		
		await reporter.MainWork("Updating attachments...", finishedItems: 1, totalItems: 3);
		await NormalizeAttachmentUrls(conn, reporter);
		
		await reporter.MainWork("Updating downloads...", finishedItems: 2, totalItems: 3);
		await NormalizeDownloadUrls(conn, reporter);
		
		await reporter.MainWork("Applying schema changes...", finishedItems: 3, totalItems: 3);
		await conn.ExecuteAsync("ALTER TABLE attachments RENAME COLUMN url TO normalized_url");
		await conn.ExecuteAsync("ALTER TABLE downloads RENAME COLUMN url TO normalized_url");
	}
	
	private async Task NormalizeAttachmentUrls(ISqliteConnection conn, ISchemaUpgradeCallbacks.IProgressReporter reporter) {
		await reporter.SubWork("Preparing attachments...", finishedItems: 0, totalItems: 0);
		
		var normalizedUrls = new Dictionary<long, string>();
		
		await using (var selectCmd = conn.Command("SELECT attachment_id, url FROM attachments")) {
			await using var reader = await selectCmd.ExecuteReaderAsync();
			
			while (await reader.ReadAsync()) {
				long attachmentId = reader.GetInt64(0);
				string originalUrl = reader.GetString(1);
				normalizedUrls[attachmentId] = DiscordCdn.NormalizeUrl(originalUrl);
			}
		}
		
		await conn.BeginTransactionAsync();
		
		int totalUrls = normalizedUrls.Count;
		int processedUrls = -1;
		
		await using (var updateCmd = conn.Command("UPDATE attachments SET download_url = url, url = :normalized_url WHERE attachment_id = :attachment_id")) {
			updateCmd.Add(":attachment_id", SqliteType.Integer);
			updateCmd.Add(":normalized_url", SqliteType.Text);
			
			foreach ((long attachmentId, string normalizedUrl) in normalizedUrls) {
				if (++processedUrls % 1000 == 0) {
					await reporter.SubWork("Updating URLs...", processedUrls, totalUrls);
				}
				
				updateCmd.Set(":attachment_id", attachmentId);
				updateCmd.Set(":normalized_url", normalizedUrl);
				await updateCmd.ExecuteNonQueryAsync();
			}
		}
		
		await reporter.SubWork("Updating URLs...", totalUrls, totalUrls);
		
		await conn.CommitTransactionAsync();
	}
	
	private async Task NormalizeDownloadUrls(ISqliteConnection conn, ISchemaUpgradeCallbacks.IProgressReporter reporter) {
		await reporter.SubWork("Preparing downloads...", finishedItems: 0, totalItems: 0);
		
		var normalizedUrlsToOriginalUrls = new Dictionary<string, string>();
		var duplicateUrlsToDelete = new HashSet<string>();
		
		await using (var selectCmd = conn.Command("SELECT url FROM downloads ORDER BY CASE WHEN status = 200 THEN 0 ELSE 1 END")) {
			await using var reader = await selectCmd.ExecuteReaderAsync();
			
			while (await reader.ReadAsync()) {
				string originalUrl = reader.GetString(0);
				string normalizedUrl = DiscordCdn.NormalizeUrl(originalUrl);
				
				if (!normalizedUrlsToOriginalUrls.TryAdd(normalizedUrl, originalUrl)) {
					duplicateUrlsToDelete.Add(originalUrl);
				}
			}
		}
		
		await conn.ExecuteAsync("PRAGMA cache_size = -20000");
		await conn.BeginTransactionAsync();
		
		await reporter.SubWork("Deleting duplicates...", finishedItems: 0, totalItems: 0);
		
		await using (var deleteCmd = conn.Delete("downloads", ("url", SqliteType.Text))) {
			foreach (string duplicateUrl in duplicateUrlsToDelete) {
				deleteCmd.Set(":url", duplicateUrl);
				await deleteCmd.ExecuteNonQueryAsync();
			}
		}
		
		await conn.CommitTransactionAsync();
		
		int totalUrls = normalizedUrlsToOriginalUrls.Count;
		int processedUrls = -1;
		
		await conn.BeginTransactionAsync();
		
		await using (var updateCmd = conn.Command("UPDATE downloads SET download_url = :download_url, url = :normalized_url WHERE url = :download_url")) {
			updateCmd.Add(":normalized_url", SqliteType.Text);
			updateCmd.Add(":download_url", SqliteType.Text);
			
			foreach ((string normalizedUrl, string downloadUrl) in normalizedUrlsToOriginalUrls) {
				if (++processedUrls % 100 == 0) {
					await reporter.SubWork("Updating URLs...", processedUrls, totalUrls);
					
					// Not proper way of dealing with transactions, but it avoids a long commit at the end.
					// Schema upgrades are already non-atomic anyways, so this doesn't make it worse.
					await conn.CommitTransactionAsync();
					
					await conn.BeginTransactionAsync();
					conn.AssignActiveTransaction(updateCmd);
				}
				
				updateCmd.Set(":normalized_url", normalizedUrl);
				updateCmd.Set(":download_url", downloadUrl);
				await updateCmd.ExecuteNonQueryAsync();
			}
		}
		
		await reporter.SubWork("Updating URLs...", totalUrls, totalUrls);
		
		await conn.CommitTransactionAsync();
		
		await conn.ExecuteAsync("PRAGMA cache_size = -2000");
	}
}
