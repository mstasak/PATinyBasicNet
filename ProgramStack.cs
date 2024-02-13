using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewPaloAltoTB;

/// <summary>
/// Implement a push-pop stack for properly nested program elements:
///   for-next forinit,next,limit-next? foreach-next?
///   gosub-return
///   begin-end?
///   do-while? repeat-until?
///   while-do? until-repeat?
///   ? items are likely future additions
///   for now, loops are primitive
///   - one start
///   - one end
///   - no break, no continue
///   - cannot next an outer loop in a nested subroutine
///   - return within a for loop ends the loop
/// </summary>
internal class ProgramStack {
    internal Stack<StackLevelInfo> TBStack;
    internal ProgramStack() {
        TBStack = new Stack<StackLevelInfo>();
    }

    internal static ProgramStack Shared => shared.Value;
    private static readonly Lazy<ProgramStack> shared = new(() => new ProgramStack());


}
