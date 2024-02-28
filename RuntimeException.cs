using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewPaloAltoTB;
internal class RuntimeException: Exception {
    internal string MessageDetail { get; set; }
    internal RuntimeException(string messageDetail) {
        MessageDetail = messageDetail;
    }
    internal string FormatedMessage(CodeInterpreter env) {
        return MessageDetail + "\r\n" +
            CodeParser.Shared.Line + "\r\n" +
            new string(' ', CodeParser.Shared.LinePosition) + "^\r\n";
    }
}
