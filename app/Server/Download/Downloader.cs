using System;
using System.Threading;
using System.Threading.Tasks;
using DHT.Server.Data.Filters;
using DHT.Server.Database;

namespace DHT.Server.Download;

public sealed class Downloader {
	private DownloaderTask? current;
	public bool IsDownloading => current != null;
	
	private readonly IDatabaseFile db;
	private readonly int? concurrentDownloads;
	private readonly SemaphoreSlim semaphore = new (initialCount: 1, maxCount: 1);
	
	internal Downloader(IDatabaseFile db, int? concurrentDownloads) {
		this.db = db;
		this.concurrentDownloads = concurrentDownloads;
	}
	
	public async Task<IObservable<DownloadItem>> Start(DownloadItemFilter filter) {
		await semaphore.WaitAsync();
		try {
			current ??= new DownloaderTask(db, filter, concurrentDownloads);
			return current.FinishedItems;
		} finally {
			semaphore.Release();
		}
	}
	
	public async Task Stop() {
		await semaphore.WaitAsync();
		try {
			if (current != null) {
				await current.DisposeAsync();
				current = null;
			}
		} finally {
			semaphore.Release();
		}
	}
}
