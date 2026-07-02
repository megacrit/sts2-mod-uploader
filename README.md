# Slay the Spire 2 Mod Uploader

## Creating a new mod
1. Double-clicking on the ModUploader.exe file should create a new folder called `NewModWorkspace`.
2. Rename `NewModWorkspace` to whatever you want.
3. Place your mod content in the `content` directory within the workspace. This is what will be uploaded to the Steam workshop.
4. Fill in the details for the `workshop.json` located in your mod's workspace. If the fields are unclear, refer to the other README.md file located in the mod's workspace for descriptions of what each field does. For long descriptions, you can create a `description.txt` file in the workspace instead of using the `description` field in `workshop.json`.
5. Replace the `image.png` in your mod's workspace with an image of the same name that you wish to use for your mod. The image must be less than 1MB because of limits on Steam's backend.
6. Open a command line window inside the folder with the ModUploader executable.
7. Run `ModUploader.exe upload -w <workspace-folder>` to upload the mod.

## Updating an existing mod
1. Place your updated mod files inside the `content` directory of the workspace.
2. Optionally fill out the `changeNote` field inside the `workshop.json` file with a description of the changes.
3. Open a command line window inside this folder.
4. Run `ModUploader.exe upload -w <workspace-folder>` to update the mod. The mod ID will be automatically filled from the `mod_id.txt` located in the directory.

## Reporting Issues
After running the uploader, you should see a file called "mod-uploader.log" appear in the directory. Send this file to the devs along with any info about what you were trying to do.
