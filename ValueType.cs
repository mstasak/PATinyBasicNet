using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewPaloAltoTB;

/// <summary>
/// A value type.  May be associated with an expression or a variable.
/// </summary>
internal enum ValueType {
    None,
    //integers
    //Byte,
    //Int8, // 8-bit int
    //UByte,
    //UInt8, // unsigned version
    Short,
    //Int16, // 16-bit
    //UShort,
    //UInt16, Word,
    Int,
    //Int32, // 32-bit, AKA Int32
    //UInt,
    //UInt32,
    //DWord,
    //Int64,
    //Long, // 64-bit, AKA Long
    //UInt64,
    //ULong,
    //QWord,
    //Int128,
    //UInt128,

    ////floating points
    ////Float24, // 24 bit weak floating point? typically not useful (maybe in sound recording)
    //Float, // IEEE single precision (32 bits: sign x 1, mantissa x 23, exponent x 8)
    Double, // IEEE double precision (64 bits: sign x 1, mantissa x 52, exponent x 11)
    
    ////logical
    Bool,

    ////chars
    //Char,
    //CharUTF8,
    //WChar,
    //CharUTF16,
    ////CharUTF24,
    ////CharUTF32,

    ////strings
    String,
    //AnsiString,
    //UnicodeString,

    //Object,
    //Variant, //class or boxed native type
    //ObjectOf, //a specific class or boxed native type
    //List,
    //ListOf, //list of Object, list of specific type
    //Map,
    //MapOf,
    //Dict,
    //DictOf, 
    //Dictionary, 
    //DictionaryOf, //(synonyms: typeless or type specific)
    //Set, 
    //SetOf,
    //Stack, 
    //StackOf,
    //Queue, 
    //QueueOf,
    //BTree, 
    //BTreeOf,
    //Tree, 
    //TreeOf,
    //Array, 
    //ArrayOf,
    //Tuple, 
    //TupleOf,

    /* What about...
     * 
     * enum
     * struct
     * nullables
     * bitarray
     * media types (bmp/others, wav/others, icons, glyphs, svg, others?)
     * json?
     * expando-ish objects
     */
    //Collection,
    //CollectionOf,
    //Reference,
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
