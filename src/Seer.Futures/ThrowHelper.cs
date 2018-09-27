using System;
using System.Runtime.CompilerServices;

namespace Seer.Futures
{
    internal class ThrowHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void NotNull(object o, string paramName)
        {
            if (o == null)
                ThrowArgumentNull(paramName);
        }

        public static void ThrowArgumentNull(string paramName)
        {
            throw new ArgumentNullException(paramName);
        }
    }
}