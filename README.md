## through penis, unity

You need to put game DLLs in Managed folder (just straight copy) suffices,
but DONT overwrite those which are already there.

Set Patchwork as startup project. Select release and x64 for solution.
Press ctrl+shift+b, you end up with iphlpapi.exe in `x64/Release`.
This is not very debuggable though.

**For debugging:**
There's also assembly-csharp.dll generated in `Managed` folder, just symlink
your game to it, with the usual dnspy debugging setup as per:

https://github.com/0xd4d/dnSpy/wiki/Debugging-Unity-Games

Note that iphlp DOES NOT work with debugging version of mono/exe, as it uses
debugging apis on its own. Use symlinks to make folder structure for dnspy.
You can also use visual studio since we have full pdb/mdb now, but the qol
in there is almost as bad as dnspy, dont bother.

To get the submodule repo, join qSUwWab on Dis\*\*rd and post your ssh key.
