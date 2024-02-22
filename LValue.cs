using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewPaloAltoTB;
internal class LValue {
    internal Variable LVar;
    internal List<int>? LVarIndexValues;

    internal LValue(Variable lVar, List<int>? lVarIndexValues) {
        LVar = lVar;
        LVarIndexValues = lVarIndexValues;
        if (LVar.VType == VariableType.ShortArray && LVarIndexValues == null) {
            throw new RuntimeException("Internal error: attempt to construct LValue object to an array without index value(s).");    
        }
        if (lVar.VDimensions != (lVarIndexValues?.Count ?? 0)) { 
            throw new RuntimeException($"Array element assignment error: Expected {lVar.VDimensions} index values but found {lVarIndexValues!.Count}.");    
        }
    }

    internal short Value {
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