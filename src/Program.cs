using System.CommandLine;

namespace ModUploader;

public static class Program
{
    public static int Main(string[] args)
    {
        // Since System.CommandLine doesn't support name-less args: Check if the first arg looks like a directory and go
        // straight to upload if it does
        if (args.Length > 0 && new DirectoryInfo(args[0]).Exists)
        {
            Log.Info(
                "Since you have supplied no arguments, I'll create a workspace in a default location.\nAdd --help to the command if you wish to know what else this program can do.");
            UploadCommand.UploadWorkspace(new DirectoryInfo(args[0]), null).RunSynchronously();
            return 0;
        }

        Option<DirectoryInfo> newWorkspaceOption =
            new Option<DirectoryInfo>(["--workspace", "-w"], "The workspace directory to create.")
                { IsRequired = true };
        
        Option<DirectoryInfo> uploadWorkspaceOption =
            new Option<DirectoryInfo>(["--workspace", "-w"], "The workspace directory to upload.")
                { IsRequired = true };

        Option<DirectoryInfo> deleteWorkspaceOption =
            new Option<DirectoryInfo>(["--workspace", "-w"], "The workspace directory to delete.");
        
        Option<ulong?> uploadItemIdOption = new Option<ulong?>(["--id", "-i"], "The ID of the workshop item to update. If this is not specified, we'll look for mod_id.txt in the workspace. If it is also not present, a new item is created.");
        Option<ulong?> deleteItemIdOption = new Option<ulong?>(["--id", "-i"], "The ID of the workshop item to delete. If this is not specified, we'll look for mod_id.txt in the workspace.");

        Command newCommand = new("new", "Create a new workspace for a new mod.")
        {
            newWorkspaceOption
        };

        newCommand.SetHandler(NewCommand.CreateNewWorkspace, newWorkspaceOption);

        Command uploadCommand = new("upload", "Upload a new mod or update a mod to the Steam Workshop.")
        {
            uploadWorkspaceOption,
            uploadItemIdOption
        };
        
        uploadCommand.SetHandler(UploadCommand.UploadWorkspace, uploadWorkspaceOption, uploadItemIdOption);

        Command removeCommand = new("remove", "Remove the mod from the workshop. Your local workspace will be unaffected.")
        {
            deleteWorkspaceOption,
            deleteItemIdOption
        };
        
        removeCommand.SetHandler(RemoveCommand.Remove, deleteWorkspaceOption, deleteItemIdOption);

        RootCommand rootCommand = new("Utility for creating and updating STS2 Steam Workshop mods.");
        rootCommand.AddCommand(newCommand);
        rootCommand.AddCommand(uploadCommand);
        rootCommand.AddCommand(removeCommand);
        rootCommand.SetHandler(NewCommand.CreateNewWorkspace);

        return rootCommand.InvokeAsync(args).Result;
    }
}