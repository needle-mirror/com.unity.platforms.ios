#if (UNITY_IOS)
using System;
using System.Runtime.InteropServices;

namespace Unity.Platforms
{
    public class RunLoopImpl
    {
        internal class MonoPInvokeCallbackAttribute : Attribute
        {
        }

        [MonoPInvokeCallbackAttribute]
        static bool ManagedRAFCallback()
        {
            return staticM();
        }

        private static RunLoop.RunLoopDelegate staticM;
        private static RunLoop.RunLoopDelegate staticManagedDelegate;

        public static void EnterMainLoop(RunLoop.RunLoopDelegate runLoopDelegate)
        {
            staticManagedDelegate = (RunLoop.RunLoopDelegate)ManagedRAFCallback;
            staticM = runLoopDelegate;
            iOSNativeCalls.set_animation_frame_callback(Marshal.GetFunctionPointerForDelegate(staticManagedDelegate));
        }
    }

    static class iOSNativeCalls
    {
        // calls to IOSWrapper.cpp
        [DllImport("__Internal", EntryPoint = "rafcallbackinit_ios")]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool set_animation_frame_callback(IntPtr func);
    }
}
#endif
