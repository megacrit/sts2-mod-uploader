namespace ModUploader;

public static class NewCommand
{
    public static Task CreateNewWorkspace()
    {
        return CreateNewWorkspace(null);
    }
    
    public static Task CreateNewWorkspace(DirectoryInfo? workspaceDirectory)
    {
        DirectoryInfo templateInfo = new(Path.Combine(AppContext.BaseDirectory, "template"));
        DirectoryInfo destination = workspaceDirectory ?? new DirectoryInfo("NewModWorkspace");
        CopyDirectoryRecursive(templateInfo, destination);
        Log.Info($"Copied template files to {destination.FullName}");
        return Task.CompletedTask;
    }
    
    private static void CopyDirectoryRecursive(DirectoryInfo sourceDir, DirectoryInfo destinationDir)
    {
        // Check if the source directory exists
        if (!sourceDir.Exists)
        {
            throw new DirectoryNotFoundException($"Template not found at {sourceDir.FullName}!\nYou might need to redownload the application.");
        }

        // Cache directories before we start copying
        DirectoryInfo[] dirs = sourceDir.GetDirectories();

        // Create the destination directory
        destinationDir.Create();

        // Get the files in the source directory and copy to the destination directory
        foreach (FileInfo file in sourceDir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir.FullName, file.Name);
            file.CopyTo(targetFilePath);
        }

        foreach (DirectoryInfo subDir in dirs)
        {
            DirectoryInfo newDestinationDir = destinationDir.CreateSubdirectory(subDir.Name);
            CopyDirectoryRecursive(subDir, newDestinationDir);
        }
    }
}
