using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewPaloAltoTB;

//This is scopeless (all global) variable store.  Obviously we will prefer something much more sophisticated.
internal class VariableStore {

    internal static VariableStore Shared => shared.Value;
    private static readonly Lazy<VariableStore> shared = new(() => new VariableStore());

    internal Dictionary<string, Variable> Globals = new(StringComparer.InvariantCulture) {
        { "Version", new Variable("Version", "0.01") }
    };

    internal Variable? TryGetVariable(string name, bool createIfMissing = true) {
        Variable? rslt;
        if (Globals.TryGetValue(name, out rslt)) {
            //found it
        } else {
            //missing; create it with a value of zero if it is a single character name
            if (createIfMissing) {
                Globals.Add(name, rslt = new(name, (short)0, VariableType.Short));
            } else {
                rslt = null;
            }
        }
        return rslt;
    }

    internal void StoreVariable(string name, short value) {
        //future: handle strings, doubles, dates, arrays, etc.  Probably using some universal value type.
        Globals[name] = new Variable(name, value);
    }
}
