using System.Text.Json;
using Steamworks;

namespace ModUploader;

public static class UploadCommand
{
    private static readonly AppId_t _sts2AppId = new(2868840);
    
    public static async Task<int> UploadWorkspace(DirectoryInfo workspaceDirectory, ulong? itemIdArg)
    {
        // First, do some validation of what is in the directory.
        FileInfo imageFileInfo = new FileInfo(Path.Combine(workspaceDirectory.FullName, "image.jpg"));
        if (!imageFileInfo.Exists)
        {
            Log.Info("There is no file named image.jpg in the workspace!");
            return 1;
        }

        DirectoryInfo contentDirectoryInfo = new DirectoryInfo(Path.Combine(workspaceDirectory.FullName, "content"));
        if (!contentDirectoryInfo.Exists)
        {
            Log.Info("There is no 'content' directory inside the workspace!");
            return 1;
        }

        FileInfo configJsonInfo = new FileInfo(Path.Combine(workspaceDirectory.FullName, "workshop.json"));
        if (!configJsonInfo.Exists)
        {
            Log.Info("There is no file named workshop.json in the workspace!");
            return 1;
        }

        ModConfig? modConfig;
        
        try
        {
            await using FileStream configJsonStream = configJsonInfo.Open(FileMode.Open);
            modConfig = await JsonSerializer.DeserializeAsync(configJsonStream, SourceGenerationContext.Default.ModConfig);
        }
        catch (JsonException)
        {
            Log.Info("Exception thrown while parsing the workshop config! Double-check that the format is correct.");
            throw;
        }

        if (modConfig == null)
        {
            Log.Info("Tried to parse workshop.json, but it returned null!");
            return 1;
        }

        if (VisibiltyFromString(modConfig.visibility) == null)
        {
            Log.Info($"Invalid visibility '{modConfig.visibility}' in workshop.json! Should be: private, public, unlisted, or friends_only");
            return 1;
        }

        ulong? modIdTxt = null;
        
        FileInfo modIdFile = new(Path.Combine(workspaceDirectory.FullName, "mod_id.txt"));
        if (modIdFile.Exists)
        {
            await using FileStream modIdStream = modIdFile.OpenRead();
            using StreamReader reader = new(modIdStream);
            string modIdStr = (await reader.ReadToEndAsync()).Trim();

            if (!ulong.TryParse(modIdStr, out ulong modId))
            {
                Log.Info("Tried to read mod ID from mod_id.txt, but the text could not be parsed as a mod ID!");
                return 1;
            }

            modIdTxt = modId;
        }

        // Validation is all done. Start the upload process.
        if (!Program.InitializeSteam())
        {
            return 1;
        }
        
        Log.Info("=================");
        Log.Info($"By submitting '{modConfig.title}' to the workshop,\n" +
                 $"you agree to the Steam Workshop terms of service:\n" +
                 $"https://steamcommunity.com/sharedfiles/workshoplegalagreement");
        Log.Info("=================");

        PublishedFileId_t workshopItem;
        
        Log.Info($"Logged in as user '{SteamFriends.GetPersonaName()}'.");

        if (itemIdArg != null)
        {
            Log.Info($"Using workshop item ID {itemIdArg.Value} passed in via command line");
            workshopItem = new PublishedFileId_t(itemIdArg.Value);

            bool exists = await DoesWorkshopItemExist(workshopItem);
            if (!exists)
            {
                Log.Error($"Tried to upload to workshop item with ID {itemIdArg.Value} passed via command line, but it doesn't exist!");
                return 1;
            }
        }
        else if (modIdTxt != null)
        {
            Log.Info($"Using workshop item ID {modIdTxt.Value} from mod_id.txt");
            workshopItem = new PublishedFileId_t(modIdTxt.Value);

            bool exists = await DoesWorkshopItemExist(workshopItem);
            if (!exists)
            {
                Log.Error($"Tried to upload to workshop item with ID {modIdTxt.Value} but it doesn't exist! If you wish to upload a new item, delete 'mod_id.txt' from your mod directory.");
                return 1;
            }
        }
        else
        {
            Log.Info("Creating new workshop item...");

            SteamAPICall_t createItemCall = SteamUGC.CreateItem(_sts2AppId, EWorkshopFileType.k_EWorkshopFileTypeCommunity);
            using SteamCallResult<CreateItemResult_t> createItemCallResult = new(createItemCall);
            CreateItemResult_t createItemResult = await createItemCallResult.Task;

            if (createItemResult.m_eResult != EResult.k_EResultOK)
            {
                Log.Error($"Failed to create workshop item! Result: {createItemResult.m_eResult}");
                return 1;
            }

            workshopItem = createItemResult.m_nPublishedFileId;
        }
        
        Log.Info($"Uploading '{modConfig.title}' to the steam workshop with item ID {workshopItem.m_PublishedFileId}...");

        UGCUpdateHandle_t updateHandle = SteamUGC.StartItemUpdate(_sts2AppId, workshopItem);

        SteamUGC.SetItemTitle(updateHandle, modConfig.title);
        SteamUGC.SetItemDescription(updateHandle, modConfig.description);
        SteamUGC.SetItemVisibility(updateHandle, VisibiltyFromString(modConfig.visibility)!.Value);
        SteamUGC.SetItemTags(updateHandle, modConfig.tags);
        SteamUGC.SetItemContent(updateHandle, contentDirectoryInfo.FullName);
        SteamUGC.SetItemPreview(updateHandle, imageFileInfo.FullName);

        SteamAPICall_t updateItemCall = SteamUGC.SubmitItemUpdate(updateHandle, modConfig.changeNote);
        using SteamCallResult<SubmitItemUpdateResult_t> updateItemCallResult = new(updateItemCall);

        CancellationTokenSource uploadProgressCancelToken = new();
        _ = LogUploadProgress(updateHandle, uploadProgressCancelToken);
        
        SubmitItemUpdateResult_t updateItemResult = await updateItemCallResult.Task;

        if (updateItemResult.m_eResult != EResult.k_EResultOK)
        {
            Log.Error($"Error occurred while uploading to the workshop! Result: {updateItemResult.m_eResult}");
            return 1;
        }
        
        await UpdateDependencies(workshopItem, modConfig.dependencies ?? []);

        Log.Info($"Successfully uploaded '{modConfig.title}' to the workshop with id {workshopItem.m_PublishedFileId}! Browsing to the item in Steam.");
        SteamFriends.ActivateGameOverlayToWebPage($"steam://url/CommunityFilePage/{workshopItem.m_PublishedFileId}");
        
        // Since we successfully uploaded, if it didn't exist already, put a mod_id.txt in the directory for later, to
        // identify which mod ID this is.
        if (modIdTxt == null || modIdTxt.Value != workshopItem.m_PublishedFileId)
        {
            await using FileStream fileStream = modIdFile.OpenWrite();
            await using StreamWriter writer = new(fileStream);
            writer.WriteLine(workshopItem.m_PublishedFileId);
        }
        
        return 0;
    }

    private static async Task UpdateDependencies(PublishedFileId_t workshopItem, List<ulong> newDependencies)
    {
        List<ulong> existingDependencies = await GetAppDependencies(workshopItem);
        bool modified = false;
        
        // Iterate new dependencies, adding dependencies that didn't exist
        foreach (ulong dependency in newDependencies)
        {
            if (!existingDependencies.Contains(dependency))
            {
                SteamUGC.AddDependency(workshopItem, new PublishedFileId_t(dependency));
                Log.Info($"Added dependency on {dependency}");
                modified = true;
            }
        }
        
        // Iterate existing dependencies, removing dependencies that no longer exist
        foreach (ulong dependency in existingDependencies)
        {
            if (!newDependencies.Contains(dependency))
            {
                SteamUGC.RemoveDependency(workshopItem, new PublishedFileId_t(dependency));
                Log.Info($"Removed dependency on {dependency}");
                modified = true;
            }
        }

        if (!modified)
        {
            Log.Info("No modifications were made to dependencies.");
        }
    }

    private static async Task<List<ulong>> GetAppDependencies(PublishedFileId_t workshopItem)
    {
        Log.Info("Querying existing app dependencies... ");
        
        UGCQueryHandle_t handle = SteamUGC.CreateQueryUGCDetailsRequest([workshopItem], 1);

        try
        {
            SteamAPICall_t call = SteamUGC.SendQueryUGCRequest(handle);
            using SteamCallResult<SteamUGCQueryCompleted_t> callResult = new(call);
            SteamUGCQueryCompleted_t result = await callResult.Task;

            if (result.m_eResult != EResult.k_EResultOK)
            {
                Log.Warn(
                    $"Couldn't get dependencies for item {workshopItem.m_PublishedFileId}! Error: {result.m_eResult}");
                return [];
            }
            
            SteamUGC.GetQueryUGCResult(handle, 0, out _);

            bool success;
            uint index = 0;
            PublishedFileId_t[] cache = new PublishedFileId_t[4];
            List<ulong> dependencies = [];

            do
            {
                success = SteamUGC.GetQueryUGCChildren(result.m_handle, index, cache, (uint)cache.Length);
                foreach (PublishedFileId_t dependency in cache)
                {
                    if (dependency.m_PublishedFileId != 0)
                    {
                        dependencies.Add(dependency.m_PublishedFileId);
                    }
                }

                index += (uint)cache.Length;
            } while (success);

            if (dependencies.Count > 0)
            {
                Log.Info($"Found {dependencies.Count} dependencies.");
            }

            return dependencies;
        }
        finally
        {
            SteamUGC.ReleaseQueryUGCRequest(handle);
        }
    }

    private static async Task<bool> DoesWorkshopItemExist(PublishedFileId_t workshopItem)
    {
        UGCQueryHandle_t handle = SteamUGC.CreateQueryUGCDetailsRequest([workshopItem], 1);

        try
        {
            SteamAPICall_t call = SteamUGC.SendQueryUGCRequest(handle);
            using SteamCallResult<SteamUGCQueryCompleted_t> callResult = new(call);
            SteamUGCQueryCompleted_t result = await callResult.Task;

            SteamUGC.GetQueryUGCResult(handle, 0, out SteamUGCDetails_t details);

            if (details.m_eResult == EResult.k_EResultFileNotFound)
            {
                return false;
            }
            else if (details.m_eResult != EResult.k_EResultOK)
            {
                Log.Warn($"Couldn't confirm existence of workshop item {workshopItem.m_PublishedFileId}. Error: {result.m_eResult}");
            }

            return true;
        }
        finally
        {
            SteamUGC.ReleaseQueryUGCRequest(handle);
        }
    }

    private static async Task LogUploadProgress(UGCUpdateHandle_t updateHandle, CancellationTokenSource cancelToken)
    {
        while (!cancelToken.IsCancellationRequested)
        {
            EItemUpdateStatus status =
                SteamUGC.GetItemUpdateProgress(updateHandle, out ulong bytesProcessed, out ulong bytesTotal);
            Log.Info($"Status: {status}, bytes processed: {bytesProcessed}/{bytesTotal} ({bytesProcessed/bytesTotal:P2})");
            await Task.Delay(1000, cancelToken.Token);
        }
    }

    private static ERemoteStoragePublishedFileVisibility? VisibiltyFromString(string? visibility)
    {
        return visibility switch
        {
            "private" => ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPrivate,
            "public" => ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic,
            "unlisted" => ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityUnlisted,
            "friends_only" => ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityFriendsOnly,
            _ => null
        };
    }
}