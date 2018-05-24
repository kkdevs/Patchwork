Patchwork is a mod of KK made through direct edits to the game code.

Currently it roughly amounts to:

* Removing all gender-specific checks for MC, editor, classroom seats, H etc
* Performance improvements
* Fully exposed unity graphics settings
* New built-in body/face sliders (currently only few and hardcoded)
* Some shader extensions, eg shadow color for hairs
* Re/Deserializing contents of .unity3d files into CSVs for easy modding on the spot
* Many of the internal toggles (texture sizes, physics...) can be tweaked at run time
* Many bugs

## Files and folders used by PW:

### UserData/csv
All internal game lists with all scenario, actions, dialogue and properties
as well as japanese text, mirroring top-level abdata folder structure. The game
dumps new entries whenever it loads it for first use from unity3d file, and then
uses the actual .csv file on disk from then on (accepting any edits you make to
it).

The only exception is ``list/characustom`` - the DLC subsystem is extensively
hooked by bepinex sideloader and the mod doesn't like any changes in there, so
it is avoided to keep compatibility until better solution is found.

### UserData/dll

So called "AssLoader". This is used for ad-hoc scripting of the game with auto
reloading on edit. **The DLLs placed in there must be plain Unity scripts**.
There is no plugin api, there's simply only unity API. Very few plugins are
behaved enough to load through this mechanism (autotranslator, for example, does).

### UserData/config.json

All internal settings to PW preserved between sessions.

## Card and class save compatibility

Both class save and card files "degrade" in features when saved in PW and run
in vanilla. For example, if right side slider is used and vanilla doesnt have it
both sides will use the value for left. Same with other settings - whatever new
is saved is simply not used by vanilla and simply ignored or filled with
approximate default when possible.

## Plugin compatibility

Is generally a tall order. There's a gentlemans agreement to not change public
interfaces. But private sigs, as well as things such as class hiearchy is all
fair game. As long the plugin doesn't access private fields or make assumptions
about OO composition, it should stay compatible.

