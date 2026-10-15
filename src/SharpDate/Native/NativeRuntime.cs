// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Runtime;
using System.Runtime.CompilerServices;

namespace Playdate.Native
{
    /// <summary>
    /// Helpers implemented in the SharpDate native runtime (src/SharpDate.Runtime/sdrt.c).
    /// Variadic Playdate functions (logToConsole, error, ...) cannot be called portably from
    /// managed code because some ABIs pass variadic arguments differently, so they go through C.
    /// </summary>
    public static unsafe class Variadic
    {
        /// <summary>Calls <c>pd->system->logToConsole("%s", message)</c>.</summary>
        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport("*", "SharpDate_LogToConsole")]
        public static extern void LogToConsole(byte* utf8Message);

        /// <summary>Calls <c>pd->system->error("%s", message)</c>.</summary>
        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport("*", "SharpDate_Error")]
        public static extern void Error(byte* utf8Message);
    }
}
