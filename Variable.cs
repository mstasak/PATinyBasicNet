using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewPaloAltoTB;

internal enum VariableType {
    //Byte,
    Short,
    //Int,
    //Long,
    //Single,
    //Float,
    //Double,
    //Bool,
    //Char,
    String,
    //Array,
    ShortArray,
    //Class,
    //Struct,
    //Set,
    //Dict,
    //Object,
    //Variant,
}
internal class Variable {
    internal string VName;
    internal object VValue;
    internal VariableType VType;

    //internal static Variable Shared => shared.Value;
    //private static readonly Lazy<Variable> shared = new(() => new Variable());


    internal Variable(string vName, object vValue, VariableType vType) {
        VName = vName;
        VValue = vValue;
        VType = vType;
    }

    internal Variable(string vName, string vValue) {
        VName = vName;
        VValue = vValue;
        VType = VariableType.String;
    }

    internal Variable(string vName, short vValue) {
        VName = vName;
        VValue = vValue;
        VType = VariableType.Short;
    }

    internal Variable(string variableName, short[] shorts) {
        VName = variableName;
        VValue = shorts;
        VType = VariableType.ShortArray;
    }

    public short? ShortValue => VType switch {
        VariableType.Short => (short?)VValue,
        //VariableType.String => null,
        //VariableType.ShortArray => null,
        _ => null
    };
}
