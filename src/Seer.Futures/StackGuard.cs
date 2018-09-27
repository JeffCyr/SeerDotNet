using System;
using System.Diagnostics;

namespace Seer.Futures
{
    // Copied from coreclr Task.cs

    /// <summary>
    /// Internal helper class to keep track of stack depth and decide whether we should inline or not.
    /// </summary>
    internal class StackGuard
    {
        // current thread's depth of nested inline task executions
        private int m_inliningDepth = 0;

        // For relatively small inlining depths we don't want to get into the business of stack probing etc.
        // This clearly leaves a window of opportunity for the user code to SO. However a piece of code
        // that can SO in 20 inlines on a typical 1MB stack size probably needs to be revisited anyway.
        private const int MAX_UNCHECKED_INLINING_DEPTH = 20;

        [ThreadStatic]
        private static StackGuard t_stackGuard;

        /// <summary>
        /// Gets the StackGuard object assigned to the current thread.
        /// </summary>
        internal static StackGuard Current
        {
            get
            {
                StackGuard sg = t_stackGuard;
                if (sg == null)
                {
                    t_stackGuard = sg = new StackGuard();
                }
                return sg;
            }
        }

        /// <summary>
        /// This method needs to be called before attempting inline execution on the current thread. 
        /// If false is returned, it means we are too close to the end of the stack and should give up inlining.
        /// Each call to TryBeginInliningScope() that returns true must be matched with a 
        /// call to EndInliningScope() regardless of whether inlining actually took place.
        /// </summary>
        internal bool TryBeginInliningScope()
        {
            // If we're still under the 'safe' limit we'll just skip the stack probe to save p/invoke calls
            if (m_inliningDepth < MAX_UNCHECKED_INLINING_DEPTH || CheckForSufficientStack())
            {
                m_inliningDepth++;
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// This needs to be called once for each previous successful TryBeginInliningScope() call after
        /// inlining related logic runs.
        /// </summary>
        internal void EndInliningScope()
        {
            m_inliningDepth--;
            Debug.Assert(m_inliningDepth >= 0, "Inlining depth count should never go negative.");

            // do the right thing just in case...
            if (m_inliningDepth < 0) m_inliningDepth = 0;
        }

        private bool CheckForSufficientStack()
        {
#if NETCOREAPP2_1
            return System.Runtime.CompilerServices.RuntimeHelpers.TryEnsureSufficientExecutionStack();
#else
            return false;
#endif
        }
    }
}