using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace NewPaloAltoTB;
internal class LValue {

    internal Variable LVar; //holds name, indices, valuetype, value
    //shortcut accessors to variable properties
    internal string VName => LVar.VName;
    internal int VDimensionCount => LVar.DimensionCount;
    internal List<(int low, int high)>? VDimensions => LVar.VDimensions;
    internal object VValue => LVar.VValue;
    internal VariableType VType => LVar.VType;
    internal bool IsArray => LVar.IsArray;

    internal List<int>? VIndices = null; //the Variable is the entire array (or a scalar); the LValue is a scalar or single array element
    

    internal LValue(Variable lVar, List<int>? lVarIndices) {
        if (lVar.IsArray && lVarIndices == null) {
            throw new RuntimeException("Internal error: attempt to construct LValue object for an entire array, without index value(s).");    
        }
        if (!lVar.IsArray && lVarIndices != null) {
            throw new RuntimeException("Internal error: attempt to reference indexed element of a scalar value.");    
        }
        if (lVar.DimensionCount != (lVarIndices?.Count ?? 0)) { 
            throw new RuntimeException($"Array element assignment error: Expected {lVar.DimensionCount} index values but found {lVarIndices!.Count}.");    
        }
        LVar = lVar;
        VIndices = lVarIndices;
    }

    internal int IntValue {
        get {
            if (IsArray) {

            } else {
            }
            switch (LVar.VType) {
                case VariableType.Int:
                    return LVar.IntValue ?? 0;
                case VariableType.IntArray:
                    return LVar.IntElementValue(LVarIndexValues!) ?? 0;
                default:
                    throw new RuntimeException($"Cannot fetch array element from variable {LVar.VName}");
            }
        }
        set {
            switch (LVar.VType) {
                case VariableType.Int:
                    LVar.IntValue = value;
                    break;
                case VariableType.IntArray:
                    //((short[])LVar.VValue)[6] = 0;
                    LVar.StoreElementValue(LVarIndexValues!, value);
                    break;
            }
        }
    }
    internal double DoubleValue {
        get {
            switch (LVar.VType) {
                case VariableType.Double:
                    return LVar.ShortValue ?? 0;
                case VariableType.DoubleArray:
                    return LVar.DoubleElementValue(LVarIndexValues!) ?? 0;
                default:
                    throw new RuntimeException($"Cannot fetch array element from variable {LVar.VName}");
            }
        }
        set {
            switch (LVar.VType) {
                case VariableType.Double:
                    LVar.DoubleValue = value;
                    break;
                case VariableType.DoubleArray:
                    //((short[])LVar.VValue)[6] = 0;
                    LVar.StoreElementValue(LVarIndexValues!, value);
                    break;
            }
        }
    }
    internal string StringValue {
        get {
            switch (LVar.VType) {
                case VariableType.String:
                    return LVar.StringValue ?? 0;
                case VariableType.StringArray:
                    return LVar.StringElementValue(LVarIndexValues!) ?? 0;
                default:
                    throw new RuntimeException($"Cannot fetch array element from variable {LVar.VName}");
            }
        }
        set {
            switch (LVar.VType) {
                case VariableType.String:
                    LVar.StringValue = value;
                    break;
                case VariableType.ShortArray:
                    //((short[])LVar.VValue)[6] = 0;
                    LVar.StoreElementValue(LVarIndexValues!, value);
                    break;
            }
        }
    }
    internal short ShortValue {
        get {
            switch (LVar.VType) {
                case VariableType.Short:
                    return LVar.ShortValue ?? 0;
                case VariableType.ShortArray:
                    return LVar.ShortElementValue(LVarIndexValues!) ?? 0;
                default:
                    throw new RuntimeException($"Cannot fetch array element from variable {LVar.VName}");
            }
        }
        set {
            switch (LVar.VType) {
                case VariableType.Short:
                    LVar.ShortValue = value;
                    break;
                case VariableType.ShortArray:
                    //((short[])LVar.VValue)[6] = 0;
                    LVar.StoreElementValue(LVarIndexValues!, value);
                    break;
            }
        }
    }
}