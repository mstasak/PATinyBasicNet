100 REM Example program for New Palo Alto Tiny Basic
110 REM STATEMENTS:
120 REM   LET, FOR-NEXT, IF, INPUT, PRINT, WAIT, REM, GOTO, GOSUB-RETURN, STOP
130 REM VARIABLES:
140 REM   A-Z, @(int index)
150 REM FUNCTIONS:
160 REM   ABS(n), RND(n), SIZE()
170 REM   note: INP(), PEEK, USR() are not implemented, as port level I/O,
180 REM         memory access, and arbitrary address execution are typically
190 REM         unavailable in an environment like Windows

300 REM ASSIGNMENTS
310 I = 1
320 LET J = 2 * I + 8
330 LET C = (F - 32) * 5 / 9, K = C + 273
340 R = RND(10); REM a random number from 1 to 10

400 REM LOOPS: FOR-NEXT
410 FOR I = 1 TO 10
420   PRINT I
430 NEXT I

500 REM LOOP, CONDITIONAL, GOTO
510 FOR I=10 TO 1 STEP -1
520   PRINT #(I), I
530 NEXT I
540 I=10
550 PRINT "*",
560 I=I-1
570 IF I>0 THEN GOTO 550
