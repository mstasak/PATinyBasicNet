# Palo Alto Tiny Basic for .Net/Windows
"Copyleft, all wrongs reserved"

Reimplementing the famous Dr. Wang's Palo Alto tiny basic, a 2K interpreter for the 8080A microcomputer, in a Windows .Net console project.

## Status
~ 90% done with initial coding, Let (assignment), Print, For-Next, Goto, Gosub, Rem, Return, If, Wait, Stop statements, work, along with the line editor.  Input statement ready to work.
Code cleanup, documentation and testing not started.

## Plans
Beef it up with several features:

(likely)
- long variable names
- while loops
- if-else statement, or maybe something more normal like if cond then ... elseif cond2 then ... else ... endif?
- thunderstrike audio effect whenever a GOTO is executed. (just kidding!)

(possible)
- String, Int32, and Double data types (maybe bool, byte, int64 as well)
- full expression operators list (add bitwise, logical, exponentiation, maybe short-circuiting, null-coalesce, assignment ops)
- Mac and Linux (incl Pi) ports or build configurations (should be pretty easy at present)
- a string manipulation library
- a file access library
- a graphics window
- a graphics drawing library (2D)
- maybe a turtlegraphics lib
- a settings library
- some form of variable scoping - perhaps with SCOPE "NAME"/ENDSCOPE "NAME" statements, and out-of-scope references via scopename.variablename syntax.  Maybe also [global/nearest].variablename syntax.  Hmm...

(doubtful - much work just to make an inferior version of vb6/vb.net)
- a GUI editor
- sound creation
- a database access library
- network access? net service access? JSON lib?
- some debugging tools (trace, dump, watch, maybe line debugging)
- Subs and Functions with passed arguments and independent variable scopes
- built-in list and dictionary objects. Maybe queue, stack, set, tree, bytearray,
- some kind of error handling
- constants
- importable modules with private details/limited public visibility
- extensibility system to define new statements and libraries accessed via DLL calls
- compatibility modes, to enforce strict original version or allow enhancements (maybe fork enhancements instead)
- possibly some gaming support, including DirectX input?
- Idea: User-defined statements
- Mouse input (not sure if this is practical without some sort of event system)
- classes/structs/records/etc. Some custom data structure, preferably with OOP capabilities.
