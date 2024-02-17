using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewPaloAltoTB;
internal class ProgramLocation {
    internal string FileName;
    internal int LineNumber;
    internal int ColumnPosition;
    internal string? SrcLine;

    internal ProgramLocation(string fileName, int lineNumber, int columnPosition, string? srcLine) {
        FileName = fileName;
        LineNumber = lineNumber;
        ColumnPosition = columnPosition;
        SrcLine = srcLine;
    }
}
