# How MonoB scripts work

All cs files in this folder are compiled as a single assembly at game runtime. Any public monobs present will be
launched as singletons, while internal and private ones must be managed manually. If a file change is detected,
the assembly is recompiled, old monobs are killed and the new assembly is loaded - provided the scripts could be
recompiled succesfuly. Otherwise we wait until scripts become "good" again.

All scripts must either perform some harmless one-shot action from Awake, or must implement OnDestroy to clean
up on reload. Scripts which don't do this will be forcefuly terminated on relaod regardless, and it will
probably corrupt game state.

If a script needs some library, simply put the dll in here as well.

# Evaluator extensions

Scripts can also extend the base class of evaluator command line. Any method or field introduced to ScriptEnv class instantly
becomes top level evaluator command or variable. This class should be always partial, so each script can implement
commands it wants in self-contained manner.

