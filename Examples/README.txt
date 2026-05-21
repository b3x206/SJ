For UPM package, these are compiled too but since these are internal it shouldn't leak into the root project when included. 

If you don't want these to compile you can
* copy the files manually to your worktree, then delete Examples
* git submodule, delete Examples and then create your own commit

