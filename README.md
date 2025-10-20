# Jellies

Simple Match-3 game.

## Play

Download the latest release of the game at <https://github.com/Souchy/Jellies/releases>

## Game & Code editors Setup

1. Download Godot 4.5.1 with .NET <https://godotengine.org/download/archive/4.5.1-stable/>
2. Add an environment variable called GODOT pointing to the godot executable file.
3. Have .NET >=9.0 installed
4. Start Godot.
5. Import the project file at "Jellies/project.godot".
6. In Godot, go to Editor > General > Dotnet > Editor. Choose your C# editor.
7. When double-clicking a .cs file in the Godot editor, it will open your C# editor with the game's solution. Or open it directly at "Jellies/Jellies.sln"

The environment variable enables Visual Studio and VSCode launch configurations to start the game and debug it.  
For VSCode, also download the Godot extensions: "C# Tools for Godot" and "godot-tools".

## Releases

- Nightly release is updated on push to main.
- Version releases are published on tags pushed starting with "v" like "v1.0.0"
