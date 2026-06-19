using System.Text.Json;
using Steamworks;

namespace ModUploader;

public static class RemoveCommand
{
    public static async Task<int> Remove(DirectoryInfo? workspaceDirectory, ulong? itemIdArg)
    {
        if (!Program.InitializeSteam())
        {
            return 1;
        }
        
        ulong? modId = null;

        if (itemIdArg != null)
        {
            modId = itemIdArg;
        }
        else if (workspaceDirectory != null)
        {
            if (!workspaceDirectory.Exists)
            {
                Log.Error($"No directory at {workspaceDirectory}!");
                return 1;
            }

            FileInfo modIdFile = new(Path.Combine(workspaceDirectory.FullName, "mod_id.txt"));
            if (modIdFile.Exists)
            {
                await using FileStream modIdStream = modIdFile.OpenRead();
                using StreamReader reader = new(modIdStream);
                string modIdStr = (await reader.ReadToEndAsync()).Trim();

                if (!ulong.TryParse(modIdStr, out ulong parsedModId))
                {
                    Log.Error("Tried to read mod ID from mod_id.txt, but the text could not be parsed as a mod ID!");
                    return 1;
                }

                modId = parsedModId;
            }
            else
            {
                Log.Error("No mod_id.txt found in the workspace! Specify the ID using the id argument instead");
                return 1;
            }
        }
        else
        {
            Log.Error("At least one of workspace or id must be specified!");
            return 1;
        }

        Log.Info($"Removing workshop item with id {modId.Value}...");

        SteamAPICall_t removeCall = SteamUGC.DeleteItem(new PublishedFileId_t(modId.Value));
        using SteamCallResult<DeleteItemResult_t> removeCallResult = new(removeCall);
        DeleteItemResult_t removeResult = await removeCallResult.Task;

        if (removeResult.m_eResult != EResult.k_EResultOK)
        {
            Log.Error($"Deletion failed! Result: {removeResult.m_eResult}");
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
        
        return 0;
    }
    
}
