using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NewPaloAltoTB;

internal enum VariableType {
    //Byte,
    Short,
    Int,
    //Long,
    //Single,
    //Float,
    Double,
    //Bool,
    //Char,
    String,
    //Array,
    ShortArray,
    IntArray,
    DoubleArray,
    StringArray,
    //Class,
    //Struct,
    //Set,
    //Dict,
    //Object,
    //Variant,
    /*
    VTByte,
    VTShort,
    VTInt,
    VTLong,
    VTSingle,
    VTFloat,
    VTDouble,
    VTBool,
    VTChar,
    VTString,
    //VTArray,
    VTArrayOf,
    VTStruct,
    VTEnumOf,
    VTCodeRef,
    VTCode,
    VTSet,
    VTDict,
    VTObject,
    VTVariant,
    VTTuple,
     
     */

}
internal class Variable {
    internal string VName;
    internal int DimensionCount => VDimensions?.Count ?? 0;
    internal List<(int low, int high)>? VDimensions;
    internal object VValue;
    internal VariableType VType;
    //internal List<int>? VIndices = null;
    internal bool IsArray => VDimensions != null;

    internal static Dictionary<string, Variable> VariableStore = NewVariableStore();

    //internal static Variable Shared => shared.OValue;
    //private static readonly Lazy<Variable> shared = new(() => new Variable());


    internal Variable(string vName, short value, bool autoAddToStore) {
        VName = vName;
        VType = VariableType.Short;
        VDimensions = null;
        VValue = value;
        if (autoAddToStore) {
            VariableStore.TryAdd(vName, this);
        }
    }

    internal Variable(string vName, double value, bool autoAddToStore) {
        VName = vName;
        VType = VariableType.Double;
        VDimensions = null;
        VValue = value;
        if (autoAddToStore) {
            VariableStore.TryAdd(vName, this);
        }
    }

    internal Variable(string vName, string value, bool autoAddToStore) {
        VName = vName;
        VType = VariableType.String;
        VDimensions = null;
        VValue = value;
        if (autoAddToStore) {
            VariableStore.TryAdd(vName, this);
        }
    }

    internal Variable(string vName,
                      VariableType vType,
                      List<(int low, int high)> vDimensionRanges,
                      object? initVal,
                      bool autoCreate) {
        VName = vName;
        VType = VariableType.ShortArray;
        VDimensions = vDimensionRanges;
        switch (vType) {
            case VariableType.ShortArray:
                var totalElements = 1;
                foreach (var dRange in vDimensionRanges) {
                    totalElements *= dRange.high - dRange.low + 1;
                }
                var arrVal = new short[totalElements];
                var iVal = (int)(initVal ?? 0);

                for (var i = 0; i < arrVal.Length; i++) {
                    arrVal[i] = ((short)iVal); //initVal should be a boxed short, such as (object)(short)0
                }
                VValue = arrVal;
                break;
            default:
                //VValue = (short)0;
                //break;
                throw new RuntimeException("Unsupported array element type.");
        }
        if (autoCreate) {
            VariableStore.TryAdd(vName, this);
        }
    }

    public short? ShortValue {
        get => VType switch {
            VariableType.Short => (short?)VValue,
            //VariableType.String => null,
            //VariableType.ShortArray => null,
            _ => null
        };
        set => VValue = value ?? 0;
    }
    public Value? ElementValue(List<int> ArrayDimensionIndices) {

        switch (VType) {
            case VariableType.ShortArray:
                var shortRslt = ((short[])VValue)[ElementIndex(VDimensions!, ArrayDimensionIndices)];
                return new(shortRslt);
            case VariableType.Int:
                break;
            case VariableType.Double:
                break;
            case VariableType.String:
                break;
            default:
                throw new NotSupportedException();
        }

    }


    public void StoreElementValue(List<int> ArrayDimensionIndices, short value) {
        ((short[])VValue)[ElementIndex(VDimensions!, ArrayDimensionIndices)] = value;
    }

    private static int ElementIndex(List<(int low, int high)> dimensionRanges, List<int> elementIndexValues) {
        var nDims = dimensionRanges.Count;
        var rslt = elementIndexValues[0] - dimensionRanges[0].low;
        for (var i = 0; i < nDims - 1; i++) {
            rslt = rslt * (dimensionRanges[i].high - dimensionRanges[i].low + 1) + elementIndexValues[i] - dimensionRanges[i].low;
        }
        return rslt;
    }
    internal static Variable? FindVariable(string vName) {
        Variable? vVar;
        if (!VariableStore.TryGetValue(vName, out vVar)) {
            vVar = null;
        }
        return vVar;
    }

    private static Dictionary<string, Variable> NewVariableStore() {
        var rslt = new Dictionary<string, Variable>();
        rslt.TryAdd("TBVersion", new Variable("TBVersion", 0.001, false));
        rslt.TryAdd("Pi", new Variable("Pi", Math.PI, false));
        rslt.TryAdd("TBVersionString", new Variable("TBVersionString", "New Palo Alto Tiny Basic 0.001", false));
        return rslt;
    }

    internal static void ClearVariables() {
        VariableStore.Clear();
        VariableStore = NewVariableStore();
    }

}
