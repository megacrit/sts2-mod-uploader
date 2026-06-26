# Steam Workshop Mod

## Directory layout
* workshop.json -- The config describing the Steam workspace mod.
* content       -- Where you should place the mod files to be uploaded to Steam Workshop.
                   This should be at least a JSON file, and probably a PCK & DLL.
* image.png     -- The image shown in the Steam Workshop. Replace with your own!
                -- Must be less than 1MB.
* README.md     -- This readme document

## `workshop.json` Properties

This describes what gets uploaded to the workshop.

Most properties can be substituted with `null` or removed from the JSON if you wish for them to remain unchanged after the initial upload.

```
{
  "title": "",                -- The title of your mod.
  "description": "",          -- The description.
  "visibility": "private",    -- The visibility status of the mod. 
                                  Options include: "private", "public", "unlisted", "friends_only".
  "changeNote": "",           -- A note for describing the newest changes you've made to your users.
  "tags": [],                 -- A list of tags to search for your mod by. 
                                 Note: the tag "Tools & APIs" is reserved for mods that function as tools or APIs.
  "dependencies": []          -- A list of mods that your mod depends on.
                                 These should be mod IDs (can be found in the workshop URL).
  "contentDescriptors": []    -- A list of mature content descriptors that apply to your mod.
                                 Options include: "nudity", "frequent_violence", "adult_only",
                                 "gratuitous_nudity", "general_mature"


                              -- The below two seem to have weird behavior but I'm including them
                                 in case Steam fixes it. Prefer updating them on the web instead.
                                 Set to `null` or remove them to say that you support all versions.
  "minBranch": "public-beta"  -- Minimum branch supported by this mod.
  "maxBranch": "public"       -- Maximum branch supported by this mod.
}
```
