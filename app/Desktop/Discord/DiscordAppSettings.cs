using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DHT.Utils.Logging;
using static System.Environment.SpecialFolder;
using static System.Environment.SpecialFolderOption;

namespace DHT.Desktop.Discord;

static class DiscordAppSettings {
	private static readonly Log Log = Log.ForType(typeof(DiscordAppSettings));
	
	private const string JsonKeyDevTools = "DANGEROUS_ENABLE_DEVTOOLS_ONLY_ENABLE_IF_YOU_KNOW_WHAT_YOURE_DOING";
	
	public static string JsonFilePath { get; }
	private static string JsonBackupFilePath { get; }
	
	[SuppressMessage("ReSharper", "ConvertIfStatementToConditionalTernaryExpression")]
	static DiscordAppSettings() {
		string rootFolder;
		
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
			rootFolder = Path.Combine(Environment.GetFolderPath(ApplicationData, DoNotVerify), "Discord");
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
			rootFolder = Path.Combine(Environment.GetFolderPath(UserProfile, DoNotVerify), "Library", "Application Support", "Discord");
		}
		else {
			rootFolder = Path.Combine(Environment.GetFolderPath(ApplicationData, DoNotVerify), "discord");
		}
		
		JsonFilePath = Path.Combine(rootFolder, "settings.json");
		JsonBackupFilePath = JsonFilePath + ".bak";
	}
	
	public static async Task<bool?> AreDevToolsEnabled() {
		try {
			JsonObject settingsJson = await ReadSettingsJson();
			return AreDevToolsEnabled(settingsJson);
		} catch (Exception e) {
			Log.Error("Could not read settings file.", e);
			return null;
		}
	}
	
	private static bool AreDevToolsEnabled(JsonObject json) {
		return json.TryGetPropertyValue(JsonKeyDevTools, out JsonNode? node) && node?.GetValueKind() == JsonValueKind.True;
	}
	
	public static async Task<SettingsJsonResult> ConfigureDevTools(bool enable) {
		JsonObject json;
		
		try {
			json = await ReadSettingsJson();
		} catch (FileNotFoundException) {
			return SettingsJsonResult.FileNotFound;
		} catch (JsonException) {
			return SettingsJsonResult.InvalidJson;
		} catch (Exception e) {
			Log.Error("Could not read settings file.", e);
			return SettingsJsonResult.ReadError;
		}
		
		if (enable == AreDevToolsEnabled(json)) {
			return SettingsJsonResult.AlreadySet;
		}
		
		if (enable) {
			json[JsonKeyDevTools] = true;
		}
		else {
			json.Remove(JsonKeyDevTools);
		}
		
		try {
			if (!File.Exists(JsonBackupFilePath)) {
				File.Copy(JsonFilePath, JsonBackupFilePath);
			}
			
			await WriteSettingsJson(json);
		} catch (Exception e) {
			Log.Error("Could not write settings file.", e);
			
			if (File.Exists(JsonBackupFilePath)) {
				try {
					File.Move(JsonBackupFilePath, JsonFilePath, overwrite: true);
					Log.Info("Restored settings file from backup.");
				} catch (Exception e2) {
					Log.Error("Could not restore settings file from backup.", e2);
				}
			}
			
			return SettingsJsonResult.WriteError;
		}
		
		try {
			File.Delete(JsonBackupFilePath);
		} catch (Exception e) {
			Log.Error("Could not delete backup file.", e);
		}
		
		return SettingsJsonResult.Success;
	}
	
	private static async Task<JsonObject> ReadSettingsJson() {
		await using var stream = new FileStream(JsonFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
		return await JsonSerializer.DeserializeAsync(stream, DiscordAppSettingsJsonContext.Default.JsonObject) ?? throw new JsonException();
	}
	
	private static async Task WriteSettingsJson(JsonObject json) {
		await using var stream = new FileStream(JsonFilePath, FileMode.Truncate, FileAccess.Write, FileShare.None);
		await JsonSerializer.SerializeAsync(stream, json, DiscordAppSettingsJsonContext.Default.JsonObject);
	}
}
