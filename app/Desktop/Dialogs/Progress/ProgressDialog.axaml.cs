using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Avalonia.Controls;
using DHT.Utils.Logging;

namespace DHT.Desktop.Dialogs.Progress;

[SuppressMessage("ReSharper", "MemberCanBeInternal")]
public sealed partial class ProgressDialog : Window {
	private static readonly Log Log = Log.ForType<ProgressDialog>();
	
	internal static async Task Show(Window owner, string title, Func<ProgressDialog, IProgressCallback, Task> action) {
		var dialog = new ProgressDialog();
		dialog.DataContext = new ProgressDialogModel(title, async callbacks => await action(dialog, callbacks[0]));
		await dialog.ShowProgressDialog(owner);
	}
	
	internal static async Task<T> Show<T>(Window owner, string title, Func<ProgressDialog, IProgressCallback, Task<T>> action) {
		var taskCompletionSource = new TaskCompletionSource<T>();
		var dialog = new ProgressDialog();
		
		dialog.DataContext = new ProgressDialogModel(title, async callbacks => {
			taskCompletionSource.SetResult(await action(dialog, callbacks[0]));
		});
		
		await dialog.ShowProgressDialog(owner);
		return await taskCompletionSource.Task;
	}
	
	internal static Task ShowIndeterminate(Window owner, string title, string message, Func<ProgressDialog, Task> action) {
		return Show(owner, title, async (dialog, callback) => {
			await callback.UpdateIndeterminate(message);
			await action(dialog);
		});
	}
	
	internal static Task<T> ShowIndeterminate<T>(Window owner, string title, string message, Func<ProgressDialog, Task<T>> action) {
		return Show<T>(owner, title, async (dialog, callback) => {
			await callback.UpdateIndeterminate(message);
			return await action(dialog);
		});
	}
	
	private bool isFinished = false;
	private Task progressTask = Task.CompletedTask;
	
	public ProgressDialog() {
		InitializeComponent();
	}
	
	public void OnOpened(object? sender, EventArgs e) {
		if (DataContext is ProgressDialogModel model) {
			progressTask = Task.Run(model.StartTask);
			progressTask.ContinueWith(OnFinished, TaskScheduler.FromCurrentSynchronizationContext());
		}
	}
	
	public void OnClosing(object? sender, WindowClosingEventArgs e) {
		e.Cancel = !isFinished;
	}
	
	private void OnFinished(Task task) {
		isFinished = true;
		Close();
	}
	
	public async Task ShowProgressDialog(Window owner) {
		await ShowDialog(owner);
		await progressTask;
	}
}
