using System.Text.Json;
using Steamworks;

namespace ModUploader;

public static class RemoveCommand
{
    private static AppId_t _sts2AppId = new(2868840);
    private static bool _steamIsInitialized;
    
    public static async Task<int> Remove(DirectoryInfo? workspaceDirectory, ulong? itemIdArg)
    {
        // Validation is all done. Start the upload process.
        Log.Info("Initializing Steam");

        try
        {
            ESteamAPIInitResult result = SteamAPI.InitEx(out string initErrorMessage);

            if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
            {
                Log.Info($"Steam initialization failed! Result: {result}, message: {initErrorMessage}");
                return 1;
            }
        }
        catch (Exception e)
        {
            Log.Info($"Steam initialization threw an exception: {e}");
            return 1;
        }
        
        // Start running callbacks, otherwise we will never get steam call results
        _steamIsInitialized = true;
        _ = DoRunCallbacks();
        
        ulong? modId = null;

        if (workspaceDirectory != null)
        {
            FileInfo modIdFile = new(Path.Combine(workspaceDirectory.FullName, "mod_id.txt"));
            if (modIdFile.Exists)
            {
                await using FileStream modIdStream = modIdFile.OpenRead();
                using StreamReader reader = new(modIdStream);
                string modIdStr = (await reader.ReadToEndAsync()).Trim();

                if (!ulong.TryParse(modIdStr, out ulong parsedModId))
                {
                    Log.Info("Tried to read mod ID from mod_id.txt, but the text could not be parsed as a mod ID!");
                    return 1;
                }

                modId = parsedModId;
            }
            else
            {
                Log.Info("No mod_id.txt found in the workspace! Specify the ID using the id argument instead");
                return 1;
            }
        }
        else if (itemIdArg != null)
        {
            modId = itemIdArg;
        }
        else
        {
            Log.Info("At least one of workspace or id must be specified!");
            return 1;
        }

        Log.Info($"Removing workshop item with id {modId.Value}...");

        SteamAPICall_t removeCall = SteamUGC.DeleteItem(new PublishedFileId_t(modId.Value));
        SteamCallResult<DeleteItemResult_t> removeCallResult = new(removeCall);
        DeleteItemResult_t removeResult = await removeCallResult.Task;

        if (removeResult.m_eResult != EResult.k_EResultOK)
        {
            Log.Info($"Deletion failed! Result: {removeResult.m_eResult}");
            return 1;
        }

        // Remove the ID file from the workspace after successful deletion
        if (workspaceDirectory != null)
        {
            FileInfo modIdFile = new(Path.Combine(workspaceDirectory.FullName, "mod_id.txt"));
            if (modIdFile.Exists)
            {
                modIdFile.Delete();
            }
        }

        Log.Info($"Successfully deleted workshop item with ID {removeResult.m_nPublishedFileId.m_PublishedFileId}");
        SteamAPI.Shutdown();
        _steamIsInitialized = false;
        
        return 0;
    }
    
    private static async Task DoRunCallbacks()
    {
        // RunCallbacks must be run periodically to flush call results
        while (_steamIsInitialized)
        {
            SteamAPI.RunCallbacks();
            await Task.Delay(50);
        }
    }

}