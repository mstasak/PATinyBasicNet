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
    internal string FormatedMessage(Interpreter env) {
        return MessageDetail + "\r\n" +
            env.CurrentLine + "\r\n" +
            new string(' ', env.LinePosition) + "^\r\n";
    }
}
