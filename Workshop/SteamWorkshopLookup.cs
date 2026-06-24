using MegaCrit.Sts2.Core.Platform.Steam;
using Steamworks;

namespace STS2WorkshopUploader.Workshop;

internal static class SteamWorkshopLookup
{
    private const uint TagBufferSize = 256;
    private const uint TagDiscoveryPages = 8;
    private static readonly AppId_t Sts2AppId = new(Const.Sts2SteamAppId);

    public static async Task<List<WorkshopItemSummary>> GetItemsAsync(IEnumerable<ulong> itemIds)
    {
        return await GetItemsAsync(itemIds, null);
    }

    public static async Task<List<WorkshopItemSummary>> GetItemsAsync(IEnumerable<ulong> itemIds, string? language)
    {
        var ids = itemIds.Distinct().Where(id => id != 0).Select(id => new PublishedFileId_t(id)).ToArray();
        if (ids.Length == 0)
            return [];

        EnsureSteam();
        var handle = SteamUGC.CreateQueryUGCDetailsRequest(ids, (uint)ids.Length);
        try
        {
            SteamUGC.SetReturnLongDescription(handle, true);
            if (!string.IsNullOrWhiteSpace(language))
                SteamUGC.SetLanguage(handle, language);
            return await ReadQueryAsync(handle, ids.Length);
        }
        finally
        {
            SteamUGC.ReleaseQueryUGCRequest(handle);
        }
    }

    public static async Task<Dictionary<string, WorkshopItemSummary>> GetLocalizedItemAsync(
        ulong itemId,
        IEnumerable<string> languages)
    {
        var result = new Dictionary<string, WorkshopItemSummary>(StringComparer.OrdinalIgnoreCase);
        foreach (var language in languages.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var item = (await GetItemsAsync([itemId], language)).FirstOrDefault();
            if (item != null)
                result[language] = item;
        }

        return result;
    }

    public static async Task<List<ulong>> GetDependenciesAsync(ulong itemId)
    {
        if (itemId == 0)
            return [];

        EnsureSteam();
        var handle = SteamUGC.CreateQueryUGCDetailsRequest([new PublishedFileId_t(itemId)], 1);
        try
        {
            SteamUGC.SetReturnChildren(handle, true);
            using var result = new SteamCallResult<SteamUGCQueryCompleted_t>(
                SteamUGC.SendQueryUGCRequest(handle), SteamInitializer.DisconnectToken);
            var completed = await result.Task;
            if (completed.m_eResult != EResult.k_EResultOK ||
                !SteamUGC.GetQueryUGCResult(handle, 0, out var details) ||
                details.m_unNumChildren == 0)
                return [];

            var children = new PublishedFileId_t[details.m_unNumChildren];
            if (!SteamUGC.GetQueryUGCChildren(handle, 0, children, details.m_unNumChildren))
                return [];

            return children.Select(child => child.m_PublishedFileId).Where(id => id != 0).ToList();
        }
        finally
        {
            SteamUGC.ReleaseQueryUGCRequest(handle);
        }
    }

    public static string? TryGetInstalledContentPath(ulong itemId)
    {
        if (itemId == 0 || !SteamInitializer.Initialized)
            return null;

        var publishedFileId = new PublishedFileId_t(itemId);
        var state = SteamUGC.GetItemState(publishedFileId);
        if ((state & (uint)EItemState.k_EItemStateInstalled) == 0)
            return null;

        return SteamUGC.GetItemInstallInfo(publishedFileId, out _, out var folder, 1024, out _) &&
               !string.IsNullOrWhiteSpace(folder)
            ? folder
            : null;
    }

    public static async Task<List<WorkshopItemSummary>> SearchAsync(string query, uint limit = 10)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        EnsureSteam();
        var handle = SteamUGC.CreateQueryAllUGCRequest(
            EUGCQuery.k_EUGCQuery_RankedByTextSearch,
            EUGCMatchingUGCType.k_EUGCMatchingUGCType_Items,
            Sts2AppId,
            Sts2AppId,
            1);

        try
        {
            SteamUGC.SetReturnLongDescription(handle, true);
            SteamUGC.SetSearchText(handle, query.Trim());
            return await ReadQueryAsync(handle, (int)limit);
        }
        finally
        {
            SteamUGC.ReleaseQueryUGCRequest(handle);
        }
    }

    public static async Task<List<WorkshopTagOption>> GetObservedTagsAsync(uint pages = TagDiscoveryPages)
    {
        var tags = new Dictionary<string, WorkshopTagOption>(StringComparer.OrdinalIgnoreCase);
        for (uint page = 1; page <= Math.Max(1, pages); page++)
        {
            var pageTags = await GetObservedTagsPageAsync(page);
            foreach (var tag in pageTags)
                tags[tag.Value] = tag;
            if (pageTags.Count == 0)
                break;
        }

        return tags.Values.OrderBy(tag => tag.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static async Task<List<WorkshopTagOption>> GetObservedTagsPageAsync(uint page)
    {
        EnsureSteam();
        var handle = SteamUGC.CreateQueryAllUGCRequest(
            EUGCQuery.k_EUGCQuery_RankedByVote,
            EUGCMatchingUGCType.k_EUGCMatchingUGCType_Items,
            Sts2AppId,
            Sts2AppId,
            page);

        try
        {
            using var result = new SteamCallResult<SteamUGCQueryCompleted_t>(
                SteamUGC.SendQueryUGCRequest(handle),
                SteamInitializer.DisconnectToken);

            var completed = await result.Task;
            if (completed.m_eResult != EResult.k_EResultOK)
                throw new InvalidOperationException($"Steam Workshop tag query failed: {completed.m_eResult}");

            var tags = new Dictionary<string, WorkshopTagOption>(StringComparer.OrdinalIgnoreCase);
            for (uint itemIndex = 0; itemIndex < completed.m_unNumResultsReturned; itemIndex++)
            {
                var count = SteamUGC.GetQueryUGCNumTags(handle, itemIndex);
                for (uint tagIndex = 0; tagIndex < count; tagIndex++)
                {
                    if (!SteamUGC.GetQueryUGCTag(handle, itemIndex, tagIndex, out var tag, TagBufferSize) ||
                        string.IsNullOrWhiteSpace(tag))
                        continue;

                    SteamUGC.GetQueryUGCTagDisplayName(handle, itemIndex, tagIndex, out var displayName, TagBufferSize);
                    tags[tag] = new WorkshopTagOption(
                        tag,
                        string.IsNullOrWhiteSpace(displayName) ? tag : displayName,
                        WorkshopTagSource.Remote);
                }
            }

            return tags.Values.OrderBy(tag => tag.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        }
        finally
        {
            SteamUGC.ReleaseQueryUGCRequest(handle);
        }
    }

    private static async Task<List<WorkshopItemSummary>> ReadQueryAsync(UGCQueryHandle_t handle, int limit)
    {
        using var result = new SteamCallResult<SteamUGCQueryCompleted_t>(
            SteamUGC.SendQueryUGCRequest(handle),
            SteamInitializer.DisconnectToken);

        var completed = await result.Task;
        if (completed.m_eResult != EResult.k_EResultOK)
            throw new InvalidOperationException($"Steam Workshop query failed: {completed.m_eResult}");

        var count = Math.Min(limit, (int)completed.m_unNumResultsReturned);
        var list = new List<WorkshopItemSummary>(count);
        for (uint i = 0; i < count; i++)
        {
            if (!SteamUGC.GetQueryUGCResult(handle, i, out var details))
                continue;

            if (details.m_eResult != EResult.k_EResultOK)
                continue;

            var id = details.m_nPublishedFileId.m_PublishedFileId;
            var title = details.m_rgchTitle;
            var visibility = ParseVisibility(details.m_eVisibility);
            var tags = ReadTags(handle, i);
            var previewUrl = ReadPreviewUrl(handle, i);
            list.Add(new WorkshopItemSummary(
                id,
                string.IsNullOrWhiteSpace(title) ? id.ToString() : title,
                BuildSummary(details, visibility),
                visibility,
                details.m_rgchDescription ?? string.Empty,
                tags,
                previewUrl,
                details.m_ulSteamIDOwner));
        }

        return list;
    }

    public static async Task<WorkshopEditPermission> GetEditPermissionAsync(ulong itemId)
    {
        if (itemId == 0)
            throw new InvalidOperationException("Workshop item id is empty.");

        EnsureSteam();
        var item = (await GetItemsAsync([itemId])).FirstOrDefault() ??
                   throw new InvalidOperationException($"Workshop item {itemId} was not found.");
        var current = SteamUser.GetSteamID().m_SteamID;
        return new WorkshopEditPermission(itemId, item.OwnerSteamId, current, item.OwnerSteamId == current);
    }

    private static List<string> ReadTags(UGCQueryHandle_t handle, uint itemIndex)
    {
        var tags = new List<string>();
        var count = SteamUGC.GetQueryUGCNumTags(handle, itemIndex);
        for (uint tagIndex = 0; tagIndex < count; tagIndex++)
            if (SteamUGC.GetQueryUGCTag(handle, itemIndex, tagIndex, out var tag, TagBufferSize) &&
                !string.IsNullOrWhiteSpace(tag))
                tags.Add(tag);

        return tags;
    }

    private static string? ReadPreviewUrl(UGCQueryHandle_t handle, uint itemIndex)
    {
        return SteamUGC.GetQueryUGCPreviewURL(handle, itemIndex, out var url, 1024) &&
               !string.IsNullOrWhiteSpace(url)
            ? url
            : null;
    }

    private static string BuildSummary(SteamUGCDetails_t details, string? visibility)
    {
        return visibility == null
            ? $"id={details.m_nPublishedFileId.m_PublishedFileId}"
            : $"id={details.m_nPublishedFileId.m_PublishedFileId}, visibility={visibility}";
    }

    private static string? ParseVisibility(ERemoteStoragePublishedFileVisibility visibility)
    {
        return visibility switch
        {
            ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPrivate => "private",
            ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityFriendsOnly => "friends_only",
            ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic => "public",
            ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityUnlisted => "unlisted",
            _ => null
        };
    }

    private static void EnsureSteam()
    {
        if (!SteamInitializer.Initialized)
            throw new InvalidOperationException(
                "Steam is not initialized. Start the game through Steam and try again.");
    }
}