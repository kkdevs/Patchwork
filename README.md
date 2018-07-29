This is a fork of the KK engine adding some more features to it

* Performance improvements
* Exposing (almost) all game serialized metadata
* Various engine internals made scriptable/configurable
* Removal of some arbitrary restrictions
* a lot of small ad-hoc improvements/bugs introduced to the engine

## Please keep up to date

If you update game to latest DLC, you'll need to use latest release of pw as well.
You'll also need to watch out for fix releases prior to DLC update.
You can track it via RSS - https://github.com/kkdevs/Patchwork/releases.atom

## No warranty

Patchwork is very far from being ready for end user use, it's move fast and
break things at the moment. It's meant mainly for advanced users somewhat
familiar with modding, unity or the predecessors of this engine (PH, HS).

**DO NOT BUNDLE IT IN MODPACKS**. Do not bug other people to fix compatibility
for patchwork, even if they did that, chances are it would get broken by
engine change yet again anyway. When mentioning it somewhere, always note
that this is meant only for experienced users who know how to troubleshoot
compatibility issues.

## Card and class save compatibility

Cards remain "mostly" compatible, in thata features added by patchwork
are simply stripped when loaded elsewhere.

Game save files are compatible only if no custom save script is used (currently
'ghettosave'). Custom saves use their own and typically are stored in different
folder. The format change is to make save files smaller (400kb/class vs 8MB/class).

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
edge and almost inevitably buggy. Beware that older versions typically don't work
with latest game DLC.


