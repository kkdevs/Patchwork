This is a fork of the KK engine adding some more features to it

* A bit of performance improvements
* Exposing (almost) all game serialized metadata as CSV files
* Various engine internals made configurable
* Removal of some arbitrary restrictions
* a lot of small ad-hoc improvements/bugs introduced to the engine

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
edge and almost inevitably buggy.

## Stability, end user support, contact

Patchwork is very far from being ready for end user use, it's move fast and
break things at the moment. It's meant mainly for advanced users somewhat
familiar with modding, unity or the predecessors of this engine (PH, HS).

Public support is exclusively via github tickets. There are better venues to
get in touch hidden as easter eggs.

## Troubleshooting bugs

Here is how you troubleshoot in general, ordered by complexity:

* Post output log
* Could be a zipmod. Delete userdata/mod and untick unzip in scripts tab.
* Could be some other plugin/script. Uncheck all entries in scripts tab so pw runs alone.
* Could be launcher exe issue, try hardpatch
* Could be a card issue. Try using non-modded cards only.
* If its a hardmod collision, try with unmodded abdata


