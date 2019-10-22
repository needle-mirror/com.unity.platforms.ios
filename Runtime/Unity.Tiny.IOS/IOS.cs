using System;
using System.Diagnostics;
using Unity.Entities;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Tiny.IOS
{
    public class IOSWindowSystem : WindowSystem
    {
        private static IOSWindowSystem sWindowSystem;
        public IOSWindowSystem()
        {
            initialized = false;
            sWindowSystem = this;
        }

        public override IntPtr GetPlatformWindowHandle()
        {
            unsafe {
                return (IntPtr)IOSNativeCalls.getNativeWindow();
            }
        }

        /*TODO how we can inform RunLoop about system events pause/resume/destroy?
        private static Action<int> onPauseM;

        [MonoPInvokeCallbackAttribute]
        static void ManagedOnPauseCallback(int pause)
        {
            onPauseM(pause);
        }

        private static Action onDestroyM;

        [MonoPInvokeCallbackAttribute]
        static void ManagedOnDestroyCallback()
        {
            onDestroyM();
        }

        internal class MonoPInvokeCallbackAttribute : Attribute
        {
        }

        public override void InfiniteMainLoop(MainLoopDelegate m)
        {
            staticM = m;
            IOSNativeCalls.set_animation_frame_callback(Marshal.GetFunctionPointerForDelegate((MainLoopDelegate)ManagedRAFCallback));
        }

        public void SetOnPauseCallback(Action<int> m)
        {
            onPauseM = m;
            IOSNativeCalls.set_pause_callback(Marshal.GetFunctionPointerForDelegate((Action<int>)ManagedOnPauseCallback));
        }

        public void SetOnDestroyCallback(Action m)
        {
            onDestroyM = m;
            IOSNativeCalls.set_destroy_callback(Marshal.GetFunctionPointerForDelegate((Action)ManagedOnDestroyCallback));
        }
        */

        public override void DebugReadbackImage(out int w, out int h, out NativeArray<byte> pixels)
        {
            var env = World.GetExistingSystem<TinyEnvironment>();
            var config = env.GetConfigData<DisplayInfo>();
            pixels = new NativeArray<byte>(config.framebufferWidth*config.framebufferHeight*4, Allocator.Persistent);
            unsafe
            {
                IOSNativeCalls.debugReadback(config.framebufferWidth, config.framebufferHeight, pixels.GetUnsafePtr());
            }

            w = config.framebufferWidth;
            h = config.framebufferHeight;
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();

            // setup window
            Console.WriteLine("IOS Window init.");

            var env = World.GetExistingSystem<TinyEnvironment>();
            var config = env.GetConfigData<DisplayInfo>();

            try
            {
                initialized = IOSNativeCalls.init();
            } catch
            {
                Console.WriteLine("  Exception during initialization.");
                initialized = false;
            }
            if (!initialized)
            {
                Console.WriteLine("  Failed.");
                World.QuitUpdate = true;
                return;
            }
            int winw = 0, winh = 0;
            IOSNativeCalls.getWindowSize(ref winw, ref winh);
            config.focused = true;
            config.visible = true;
            config.orientation = winw >= winh ? ScreenOrientation.Horizontal : ScreenOrientation.Vertical;
            config.frameWidth = winw;
            config.frameHeight = winh;
            int sw = 0, sh = 0;
            IOSNativeCalls.getScreenSize(ref sw, ref sh);
            config.screenWidth = sw;
            config.screenHeight = sh;
            config.width = winw;
            config.height = winh;
            int fbw = 0, fbh = 0;
            IOSNativeCalls.getFramebufferSize(ref fbw, ref fbh);
            config.framebufferWidth = fbw;
            config.framebufferHeight = fbh;
            env.SetConfigData(config);

            frameTime = IOSNativeCalls.time();
        }

        protected override void OnDestroy()
        {
            // close window
            if (initialized)
            {
                Console.WriteLine("iOS Window shutdown.");
                IOSNativeCalls.shutdown(0);
                initialized = false;
            }
        }

        protected override void OnUpdate()
        {
            if (!initialized)
                return;

            var env = World.GetExistingSystem<TinyEnvironment>();
            var config = env.GetConfigData<DisplayInfo>();
            int winw = 0, winh = 0;
            IOSNativeCalls.getWindowSize(ref winw, ref winh);
            if (winw != config.width || winh != config.height)
            {
                if (config.autoSizeToFrame)
                {
                    Console.WriteLine("IOS Window update size.");
                    config.orientation = winw >= winh ? ScreenOrientation.Landscape : ScreenOrientation.Portrait;
                    config.width = winw;
                    config.height = winh;
                    config.frameWidth = winw;
                    config.frameHeight = winh;
                    int fbw = 0, fbh = 0;
                    IOSNativeCalls.getFramebufferSize(ref fbw, ref fbh);
                    config.framebufferWidth = fbw;
                    config.framebufferHeight = fbh;
                    env.SetConfigData(config);
                }
                else
                {
                    IOSNativeCalls.resize(config.width, config.height);
                }
            }
            if (!IOSNativeCalls.messagePump())
            {
                Console.WriteLine("iOS message pump exit.");
                IOSNativeCalls.shutdown(1);
                World.QuitUpdate = true;
                initialized = false;
                return;
            }
#if DEBUG
            IOSNativeCalls.debugClear();
#endif
            double newFrameTime = IOSNativeCalls.time();
            var timeData = env.StepWallRealtimeFrame(newFrameTime - frameTime);
            World.SetTime(timeData);
            frameTime = newFrameTime;
        }

        private bool initialized;
        private double frameTime;
    }

    public static class IOSNativeCalls
    {
        [DllImport("lib_unity_tiny_ios", EntryPoint = "init_ios")]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool init();

        [DllImport("lib_unity_tiny_ios", EntryPoint = "getWindowSize_ios")]
        public static extern void getWindowSize(ref int w, ref int h);

        [DllImport("lib_unity_tiny_ios", EntryPoint = "getScreenSize_ios")]
        public static extern void getScreenSize(ref int w, ref int h);

        [DllImport("lib_unity_tiny_ios", EntryPoint = "getFramebufferSize_ios")]
        public static extern void getFramebufferSize(ref int w, ref int h);

        [DllImport("lib_unity_tiny_ios", EntryPoint = "getWindowFrameSize_ios")]
        public static extern void getWindowFrameSize(ref int left, ref int top, ref int right, ref int bottom);

        [DllImport("lib_unity_tiny_ios", EntryPoint = "shutdown_ios")]
        public static extern void shutdown(int exitCode);

        [DllImport("lib_unity_tiny_ios", EntryPoint = "resize_ios")]
        public static extern void resize(int width, int height);

        [DllImport("lib_unity_tiny_ios", EntryPoint = "messagePump_ios")]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool messagePump();

        [DllImport("lib_unity_tiny_ios", EntryPoint = "swapBuffers_ios")]
        public static extern void swapBuffers();

        [DllImport("lib_unity_tiny_ios", EntryPoint = "debugClear_ios")]
        public static extern void debugClear();

        [DllImport("lib_unity_tiny_ios", EntryPoint = "debugReadback_ios")]
        public static unsafe extern void debugReadback(int w, int h, void *pixels);

        [DllImport("lib_unity_tiny_ios", EntryPoint = "time_ios")]
        public static extern double time();

        [DllImport("lib_unity_tiny_ios", EntryPoint = "pausecallbacksinit_ios")]
        public static extern bool set_pause_callback(IntPtr func);

        [DllImport("lib_unity_tiny_ios", EntryPoint = "destroycallbacksinit_ios")]
        public static extern bool set_destroy_callback(IntPtr func);

        [DllImport("lib_unity_tiny_ios", EntryPoint = "get_touch_info_stream_ios")]
        public static extern unsafe int * getTouchInfoStream(ref int len);

        [DllImport("lib_unity_tiny_ios", EntryPoint = "get_native_window_ios")]
        public static extern unsafe void * getNativeWindow();

        [DllImport("lib_unity_tiny_ios", EntryPoint = "reset_ios_input")]
        public static extern void resetStreams();
    }

}

