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

    internal ProgramLocation(string fileName, int lineNumber, int columnPosition) {
        FileName = fileName;
        LineNumber = lineNumber;
        ColumnPosition = columnPosition;
    }
}
