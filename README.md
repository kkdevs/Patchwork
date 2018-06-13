This is a fork of the KK engine adding some more features to it

* A bit of performance improvements
* Exposing (almost) all game serialized metadata as CSV files
* Various engine internals made configurable
* Removal of some arbitrary restrictions
* a lot of small ad-hoc improvements/bugs introduced to the engine

## Card and class save compatibility

Both class save and card files "degrade" in features when saved here and then
loaded in vanilla engine. For example, if right side slider is used and vanilla
doesnt have it both sides will use the value for left. Same with other
settings - whatever new is saved is simply not used by vanilla and
ignored, or filled with approximate default if possible.

Newly introduced fields are serialized the same way as vanilla data since most
of those are properties added to existing classes. Vanilla game can deal with
such extensions gracefuly, as the serialization mechanism (msgpack in string
key mode) was designed to ignore all unknown fields from the get go for
extensibility in the first place.

## Binary-only plugins compatibility

Is preserved for public apis used by existing plugins most of the time - ie
the most common stuff will continue to work.

This leeway doesn't go far overall, however.

Non-hooked classes, field and method access can be assumed compatible only on
source level as those are frequently refactored into different types:

Fields can be turned into properties, methods into a delegate or extension,
array types may become different iterator type altogether only with generic arg
kept etc.

In the worst case, the plugin needs to be decompiled and rebuilt again
to reference correct types.

## Versioning scheme

Major changes (typically heavier modification of the engine) are designated as
new "mark", as in iteration of prototypes.

A new mark almost always introduces slew of new bugs, and may or may not
necessarily bring user-visible features as the changes can be infrastructural
rather than user-facing. 

Each mark has actual versions - these are always fixes for bugs introduced by
the new mark, or trivial toggles added under default-off checkbox.

Marks with high versions tend to be most stable, whereas latest mark is bleeding
edge and almost inevitably buggy.

## Stability, end user support, contact

Patchwork is very far from being ready for end user use, it's move fast and
break things at the moment. It's meant mainly for advanced users somewhat
familiar with modding, unity or the predecessors of this engine (PH, HS).

Public support is exclusively via github tickets. There are better venues to
get in touch hidden as easter eggs.


