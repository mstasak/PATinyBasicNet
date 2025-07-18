using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Schema;

namespace NewPaloAltoTB;

/// <summary>
/// Contains a value (result of an expression).  This includes the type, but no
/// modifiers like public or readonly/const.
/// </summary>
internal class Value {
    internal ValueType VType = ValueType.None;
    internal object? OValue;
    //private bool v;

    private static readonly Value _nullValue = new();
    internal static Value NullValue => _nullValue;
    internal Value(bool v) {
        VType = ValueType.Bool;
        OValue = v;
    }

    internal Value(short v) {
        VType = ValueType.Short;
        OValue = v;
    }

    internal Value(int v) {
        VType = ValueType.Int;
        OValue = v;
    }

    internal Value(double v) {
        VType = ValueType.Double;
        OValue = v;
    }

    internal Value(string v) {
        VType = ValueType.String;
        OValue = v;
    }

    internal Value() {
        VType = ValueType.None;
        OValue = null;
    }

    public Value CastAsBool() {
        switch (VType) {
            case ValueType.Bool: return this;
            case ValueType.None: return new(false);
            case ValueType.Short: return new(((short?)OValue ?? 0) != 0);
            case ValueType.Int: return new((((int?)OValue) ?? 0) != 0);
            case ValueType.Double: return new((((double?)OValue) ?? 0.0) != 0.0);
            case ValueType.String: //we are being pretty permissive here!  Should probably make a formal converter...
                var tmpStr = (((string?)OValue) ?? "0").ToUpper();
                if (tmpStr.StartsWith("TRUE")) return new(true);
                if (tmpStr.StartsWith("T")) return new(true);
                if (tmpStr.StartsWith("YES")) return new(true);
                if (tmpStr.StartsWith("Y")) return new(true);
                if (tmpStr.StartsWith("1")) return new(true);
                if (tmpStr.StartsWith("FALSE")) return new(false);
                if (tmpStr.StartsWith("F")) return new(false);
                if (tmpStr.StartsWith("NO")) return new(false);
                if (tmpStr.StartsWith("N")) return new(false);
                if (tmpStr.StartsWith("0")) return new(false);
                throw new ArgumentException($"Cannot cast \"{(string?)OValue}\" as a boolean value.");
            default:
                throw new ArgumentException(); //Cast from unknown type - would mean coding not complete
        }
    }
    public Value CastAsShort() {
        switch (VType) {
            case ValueType.Bool: return new((short)((bool)(OValue ?? false) ? 1 : 0));
            case ValueType.None: return new((short)0);
            case ValueType.Short: return this;
            case ValueType.Int:
                var tmpInt = ((int?)OValue) ?? 0;
                if ((tmpInt < short.MinValue) || (tmpInt > short.MaxValue)) throw new ArgumentOutOfRangeException();
                return new((short)tmpInt);
            case ValueType.Double:
                var tmpDbl = ((double?)OValue) ?? 0.0;
                if ((tmpDbl < short.MinValue) || (tmpDbl > short.MaxValue)) throw new ArgumentOutOfRangeException();
                return new((short)tmpDbl);
            case ValueType.String:
                var tmpStr = ((string?)OValue) ?? "0";
                short tmpShortParsed;
                if (short.TryParse(tmpStr, out tmpShortParsed)) {
                    return new(tmpShortParsed);    
                }
                throw new ArgumentException(); //TryParse() failed - not a valid number, out of range, or followed by garbage (non-ws) text
            default:
                throw new ArgumentException(); //Cast from unknown type - would mean coding not complete
        }
    }

    public Value CastAsInt() {
        switch (VType) {
            case ValueType.Bool: return new((int)((bool)(OValue ?? false) ? 1 : 0));
            case ValueType.None: return new((int)0);
            case ValueType.Short: return new Value((int)((short?)OValue ?? 0));
            case ValueType.Int: return this;
            case ValueType.Double:
                var tmpDbl = ((double?)OValue) ?? 0.0;
                if ((tmpDbl < int.MinValue) || (tmpDbl > int.MaxValue)) throw new ArgumentOutOfRangeException();
                return new((int)tmpDbl);
            case ValueType.String:
                var tmpStr = ((string?)OValue) ?? "0"; //odd, why is tmpStr a nullable string when (evenANullValue ?? "0") cannot evaluate to null?
                int tmpIntParsed;
                if (int.TryParse(tmpStr, out tmpIntParsed)) {
                    return new(tmpIntParsed);    
                }
                throw new ArgumentException(); //TryParse() failed - not a valid number, out of range, or followed by garbage (non-ws) text
            default:
                throw new ArgumentException(); //Cast from unknown type - would mean coding not complete
        }
    }

    public Value CastAsDouble() {
        switch (VType) {
            case ValueType.Bool: return new((double)((((bool?)OValue) ?? false) ? 1.0 : 0.0));
            case ValueType.None: return new((short)0);
            case ValueType.Short: return new( (double)(((short?)OValue) ?? 0));
            case ValueType.Int: return new( (double)(((int?)OValue) ?? 0));
            case ValueType.Double: return this;
            case ValueType.String:
                var tmpStr = ((string?)OValue) ?? "0";
                double tmpParsed;
                if (double.TryParse(tmpStr, out tmpParsed)) {
                    return new(tmpParsed);    
                }
                throw new ArgumentException($"Cannot convert {(string?)OValue} to a floating point number."); //TryParse() failed - not a valid number, out of range, or followed by garbage (non-ws) text
            default:
                throw new ArgumentException(); //Cast from unknown type - would mean coding not complete
        }
    }

    public Value CastAsString() {
        switch (VType) {
            case ValueType.Bool: return new((((bool?)OValue) ?? false) ? "True" : "False");
            case ValueType.None: return new("Nil");
            case ValueType.Short: return new($"{OValue}");
            case ValueType.Int: return new($"{OValue}");
            case ValueType.Double: return new($"{OValue}");
            case ValueType.String: return this;
            default:
                throw new ArgumentException(); //Cast from unknown type - would mean coding not complete
        }
    }

    internal Value EqualTo(Value b) {
        if (NormalizeTypes(b, OperationType.Compare)) {
            switch (VType) {
                case ValueType.Int: return new Value((int)OValue == (int)b.OValue);
                case ValueType.String: return new Value((string)OValue == (string)b.OValue);
                case ValueType.Bool: return new Value((bool)OValue == (bool)b.OValue);
                default:
                    break;
            }
        }

        throw new NotImplementedException();
        //many cases to handle...
    }

    /// <summary>
    /// Coerce operands to matching or compatible types
    /// (this could be long and messy)
    /// Generally, coerce both operands to the wider/higher precision type.
    /// </summary>
    /// <param name="a">'this' is the first operand</param>
    /// <param name="b">b is the second operand</param>
    /// <param name="operType">operType describes the required operation: addition, multiplication, etc.</param>
    /// <returns>true if successful</returns>
    /// <exception cref="NotImplementedException"></exception>
    private bool NormalizeTypes(Value b, OperationType operType) {
        Value a = this;
        switch (operType) {
            case OperationType.None:
                break;
            case OperationType.Compare:
                if (a.VType == ValueType.Int && a.VType == b.VType) return true;
                break;
            case OperationType.AddSubtract:
                if (a.VType == ValueType.Int && a.VType == b.VType) return true;
                break;
            case OperationType.MultiplyDivide:
                if (a.VType == ValueType.Int && a.VType == b.VType) return true;
                break;
            case OperationType.BooleanLogical:
                if (a.VType == ValueType.Bool && a.VType == b.VType) return true;
                break;
            case OperationType.BooleanBitwise:
                break;
            default:
                break;
        }
        throw new NotImplementedException();
    }

    internal Value LessThanOrEqualTo(Value b) {
        throw new NotImplementedException();
        //many cases to handle...
    }
    internal Value LessThan(Value b) {
        throw new NotImplementedException();
        //many cases to handle...
    }
    internal Value GreaterThanOrEqualTo(Value b) {
        throw new NotImplementedException();
        //many cases to handle...
    }
    internal Value GreaterThan(Value b) {
        throw new NotImplementedException();
        //many cases to handle...
    }
    internal Value NotEqualTo(Value b) {
        throw new NotImplementedException();
        //many cases to handle...
    }
    internal Value Add(Value b) {
        throw new NotImplementedException();
        //many cases to handle...
    }
    internal Value Subtract(Value b) {
        throw new NotImplementedException();
        //many cases to handle...
    }
    internal Value Multiply(Value b) {
        throw new NotImplementedException();
        //many cases to handle...
    }
    internal Value Divide(Value b) {
        throw new NotImplementedException();
        //many cases to handle...
    }
    internal Value Modulo(Value b) {
        throw new NotImplementedException();
        //many cases to handle...
    }
    internal Value Exponential(Value b) {
        throw new NotImplementedException();
        //many cases to handle...
    }
    internal Value LogicalAnd(Value b) {
        throw new NotImplementedException();
        //many cases to handle...
    }
    internal Value LogicalOr(Value b) {
        throw new NotImplementedException();
        //many cases to handle...
    }
    internal Value LogicalNot() {
        throw new NotImplementedException();
        //many cases to handle...
    }
    internal Value LogicalXor(Value b) {
        throw new NotImplementedException();
        //many cases to handle...
    }
    internal Value BitwiseAnd(Value b) {
        throw new NotImplementedException();
        //many cases to handle...
    }
    internal Value BitwiseOr(Value b) {
        throw new NotImplementedException();
        //many cases to handle...
    }
    internal Value BitwiseNot() {
        throw new NotImplementedException();
        //many cases to handle...
    }
    internal Value BitwiseXor(Value b) {
        throw new NotImplementedException();
        //many cases to handle...
    }

    internal Value NegativeValue() {
        throw new NotImplementedException();
    }

    internal bool IsZero() {
        switch (VType) {
            case ValueType.Bool:
            case ValueType.String:
                break; //should never be called for non-XmlSchemaNumericFacet types
            case ValueType.Int:
                return (int)OValue == 0;
            case ValueType.Double:
                return (double)OValue == 0.0;
            default:
                break;
        }
        throw new NotImplementedException();
    }
}
