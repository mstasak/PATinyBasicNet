using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    //String,
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
    internal int VDimensions;
    internal List<(int low, int high)>? VDimensionRanges;
    internal object VValue;
    internal VariableType VType;

    internal static Dictionary<string, Variable> VariableStore = new();

    //internal static Variable Shared => shared.Value;
    //private static readonly Lazy<Variable> shared = new(() => new Variable());


    internal Variable(string vName, short value, bool autoCreate) {
        VName = vName;
        VType = VariableType.Short;
        VDimensions = 0;
        VDimensionRanges = null;
        VValue = value;
        if (autoCreate) {
            VariableStore.TryAdd(vName, this);
        }
    }

    //internal Variable(string vName, string vValue) {
    //    VName = vName;
    //    VType = VariableType.String;
    //    VDimensions = 0;
    //    VDimensionRanges = null;
    //    VValue = vValue;
    //}

    internal Variable(string vName,
                      VariableType vType,
                      List<(int low, int high)> vDimensionRanges,
                      object? initVal,
                      bool autoCreate) {
        VName = vName;
        VType = VariableType.ShortArray;
        VDimensions = vDimensionRanges.Count;
        VDimensionRanges = vDimensionRanges;
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
        set {
            VValue = value ?? 0;
        }
    }
    public short? ShortElementValue(List<int> ArrayDimensionIndices) => VType switch {
        VariableType.ShortArray => ((short[])VValue)[ElementIndex(VDimensionRanges!, ArrayDimensionIndices)],
        _ => null
    };


    public void StoreElementValue(List<int> ArrayDimensionIndices, short value) {
        ((short[])VValue)[ElementIndex(VDimensionRanges!, ArrayDimensionIndices)] = value;
    }

    private static int ElementIndex(List<(int low, int high)> dimensionRanges, List<int> elementIndexValues) {
        var nDims = dimensionRanges.Count;
        var rslt = elementIndexValues[0] - dimensionRanges[0].low;
        for (var i = 0; i < nDims-1; i++) {
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
}
