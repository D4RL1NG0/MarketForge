using Microsoft.Maui.Storage;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;
using System.Collections.Concurrent;

namespace AlbionMarket.Mobile.Services;

public static class ItemIconCacheService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };
    private static readonly ConcurrentDictionary<string, Task<string?>> InFlight = new(StringComparer.OrdinalIgnoreCase);

    static ItemIconCacheService()
    {
        try
        {
            Http.DefaultRequestHeaders.UserAgent.ParseAdd("AlbionMarketMobile/1.0");
        }
        catch
        {
            // Header opcional. Não derrube o app por configuração de HTTP.
        }
    }

    public static ImageSource? GetBestSource(string itemId, int quality, int size)
    {
        var cleanId = Clean(itemId);
        quality = Math.Clamp(quality, 1, 5);
        size = Math.Clamp(size, 48, 217);
        var key = BuildKey(cleanId, quality, size);
        var localPath = Path.Combine(GetIconCacheDirectory(), $"{SanitizeFileName(key)}.png");
        if (File.Exists(localPath))
            return ImageSource.FromFile(localPath);

        // Quando o usuário rodar o script de pré-download, os arquivos entram como MauiImage.
        // O nome de arquivo é usado antes da URL remota, deixando a carga instantânea.
        var bundledName = $"{SanitizeFileName(key)}.png";
        if (IsLikelyBundledIconAvailable())
            return ImageSource.FromFile(bundledName);

        return new UriImageSource
        {
            Uri = new Uri(UiText.ItemIconUrl(cleanId, quality, size)),
            CachingEnabled = true,
            CacheValidity = TimeSpan.FromDays(30)
        };
    }

    public static async Task LoadIntoAsync(Image image, string itemId, int quality, int size)
    {
        try
        {
            var local = await GetOrDownloadAsync(itemId, quality, size);
            if (string.IsNullOrWhiteSpace(local) || !File.Exists(local))
                return;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                image.Source = ImageSource.FromFile(local);
            });
        }
        catch
        {
            // A imagem remota já foi definida como fallback. Não derrube a UI por ícone.
        }
    }

    public static async Task PrewarmAsync(IEnumerable<string> itemIds, int quality, int size)
    {
        var throttler = new SemaphoreSlim(4);
        var tasks = itemIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(120)
            .Select(async itemId =>
            {
                await throttler.WaitAsync();
                try { await GetOrDownloadAsync(itemId, quality, size); }
                finally { throttler.Release(); }
            });

        await Task.WhenAll(tasks);
    }

    public static Task<string?> GetOrDownloadAsync(string itemId, int quality, int size)
    {
        var cleanId = Clean(itemId);
        if (string.IsNullOrWhiteSpace(cleanId))
            return Task.FromResult<string?>(null);

        quality = Math.Clamp(quality, 1, 5);
        size = Math.Clamp(size, 48, 217);
        var key = BuildKey(cleanId, quality, size);
        var path = Path.Combine(GetIconCacheDirectory(), $"{SanitizeFileName(key)}.png");
        if (File.Exists(path))
            return Task.FromResult<string?>(path);

        return InFlight.GetOrAdd(key, _ => DownloadAndRemoveAsync(key, cleanId, quality, size, path));
    }

    private static async Task<string?> DownloadAndRemoveAsync(string key, string itemId, int quality, int size, string path)
    {
        try
        {
            return await DownloadAsync(itemId, quality, size, path);
        }
        finally
        {
            InFlight.TryRemove(key, out _);
        }
    }

    private static async Task<string?> DownloadAsync(string itemId, int quality, int size, string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var url = UiText.ItemIconUrl(itemId, quality, size);
            using var response = await Http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;

            var bytes = await response.Content.ReadAsByteArrayAsync();
            if (bytes.Length == 0)
                return null;

            await File.WriteAllBytesAsync(path, bytes);
            return path;
        }
        catch
        {
            return null;
        }
    }

    private static string GetIconCacheDirectory()
    {
        var dir = Path.Combine(FileSystem.CacheDirectory, "item-icons");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static bool IsLikelyBundledIconAvailable() => false;

    private static string BuildKey(string itemId, int quality, int size) => $"{itemId}_{quality}_{size}";
    private static string Clean(string itemId) => (itemId ?? string.Empty).Trim().ToUpperInvariant();

    public static string SanitizeFileName(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');
        return value.Replace('@', '_').ToLowerInvariant();
    }
}
