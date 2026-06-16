For UPM package, these are compiled too but since these are internal it shouldn't leak into the root project when included. 

If you don't want these to compile you can
* copy the files manually to your worktree, then delete Examples
* git submodule, delete Examples and then create your own commit (somehow)

> These files provide a psuedo entry point, simulating a console application. <br>
  You can copy these to any MonoBehaviour Start or Node _Ready (or anything similar) to simulate it and remove references to "args" or replace it with your own fields.
