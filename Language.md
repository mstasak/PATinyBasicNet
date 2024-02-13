Starting from Dr. Wang's Palo Alto Tiny Basic 1.0, I'm hoping to add some useful features.
```
Subs

Functions

Deprecated GoTos

No GoSubs

No line numbers

Alphanumeric labels

Named variables

Option Explicit

'EOL Comments

Variable scoping, access, and referencing prefixes (parent.[parent.]*, ancestor., 'modulename'., global.)

Struct

Class

why am I doing this instead of using Rosalyn/VB.NET?

Typed variables (dynamic unless explicitly specified)

List, Dictionary, Set(?)
Stack? Queue? BinaryTree?
Array? (else build functionality into list)

Enum

For, ForEach with break(n=1), continue

Try-Catch-Finally

Dispose(v)

Using

Sequence-End Sequence with Repeat, Break

Repeat-While/Until, Do-While/Until, While/Until() Do

Optional curly braces in place of block delimiters

/*   /* nested 
multiline comment */  comment */

#ifdef-#endif[def] build/preproc constant
#define preproc constant

db builtin object
dbtable(tblname)
dbquery(query, params)
dbcommand(query)
dbresult

settings(appname=something) dictionary object


New types: (int8, int16, int32, int64, int, [u](inttype), [s](inttype), byte, word, dword, qword, sbyte, sword, sdword, sqword, string, char, char16, bool

nullable types?

date
time
datetime

float, double, others?

lambda?

ASMBlock?

A name!  PATB++? OLTRTA (one lang to rule them all)

byref, byval, opt byref, opt byval, readonly, immutable (instance can't be modified)

what about multiple files? namespaces?  RPCs? WebMethods?
string formatting
todo. notimplemented, assert, test, 

```