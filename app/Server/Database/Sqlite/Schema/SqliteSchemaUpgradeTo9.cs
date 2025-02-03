using System.Threading.Tasks;
using DHT.Server.Database.Sqlite.Utils;

namespace DHT.Server.Database.Sqlite.Schema;

sealed class SqliteSchemaUpgradeTo9 : ISchemaUpgrade {
	async Task ISchemaUpgrade.Run(ISqliteConnection conn, ISchemaUpgradeCallbacks.IProgressReporter reporter) {
		await reporter.MainWork("Applying schema changes...", finishedItems: 0, totalItems: 3);
		await SqliteSchema.CreateMessageAttachmentsTable(conn);
		
		await reporter.MainWork("Migrating message attachments...", finishedItems: 1, totalItems: 3);
		await conn.ExecuteAsync("INSERT INTO message_attachments (message_id, attachment_id) SELECT message_id, attachment_id FROM attachments a JOIN messages m USING (message_id)");
		
		await reporter.MainWork("Applying schema changes...", finishedItems: 2, totalItems: 3);
		await conn.ExecuteAsync("DROP INDEX attachments_message_ix");
		await conn.ExecuteAsync("ALTER TABLE attachments DROP COLUMN message_id");
		
		await conn.ExecuteAsync("ALTER TABLE embeds RENAME TO message_embeds");
		await conn.ExecuteAsync("ALTER TABLE edit_timestamps RENAME TO message_edit_timestamps");
		await conn.ExecuteAsync("ALTER TABLE replied_to RENAME TO message_replied_to");
		await conn.ExecuteAsync("ALTER TABLE reactions RENAME TO message_reactions");
	}
}
