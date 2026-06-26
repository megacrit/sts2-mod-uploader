using System.Text.Json;
using Steamworks;

namespace ModUploader;

public static class UploadCommand
{
    private static readonly AppId_t _sts2AppId = new(2868840);

    private struct WorkshopItemDetails
    {
        public required List<ulong> dependencies;
        public required List<EUGCContentDescriptorID> contentDescriptors;
    }
    
    public static async Task<int> UploadWorkspace(DirectoryInfo workspaceDirectory, ulong? itemIdArg)
    {
        if (!workspaceDirectory.Exists)
        {
            Log.Error($"No directory at {workspaceDirectory}!");
            return 1;
        }
        
        // First, do some validation of what is in the directory.
        FileInfo imageFileInfo = new FileInfo(Path.Combine(workspaceDirectory.FullName, "image.png"));
        if (!imageFileInfo.Exists)
        {
            Log.Error("There is no file named image.png in the workspace!");
            return 1;
        }

        DirectoryInfo contentDirectoryInfo = new DirectoryInfo(Path.Combine(workspaceDirectory.FullName, "content"));
        if (!contentDirectoryInfo.Exists)
        {
            Log.Error("There is no 'content' directory inside the workspace!");
            return 1;
        }

        FileInfo configJsonInfo = new FileInfo(Path.Combine(workspaceDirectory.FullName, "workshop.json"));
        if (!configJsonInfo.Exists)
        {
            Log.Error("There is no file named workshop.json in the workspace!");
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
            Log.Error("Exception thrown while parsing the workshop config! Double-check that the format is correct.");
            return 1;
        }

        if (modConfig == null)
        {
            Log.Error("Tried to parse workshop.json, but it returned null!");
            return 1;
        }
        
        ERemoteStoragePublishedFileVisibility? visibility = null;

        if (modConfig.visibility != null)
        {
            visibility = VisibilityFromString(modConfig.visibility);

            if (visibility == null)
            {
                Log.Error(
                    $"Invalid visibility '{modConfig.visibility}' in workshop.json! Should be: private, public, unlisted, or friends_only");
                return 1;
            }
        }

        List<EUGCContentDescriptorID>? contentDescriptors = null;

        if (modConfig.contentDescriptors != null)
        {
            contentDescriptors = [];
            
            foreach (string descriptorStr in modConfig.contentDescriptors)
            {
                EUGCContentDescriptorID? descriptor = ContentDescriptorFromString(descriptorStr);

                if (descriptor == null)
                {
                    Log.Error(
                        $"Invalid content descriptor '{descriptorStr}' in workshop.json! Should be: nudity, frequent_violence, adult_only, gratuitous_nudity, or general_mature");
                    return 1;
                }
                
                contentDescriptors.Add(descriptor.Value);
            }
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
                Log.Error("Tried to read mod ID from mod_id.txt, but the text could not be parsed as a mod ID!");
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

        WorkshopItemDetails? existingDetails;

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

            existingDetails = await GetWorkshopItemDetails(workshopItem);
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
            
            existingDetails = await GetWorkshopItemDetails(workshopItem);
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
            existingDetails = new WorkshopItemDetails
            {
                contentDescriptors = [],
                dependencies = []
            };
        }

        if (existingDetails == null)
        {
            return 1;
        }
        
        Log.Info($"Uploading '{modConfig.title}' to the steam workshop with item ID {workshopItem.m_PublishedFileId}...");

        UGCUpdateHandle_t updateHandle = SteamUGC.StartItemUpdate(_sts2AppId, workshopItem);

        if (modConfig.title != null)
        {
            if (!SteamUGC.SetItemTitle(updateHandle, modConfig.title))
            {
                Log.Warn("Failed to set title!");
            }
        }

        if (modConfig.description != null)
        {
            if (!SteamUGC.SetItemDescription(updateHandle, modConfig.description))
            {
                Log.Warn("Failed to set description!");
            }
        }

        if (visibility != null)
        {
            if (!SteamUGC.SetItemVisibility(updateHandle, visibility.Value))
            {
                Log.Warn("Failed to set visibility!");
            }
        }

        if (contentDescriptors != null)
        {
            // Add new descriptors
            foreach (EUGCContentDescriptorID descriptor in contentDescriptors)
            {
                if (!existingDetails.Value.contentDescriptors.Contains(descriptor))
                {
                    if (!SteamUGC.AddContentDescriptor(updateHandle, descriptor))
                    {
                        Log.Warn($"Failed to add content descriptor {descriptor}");
                    }
                }
            }
            
            // Remove descriptors
            foreach (EUGCContentDescriptorID descriptor in existingDetails.Value.contentDescriptors)
            {
                if (!contentDescriptors.Contains(descriptor))
                {
                    if (!SteamUGC.RemoveContentDescriptor(updateHandle, descriptor))
                    {
                        Log.Warn($"Failed to remove content descriptor {descriptor}");
                    }
                }
            }
        }

        if (modConfig.tags != null)
        {
            if (!SteamUGC.SetItemTags(updateHandle, modConfig.tags))
            {
                Log.Warn("Failed to set tags!");
            }
        }

        if (!SteamUGC.SetRequiredGameVersions(updateHandle, modConfig.minBranch ?? "", modConfig.maxBranch ?? ""))
        {
            Log.Warn("Failed to set required game versions!");
        }

        if (!SteamUGC.SetItemContent(updateHandle, contentDirectoryInfo.FullName))
        {
            Log.Warn("Failed to upload content!");
        }

        if (!SteamUGC.SetItemPreview(updateHandle, imageFileInfo.FullName))
        {
            Log.Warn("Failed to set preview image!");
        }

        SteamAPICall_t updateItemCall = SteamUGC.SubmitItemUpdate(updateHandle, modConfig.changeNote ?? "");
        using SteamCallResult<SubmitItemUpdateResult_t> updateItemCallResult = new(updateItemCall);

        while (!updateItemCallResult.Task.IsCompleted)
        {
            await Task.Delay(500);
            
            EItemUpdateStatus status =
                SteamUGC.GetItemUpdateProgress(updateHandle, out ulong bytesProcessed, out ulong bytesTotal);

            if (bytesTotal > 0)
            {
                Log.Info($"Status: {status}, bytes processed: {bytesProcessed}/{bytesTotal} ({(float)bytesProcessed/bytesTotal:P2})");
            }
            else
            {
                Log.Info($"Status: {status}");
            }
        }
        
        SubmitItemUpdateResult_t updateItemResult = await updateItemCallResult.Task;

        if (updateItemResult.m_eResult != EResult.k_EResultOK)
        {
            Log.Error($"Error occurred while uploading to the workshop! Result: {updateItemResult.m_eResult}");
            return 1;
        }
        
        // Since we successfully uploaded, if it didn't exist already, put a mod_id.txt in the directory for later, to
        // identify which mod ID this is.
        if (modIdTxt == null || modIdTxt.Value != workshopItem.m_PublishedFileId)
        {
            await using FileStream fileStream = modIdFile.Open(FileMode.Create);
            await using StreamWriter writer = new(fileStream);
            writer.WriteLine(workshopItem.m_PublishedFileId);
        }

        if (!await UpdateDependencies(workshopItem, existingDetails.Value.dependencies, modConfig.dependencies ?? []))
        {
            return 1;
        }

        Log.Info($"Successfully uploaded '{modConfig.title}' to the workshop with id {workshopItem.m_PublishedFileId}! Browsing to the item in Steam.");
        SteamFriends.ActivateGameOverlayToWebPage($"steam://url/CommunityFilePage/{workshopItem.m_PublishedFileId}");
        
        return 0;
    }

    private static async Task<bool> UpdateDependencies(PublishedFileId_t workshopItem, List<ulong> existingDependencies, List<ulong> newDependencies)
    {
        bool modified = false;
        bool succeeded = true;
        
        // Iterate new dependencies, adding dependencies that didn't exist
        foreach (ulong dependency in newDependencies)
        {
            if (!existingDependencies.Contains(dependency))
            {
                succeeded = succeeded && await AddDependency(workshopItem, dependency);
                modified = true;
            }
        }
        
        // Iterate existing dependencies, removing dependencies that no longer exist
        foreach (ulong dependency in existingDependencies)
        {
            if (!newDependencies.Contains(dependency))
            {
                succeeded = succeeded && await RemoveDependency(workshopItem, dependency);
                modified = true;
            }
        }

        if (!modified)
        {
            Log.Info("No modifications were made to dependencies.");
        }

        return succeeded;
    }

    private static async Task<bool> AddDependency(PublishedFileId_t workshopItem, ulong dependency)
    {
        SteamAPICall_t call = SteamUGC.AddDependency(workshopItem, new PublishedFileId_t(dependency));
        using SteamCallResult<AddUGCDependencyResult_t> callResult = new(call);
        AddUGCDependencyResult_t result = await callResult.Task;

        if (result.m_eResult != EResult.k_EResultOK)
        {
            Log.Error($"Failed to add dependency on {dependency}! Result: {result.m_eResult}");
            return false;
        }

        Log.Info($"Added dependency on {dependency}");
        return true;
    }

    private static async Task<bool> RemoveDependency(PublishedFileId_t workshopItem, ulong dependency)
    {
        SteamAPICall_t call = SteamUGC.RemoveDependency(workshopItem, new PublishedFileId_t(dependency));
        using SteamCallResult<RemoveUGCDependencyResult_t> callResult = new(call);
        RemoveUGCDependencyResult_t result = await callResult.Task;

        if (result.m_eResult != EResult.k_EResultOK)
        {
            Log.Error($"Failed to remove dependency on {dependency}! Result: {result.m_eResult}");
            return false;
        }

        Log.Info($"Removed dependency on {dependency}");
        return true;
    }

    private static async Task<WorkshopItemDetails?> GetWorkshopItemDetails(PublishedFileId_t workshopItem)
    {
        Log.Info("Querying existing workshop item details... ");
        
        UGCQueryHandle_t handle = SteamUGC.CreateQueryUGCDetailsRequest([workshopItem], 1);

        try
        {
            // Children (dependencies) are only populated in the query results if we explicitly request them.
            SteamUGC.SetReturnChildren(handle, true);

            SteamAPICall_t call = SteamUGC.SendQueryUGCRequest(handle);
            using SteamCallResult<SteamUGCQueryCompleted_t> callResult = new(call);
            SteamUGCQueryCompleted_t queryResult = await callResult.Task;

            if (queryResult.m_eResult != EResult.k_EResultOK)
            {
                Log.Warn(
                    $"Couldn't get details for item {workshopItem.m_PublishedFileId}! Error: {queryResult.m_eResult}");
                return null;
            }

            if (!SteamUGC.GetQueryUGCResult(handle, 0, out SteamUGCDetails_t details))
            {
                Log.Warn($"Couldn't read query result for item {workshopItem.m_PublishedFileId}.");
                return null;
            }

            WorkshopItemDetails result = new()
            {
                contentDescriptors = [],
                dependencies = []
            };

            uint numChildren = details.m_unNumChildren;
            if (numChildren > 0)
            {
                // GetQueryUGCChildren returns all children of the item (at result index 0) in a single call.
                // The array must be sized to the number of children; there is no pagination for children.
                PublishedFileId_t[] cache = new PublishedFileId_t[numChildren];
                if (!SteamUGC.GetQueryUGCChildren(handle, 0, cache, numChildren))
                {
                    Log.Warn($"Failed to read dependencies for item {workshopItem.m_PublishedFileId}.");
                }

                foreach (PublishedFileId_t dependency in cache)
                {
                    if (dependency.m_PublishedFileId != 0)
                    {
                        result.dependencies.Add(dependency.m_PublishedFileId);
                    }
                }

                if (result.dependencies.Count > 0)
                {
                    Log.Info($"Found {result.dependencies.Count} dependencies.");
                }
            }

            // There's currently a maximum of 5 content descriptors.
            const int maxContentDescriptors = 5;
            EUGCContentDescriptorID[] contentDescriptors = new EUGCContentDescriptorID[maxContentDescriptors];
            SteamUGC.GetQueryUGCContentDescriptors(handle, 0, contentDescriptors, maxContentDescriptors);
            result.contentDescriptors.AddRange(contentDescriptors);

            return result;
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

            if (result.m_eResult != EResult.k_EResultOK)
            {
                Log.Warn($"Couldn't confirm existence of workshop item {workshopItem.m_PublishedFileId}. Error: {result.m_eResult}");
                return false;
            }

            if (!SteamUGC.GetQueryUGCResult(handle, 0, out SteamUGCDetails_t details))
            {
                Log.Warn($"Couldn't read query result for workshop item {workshopItem.m_PublishedFileId}.");
                return false;
            }

            if (details.m_eResult == EResult.k_EResultFileNotFound)
            {
                return false;
            }
            else if (details.m_eResult != EResult.k_EResultOK)
            {
                Log.Warn($"Couldn't confirm existence of workshop item {workshopItem.m_PublishedFileId}. Error: {details.m_eResult}");
                return false;
            }

            return true;
        }
        finally
        {
            SteamUGC.ReleaseQueryUGCRequest(handle);
        }
    }

    private static ERemoteStoragePublishedFileVisibility? VisibilityFromString(string visibility)
    {
        return visibility.Trim().ToLowerInvariant() switch
        {
            "private" => ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPrivate,
            "public" => ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic,
            "unlisted" => ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityUnlisted,
            "friends" => ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityFriendsOnly,
            "friendsonly" => ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityFriendsOnly,
            "friends_only" => ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityFriendsOnly,
            _ => null
        };
    }

    private static EUGCContentDescriptorID? ContentDescriptorFromString(string contentDescriptor)
    {
        return contentDescriptor switch
        {
            "nudity" => EUGCContentDescriptorID.k_EUGCContentDescriptor_NudityOrSexualContent,
            "frequent_violence" => EUGCContentDescriptorID.k_EUGCContentDescriptor_FrequentViolenceOrGore,
            "adult_only" => EUGCContentDescriptorID.k_EUGCContentDescriptor_AdultOnlySexualContent,
            "gratuitous_nudity" => EUGCContentDescriptorID.k_EUGCContentDescriptor_GratuitousSexualContent,
            "general_mature" => EUGCContentDescriptorID.k_EUGCContentDescriptor_AnyMatureContent,
            _ => null
        };
    }
}
