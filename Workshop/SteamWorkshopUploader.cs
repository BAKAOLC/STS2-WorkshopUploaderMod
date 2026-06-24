using MegaCrit.Sts2.Core.Platform.Steam;
using Steamworks;

namespace STS2WorkshopUploader.Workshop;

internal static class SteamWorkshopUploader
{
    private const int MaxAttempts = 3;
    private static readonly AppId_t Sts2AppId = new(Const.Sts2SteamAppId);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    public static async Task<string> UploadAsync(
        WorkshopUploadPlan plan,
        IProgress<WorkshopUploadProgress>? progress = null)
    {
        if (!SteamInitializer.Initialized)
            throw new InvalidOperationException(
                "Steam is not initialized. Start the game through Steam and try again.");

        if (plan is { Mode: WorkshopUploadMode.MetadataOnly, HasWorkshopItem: false })
            throw new InvalidOperationException("Metadata-only upload requires an existing workshop item id.");

        var item = await ResolveWorkshopItem(plan);
        var mainChanged = await SubmitMainUpdate(plan, item, progress);
        await SubmitLocalizedUpdates(plan, item, progress);
        await SyncDependencies(plan, item);

        plan.State.WorkshopItemId = item.m_PublishedFileId;
        plan.State.Fingerprints = plan.Fingerprints;
        plan.State.ContentFiles = plan.ContentFiles.ToDictionary(
            file => file.Path,
            file => new ContentPackageFileState
            {
                Hash = file.Hash,
                Size = file.Size,
                LastWriteUtc = file.LastWriteUtc
            },
            StringComparer.OrdinalIgnoreCase);
        plan.State.LastUploadedUtc = DateTimeOffset.UtcNow;
        plan.Metadata.WorkshopItemId = item.m_PublishedFileId;
        WorkshopJson.Write(WorkshopPaths.StateFile(plan.Mod.Path), plan.State);
        WorkshopJson.Write(WorkshopPaths.MetadataFile(plan.Mod.Path), plan.Metadata);

        var changed = plan.ChangedKeys.Count == 0 ? "no changed fields" : string.Join(", ", plan.ChangedKeys.Order());
        return $"Uploaded item {item.m_PublishedFileId}; main update: {mainChanged}; changed: {changed}";
    }

    private static async Task<PublishedFileId_t> ResolveWorkshopItem(WorkshopUploadPlan plan)
    {
        var id = plan.Metadata.WorkshopItemId ?? plan.State.WorkshopItemId;
        if (id != null)
        {
            Main.Logger.Info($"[Audit] Using existing Workshop item. ModId={plan.Mod.Id}, ItemId={id.Value}.");
            return new PublishedFileId_t(id.Value);
        }

        Main.Logger.Info($"[Audit] Creating new Workshop item. ModId={plan.Mod.Id}.");
        var call = SteamUGC.CreateItem(Sts2AppId, EWorkshopFileType.k_EWorkshopFileTypeCommunity);
        using var result = new SteamCallResult<CreateItemResult_t>(call, SteamInitializer.DisconnectToken);
        var create = await result.Task;
        if (create.m_eResult != EResult.k_EResultOK)
            throw new InvalidOperationException($"Failed to create workshop item: {create.m_eResult}");
        Main.Logger.Info(
            $"[Audit] Created new Workshop item. ModId={plan.Mod.Id}, ItemId={create.m_nPublishedFileId.m_PublishedFileId}.");
        PersistWorkshopItemBinding(plan, create.m_nPublishedFileId);
        return create.m_nPublishedFileId;
    }

    private static void PersistWorkshopItemBinding(WorkshopUploadPlan plan, PublishedFileId_t item)
    {
        plan.State.WorkshopItemId = item.m_PublishedFileId;
        plan.Metadata.WorkshopItemId = item.m_PublishedFileId;
        WorkshopJson.Write(WorkshopPaths.StateFile(plan.Mod.Path), plan.State);
        WorkshopJson.Write(WorkshopPaths.MetadataFile(plan.Mod.Path), plan.Metadata);
        Main.Logger.Info(
            $"[Audit] Persisted Workshop item binding. ModId={plan.Mod.Id}, ItemId={item.m_PublishedFileId}.");
    }

    private static async Task<string> SubmitMainUpdate(
        WorkshopUploadPlan plan,
        PublishedFileId_t item,
        IProgress<WorkshopUploadProgress>? progress)
    {
        var metadata = plan.Metadata;
        var handle = SteamUGC.StartItemUpdate(Sts2AppId, item);
        var touched = false;

        if (metadata.Update.Title && plan.Changed("title") && metadata.Title != null)
            touched |= Ensure(SteamUGC.SetItemTitle(handle, metadata.Title), "title");

        if (metadata.Update.Description && plan.Changed("description") && metadata.Description != null)
            touched |= Ensure(SteamUGC.SetItemDescription(handle, metadata.Description), "description");

        if (metadata.Update.Visibility && plan.Changed("visibility") && metadata.Visibility != null)
            touched |= Ensure(SteamUGC.SetItemVisibility(handle, ParseVisibility(metadata.Visibility)), "visibility");

        if (metadata.Update.Tags && plan.Changed("tags"))
            touched |= Ensure(SteamUGC.SetItemTags(handle, metadata.Tags), "tags");

        if (metadata.Update.GameVersions && plan.Changed("gameVersions"))
            touched |= Ensure(
                SteamUGC.SetRequiredGameVersions(handle, metadata.MinBranch ?? "", metadata.MaxBranch ?? ""),
                "game versions");

        if (plan.Mode == WorkshopUploadMode.Full && metadata.Update.Content && plan.Changed("content") &&
            plan.StagingPath != null)
            touched |= Ensure(SteamUGC.SetItemContent(handle, plan.StagingPath), "content");

        var previewPath = WorkshopPaths.PreviewFile(plan.Mod.Path);
        if (metadata.Update.Preview && plan.Changed("preview") && File.Exists(previewPath))
            touched |= Ensure(SteamUGC.SetItemPreview(handle, previewPath), "preview");

        var changelogOnly = !touched &&
                            !string.IsNullOrWhiteSpace(metadata.ChangeNote) &&
                            plan.ChangedKeys.Count > 0;
        if (!touched && !changelogOnly)
        {
            Main.Logger.Info(
                $"[Audit] Main Workshop update skipped. ItemId={item.m_PublishedFileId}, ChangedKeys={FormatKeys(plan.ChangedKeys)}.");
            return "skipped";
        }

        Main.Logger.Info(
            $"[Audit] Submitting main Workshop update. ItemId={item.m_PublishedFileId}, Touched={touched}, ChangelogOnly={changelogOnly}, ChangedKeys={FormatKeys(plan.ChangedKeys)}.");
        var call = SteamUGC.SubmitItemUpdate(handle, metadata.ChangeNote ?? "");
        var update = await WaitForUpdate(
            handle,
            call,
            progress,
            WorkshopUploadProgressOperation.MainUpdate);
        if (update.m_eResult != EResult.k_EResultOK)
            throw new InvalidOperationException($"Workshop update failed: {update.m_eResult}");
        Main.Logger.Info(
            $"[Audit] Main Workshop update completed. ItemId={item.m_PublishedFileId}, Result={update.m_eResult}.");
        return changelogOnly ? "changelog submitted" : "submitted";
    }

    private static async Task SubmitLocalizedUpdates(
        WorkshopUploadPlan plan,
        PublishedFileId_t item,
        IProgress<WorkshopUploadProgress>? progress)
    {
        if (!plan.Metadata.Update.Localized)
            return;

        foreach (var (language, text) in plan.Metadata.Localized)
        {
            if (!plan.Changed($"localized:{language}:title") &&
                !plan.Changed($"localized:{language}:description"))
                continue;

            if (text.Description != null && string.IsNullOrWhiteSpace(text.Title))
                throw new InvalidOperationException(
                    $"Localized entry '{language}' has description without title. Include title.txt to avoid Steam clearing it.");

            await SubmitLocalizedUpdate(language, text, item, progress);
            Main.Logger.Info(
                $"[Audit] Localized Workshop update completed. ItemId={item.m_PublishedFileId}, Language={language}.");
        }
    }

    private static async Task SubmitLocalizedUpdate(
        string language,
        LocalizedWorkshopText text,
        PublishedFileId_t item,
        IProgress<WorkshopUploadProgress>? progress)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var handle = SteamUGC.StartItemUpdate(Sts2AppId, item);
            if (!SteamUGC.SetItemUpdateLanguage(handle, language))
                throw new InvalidOperationException($"Failed to set Steam update language '{language}'.");

            if (text.Title != null)
                Ensure(SteamUGC.SetItemTitle(handle, text.Title), $"localized title {language}");
            if (text.Description != null)
                Ensure(SteamUGC.SetItemDescription(handle, text.Description), $"localized description {language}");

            var call = SteamUGC.SubmitItemUpdate(handle, "");
            var result = await WaitForUpdate(
                handle,
                call,
                progress,
                WorkshopUploadProgressOperation.LocalizedUpdate,
                language);
            if (result.m_eResult == EResult.k_EResultOK)
                return;

            if (!ShouldRetry(result.m_eResult) || attempt == MaxAttempts)
                throw new InvalidOperationException($"Localized update '{language}' failed: {result.m_eResult}");
            await Task.Delay(RetryDelay, SteamInitializer.DisconnectToken);
        }
    }

    private static async Task SyncDependencies(WorkshopUploadPlan plan, PublishedFileId_t item)
    {
        if (!plan.Metadata.Update.Dependencies || !plan.Changed("dependencies"))
            return;

        var existing = await GetDependencies(item);
        foreach (var dependency in plan.Metadata.Dependencies.Except(existing).ToArray())
        {
            Main.Logger.Info(
                $"[Audit] Adding Workshop dependency. ItemId={item.m_PublishedFileId}, DependencyId={dependency}.");
            await AddDependency(item, dependency);
        }

        foreach (var dependency in existing.Except(plan.Metadata.Dependencies).ToArray())
        {
            Main.Logger.Info(
                $"[Audit] Removing Workshop dependency. ItemId={item.m_PublishedFileId}, DependencyId={dependency}.");
            await RemoveDependency(item, dependency);
        }
    }

    private static async Task<List<ulong>> GetDependencies(PublishedFileId_t item)
    {
        var handle = SteamUGC.CreateQueryUGCDetailsRequest([item], 1);
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

    private static async Task AddDependency(PublishedFileId_t item, ulong dependency)
    {
        await RetryResult(
            () => new SteamCallResult<AddUGCDependencyResult_t>(
                SteamUGC.AddDependency(item, new PublishedFileId_t(dependency)), SteamInitializer.DisconnectToken).Task,
            result => result.m_eResult,
            $"add dependency {dependency}");
    }

    private static async Task RemoveDependency(PublishedFileId_t item, ulong dependency)
    {
        await RetryResult(
            () => new SteamCallResult<RemoveUGCDependencyResult_t>(
                    SteamUGC.RemoveDependency(item, new PublishedFileId_t(dependency)),
                    SteamInitializer.DisconnectToken)
                .Task,
            result => result.m_eResult,
            $"remove dependency {dependency}");
    }

    private static async Task RetryResult<T>(Func<Task<T>> call, Func<T, EResult> resultSelector, string label)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var result = await call();
            var code = resultSelector(result);
            if (code == EResult.k_EResultOK)
                return;
            if (!ShouldRetry(code) || attempt == MaxAttempts)
                throw new InvalidOperationException($"Failed to {label}: {code}");
            await Task.Delay(RetryDelay, SteamInitializer.DisconnectToken);
        }

        throw new InvalidOperationException($"Failed to {label}.");
    }

    private static async Task<SubmitItemUpdateResult_t> WaitForUpdate(
        UGCUpdateHandle_t handle,
        SteamAPICall_t call,
        IProgress<WorkshopUploadProgress>? progress,
        WorkshopUploadProgressOperation operation,
        string? language = null)
    {
        using var result = new SteamCallResult<SubmitItemUpdateResult_t>(call, SteamInitializer.DisconnectToken);
        while (!result.Task.IsCompleted)
        {
            ReportUpdateProgress(handle, progress, operation, language);
            await Task.Delay(500, SteamInitializer.DisconnectToken);
        }

        ReportUpdateProgress(handle, progress, operation, language);
        return await result.Task;
    }

    private static void ReportUpdateProgress(
        UGCUpdateHandle_t handle,
        IProgress<WorkshopUploadProgress>? progress,
        WorkshopUploadProgressOperation operation,
        string? language)
    {
        if (progress == null)
            return;

        var status = SteamUGC.GetItemUpdateProgress(handle, out var bytesProcessed, out var bytesTotal);
        progress.Report(new WorkshopUploadProgress(
            operation,
            MapProgressStatus(status),
            bytesProcessed,
            bytesTotal,
            language));
    }

    private static WorkshopItemUpdateProgressStatus MapProgressStatus(EItemUpdateStatus status)
    {
        return status switch
        {
            EItemUpdateStatus.k_EItemUpdateStatusPreparingConfig =>
                WorkshopItemUpdateProgressStatus.PreparingConfig,
            EItemUpdateStatus.k_EItemUpdateStatusPreparingContent =>
                WorkshopItemUpdateProgressStatus.PreparingContent,
            EItemUpdateStatus.k_EItemUpdateStatusUploadingContent =>
                WorkshopItemUpdateProgressStatus.UploadingContent,
            EItemUpdateStatus.k_EItemUpdateStatusUploadingPreviewFile =>
                WorkshopItemUpdateProgressStatus.UploadingPreviewFile,
            EItemUpdateStatus.k_EItemUpdateStatusCommittingChanges =>
                WorkshopItemUpdateProgressStatus.CommittingChanges,
            _ => WorkshopItemUpdateProgressStatus.Invalid
        };
    }

    private static bool Ensure(bool ok, string field)
    {
        if (!ok)
            throw new InvalidOperationException($"Steam rejected {field} update.");
        return true;
    }

    private static ERemoteStoragePublishedFileVisibility ParseVisibility(string visibility)
    {
        return visibility.Trim().ToLowerInvariant() switch
        {
            "private" => ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPrivate,
            "public" => ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic,
            "unlisted" => ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityUnlisted,
            "friends" or "friendsonly" or "friends_only" =>
                ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityFriendsOnly,
            _ => throw new InvalidOperationException(
                $"Invalid visibility '{visibility}'. Use private, public, unlisted, or friends_only.")
        };
    }

    private static bool ShouldRetry(EResult result)
    {
        return result is EResult.k_EResultBusy
            or EResult.k_EResultTimeout
            or EResult.k_EResultServiceUnavailable
            or EResult.k_EResultLimitExceeded
            or EResult.k_EResultIOFailure
            or EResult.k_EResultRemoteCallFailed
            or EResult.k_EResultRateLimitExceeded;
    }

    private static string FormatKeys(IEnumerable<string> keys)
    {
        var values = keys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return values.Length == 0 ? "<none>" : string.Join(",", values);
    }
}