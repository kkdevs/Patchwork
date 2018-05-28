This is a fork of the KK engine codebase adding some more features to it

* A bit of performance improvements
* Configurable edit/gameplay (gender, scenario and location restrictions)
* Configurable main graphics (AA etc) and rendering (texture sizes, physics..)
* Configurable sliders instead of hardcoded ones
* Configurable shaders for hair, rim effect, per character
* Configurable game metadata, on the spot, as CSV files

## Card and class save compatibility

Both class save and card files "degrade" in features when saved here and then
loaded in vanilla engine. For example, if right side slider is used and vanilla
doesnt have it both sides will use the value for left. Same with other
settings - whatever new is saved is simply not used by vanilla and
ignored, or filled with approximate default if possible.

## Binary plugins compatibility

Is preserved for public apis (ie just straight harmony hooks are fine), but
IL recompiling plugins will sometimes break when expected opcodes are not found.
If there is such a conflict, it can be typically worked around by fixig up the
engine with a compat layer for that particular plugin - open an issue for it.
