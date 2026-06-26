using System.CommandLine;
using Steamworks;

namespace ModUploader;

public static class Program
{
    private static bool _shutdown;
    private static Task? _steamCallbacksTask;
    
    public static int Main(string[] args)
    {
        try
        {
            // Since System.CommandLine doesn't support name-less args: Check if the first arg looks like a directory and go
            // straight to upload if it does
            if (args.Length > 0 && new DirectoryInfo(args[0]).Exists)
            {
                return UploadCommand.UploadWorkspace(new DirectoryInfo(args[0]), null).GetAwaiter().GetResult();
            }

            if (args.Length == 0)
            {
                Log.Info(
                    "Since you have supplied no arguments, I'll create a workspace in a default location.\nAdd --help to the command if you wish to know what else this program can do.");
                NewCommand.CreateNewWorkspace();
                return 0;
            }

            Option<DirectoryInfo> newWorkspaceOption = new("--workspace", "-w")
            {
                Description = "The location in which the new workspace will be created.",
                Required = true
            };

            Option<DirectoryInfo> uploadWorkspaceOption = new("--workspace", "-w")
            {
                Description = "The directory of the workspace to upload to the workshop.",
                Required = true
            };

            Option<DirectoryInfo> deleteWorkspaceOption = new("--workspace", "-w")
            {
                Description = "The directory of the workspace which will be deleted from the workshop.",
            };

            Option<ulong?> uploadItemIdOption = new Option<ulong?>("--id", "-i")
            {
                Description =
                    "The ID of the workshop item to update. If this is not specified, we'll look for mod_id.txt in the workspace. If it is also not present, a new item is created."
            };

            Option<ulong?> deleteItemIdOption = new Option<ulong?>("--id", "-i")
            {
                Description =
                    "The ID of the workshop item to delete. If this is not specified, we'll look for mod_id.txt in the workspace."
            };

            Command newCommand = new("new", "Create a new workspace for a new mod.");
            newCommand.Options.Add(newWorkspaceOption);
            newCommand.SetAction(p => NewCommand.CreateNewWorkspace(p.GetValue(newWorkspaceOption)));

            Command uploadCommand = new("upload", "Upload a new mod or update a mod to the Steam Workshop.");
            uploadCommand.Options.Add(uploadWorkspaceOption);
            uploadCommand.Options.Add(uploadItemIdOption);
            uploadCommand.SetAction(p => UploadCommand.UploadWorkspace(p.GetRequiredValue(uploadWorkspaceOption), p.GetValue(uploadItemIdOption)));

            Command removeCommand = new("remove",
                "Remove the mod from the workshop. Your local workspace will be unaffected.");
            removeCommand.Options.Add(deleteWorkspaceOption);
            removeCommand.Options.Add(deleteItemIdOption);
            removeCommand.SetAction(p => RemoveCommand.Remove(p.GetValue(deleteWorkspaceOption), p.GetValue(deleteItemIdOption)));

            RootCommand rootCommand = new("Utility for creating and updating STS2 Steam Workshop mods.");
            rootCommand.Subcommands.Add(newCommand);
            rootCommand.Subcommands.Add(uploadCommand);
            rootCommand.Subcommands.Add(removeCommand);

            ParseResult parseResult = rootCommand.Parse(args);
            return parseResult.Invoke();
        }
        catch (Exception e)
        {
            Log.Error(e.ToString());
            throw;
        }
        finally
        {
            _shutdown = true;
            
            if (_steamCallbacksTask != null)
            {
                _steamCallbacksTask.Wait();
                SteamAPI.Shutdown();
            }
            
            Log.Close();
        }
    }

    public static bool InitializeSteam()
    {
        Log.Info("Initializing Steam");

        try
        {
            ESteamAPIInitResult result = SteamAPI.InitEx(out string initErrorMessage);

            if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
            {
                Log.Error($"Steam initialization failed! Result: {result}, message: {initErrorMessage}");
                return false;
            }
        }
        catch (Exception e)
        {
            Log.Error($"Steam initialization threw an exception: {e}");
            return false;
        }
        
        // Start running callbacks, otherwise we will never get steam call results
        _steamCallbacksTask = DoRunCallbacks();
        return true;
    }
    
    private static async Task DoRunCallbacks()
    {
        // RunCallbacks must be run periodically to flush call results
        while (!_shutdown)
        {
            SteamAPI.RunCallbacks();
            await Task.Delay(50);
        }
    }

}
