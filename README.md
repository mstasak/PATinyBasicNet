# Palo Alto Tiny Basic for .Net/Windows
"Copyleft, all wrongs reserved"

Reimplementing the famous Dr. Wang's Palo Alto tiny basic, a 2K interpreter for the 8080A microcomputer, in a Windows .Net console project.

## Status
~ 66% done, Let (assignments) and Print statements work, along with the line editor.  Rem, Goto, Wait, If statements are ready to test.  Input, For, Gosub are ready to work.

## Plans
Beef it up with several features:
- a graphics window
- a GUI editor
- a graphics drawing library (2D)
- maybe a turtlegraphics lib
- a string manipulation library
- sound creation
- a database access library
- a file access library
- network access? net service access? JSON lib?
- some debugging tools (trace, dump, watch, maybe line debugging)
- Subs and Functions with passed arguments and independent variable scopes
- String, Int32, and Double data types (maybe bool, byte, int64 as well)
- full expression operators list (add bitwise, logical, exponentiation, maybe short-circuiting, null-coalesce, assignment ops)
- built-in list and dictionary objects. Maybe queue, stack, set, tree, bytearray,
- long variable names
- while loops
- some kind of error handling
- constants
- importable modules with private details/limited public visibility
- extensibility system to define new statements and libraries accessed via DLL calls
- Mac and Linux (incl Pi) ports or build configurations (should be pretty easy at present)
- compatibility modes, to enforce strict original version or allow enhancements (maybe fork enhancements instead)
- possibly some gaming support, including DirectX input?
- Idea: User-defined statements
- Mouse input (not sure if this is practical without some sort of event system)
