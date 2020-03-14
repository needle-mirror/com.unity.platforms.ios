#include <Unity/Runtime.h>

#include <dlfcn.h>
#include <unistd.h>
#include <stdio.h>
#include <math.h>
#include <time.h>
#include <vector>
#include "IOSSensors.h"

static bool shouldClose = false;
static int windowW = 0;
static int windowH = 0;
static int deviceOrientation;
static int screenOrientation;
static void* nativeWindow = NULL;
// input
static std::vector<int> touch_info_stream;
// c# delegates
static bool (*raf)() = 0;
static void (*pausef)(int) = 0;
static void (*destroyf)() = 0;
static void (*device_orientationf)(int) = 0;

void setOrientationMask(int orientationMask);
void rotateToDeviceOrientation();
void rotateToAllowedOrientation();
void rotateToOrientation(int orientation);

DOTS_EXPORT(bool)
init_ios() {
    printf("IOSWrapper: iOS C Init\n");
    return true;
}

DOTS_EXPORT(void)
getWindowSize_ios(int *width, int *height) {
    *width = windowW;
    *height = windowH;
}

DOTS_EXPORT(void)
getScreenSize_ios(int *width, int *height) {
    *width = windowW;
    *height = windowH;
}

DOTS_EXPORT(void)
getFramebufferSize_ios(int *width, int *height) {
    *width = windowW;
    *height = windowH;
}

DOTS_EXPORT(void)
getWindowFrameSize_ios(int *left, int *top, int *right, int *bottom) {
    *left = *top = 0;
    *right = windowW;
    *bottom = windowH;
}

DOTS_EXPORT(void)
getScreenOrientation_ios(int *orientation) {
    *orientation = screenOrientation;
}

DOTS_EXPORT(void)
shutdown_ios(int exitCode) {
    // BS call something to kill app
    raf = 0;
}

DOTS_EXPORT(void)
resize_ios(int width, int height) {
    //glfwSetWindowSize(mainWindow, width, height);
    windowW = width;
    windowH = height;
}

DOTS_EXPORT(bool)
messagePump_ios() {
    /*if (!mainWindow || shouldClose)
        return false;
    glfwMakeContextCurrent(mainWindow);
    glfwPollEvents();*/
    return !shouldClose;
}

DOTS_EXPORT(double)
time_ios() {
    static double start_time = -1;
    struct timespec res;
    clock_gettime(CLOCK_REALTIME, &res);
    double t = res.tv_sec + (double) res.tv_nsec / 1e9;
    if (start_time < 0) {
        start_time = t;
    }
    return t - start_time;
}

DOTS_EXPORT(bool)
rafcallbackinit_ios(bool (*func)()) {
    if (raf)
        return false;
    raf = func;
    return true;
}

DOTS_EXPORT(bool)
pausecallbackinit_ios(void (*func)(int)) {
    if (pausef)
        return false;
    pausef = func;
    return true;
}

DOTS_EXPORT(bool)
destroycallbackinit_ios(void (*func)()) {
    if (destroyf)
        return false;
    destroyf = func;
    return true;
}

DOTS_EXPORT(bool)
device_orientationcallbackinit_ios(void (*func)(int)) {
    if (device_orientationf)
        return false;
    device_orientationf = func;
    if (device_orientationf)
        device_orientationf(deviceOrientation);
    return true;
}

DOTS_EXPORT(const int*)
get_touch_info_stream_ios(int *len) {
    *len = (int)touch_info_stream.size();
    return touch_info_stream.data();
}

DOTS_EXPORT(void*)
get_native_window_ios() {
    return nativeWindow ;
}

DOTS_EXPORT(void)
reset_ios_input()
{
    touch_info_stream.clear();
    m_iOSSensors.ResetSensorsData();
}

DOTS_EXPORT(bool)
available_sensor_ios(int type)
{
    return m_iOSSensors.AvailableSensor((iOSSensorType)type);
}

DOTS_EXPORT(bool)
enable_sensor_ios(int type, bool enable)
{
    return m_iOSSensors.EnableSensor((iOSSensorType)type, enable);
}

DOTS_EXPORT(void)
set_sensor_frequency_ios(int type, int rate)
{
    m_iOSSensors.SetSamplingFrequency((iOSSensorType)type, rate);
}

DOTS_EXPORT(int)
get_sensor_frequency_ios(int type)
{
    return m_iOSSensors.GetSamplingFrequency((iOSSensorType)type);
}

DOTS_EXPORT(const double*)
get_sensor_stream_ios(int type, int *len)
{
    if (len == NULL)
        return NULL;
    return m_iOSSensors.GetSensorData((iOSSensorType)type, len);
}

DOTS_EXPORT(void)
setOrientationMask_ios(int orientationMask)
{
    setOrientationMask(orientationMask);
}

DOTS_EXPORT(void)
rotateToDeviceOrientation_ios()
{
    rotateToDeviceOrientation();
}

DOTS_EXPORT(void)
rotateToAllowedOrientation_ios()
{
    rotateToAllowedOrientation();
}

extern "C" void start();
DOTS_EXPORT(void)
startapp()
{
    m_iOSSensors.InitializeSensors();
    start();
}

DOTS_EXPORT(void)
init(void *nwh, int width, int height, int orientation)
{
    printf("init %d x %d\n", width, height);
    windowW = width;
    windowH = height;
    screenOrientation = orientation;
    nativeWindow = nwh;
}

DOTS_EXPORT(void)
step()
{
    if (raf && !raf())
        shutdown_ios(2);
}

DOTS_EXPORT(void)
pauseapp(int paused)
{
    if (pausef)
        pausef(paused);
}

DOTS_EXPORT(void)
destroyapp()
{
    m_iOSSensors.ShutdownSensors();
    if (destroyf)
        destroyf();
}

DOTS_EXPORT(void)
touchevent(int id, int action, int xpos, int ypos)
{
    touch_info_stream.push_back((int)id);
    touch_info_stream.push_back((int)action);
    touch_info_stream.push_back((int)xpos);
    touch_info_stream.push_back(windowH - 1 - (int)ypos);
}

DOTS_EXPORT(void)
deviceOrientationChanged(int orientation)
{
    deviceOrientation = orientation;
    if (device_orientationf)
        device_orientationf(orientation);
}

#if UNITY_DOTSPLAYER_IL2CPP_WAIT_FOR_MANAGED_DEBUGGER

typedef void(*BroadcastFunction)();
void ShowDebuggerAttachDialogImpl(const char* message, BroadcastFunction broadcast);

bool waitForManagedDebugger = true;

DOTS_EXPORT(void)
ShowDebuggerAttachDialog(const char* message, BroadcastFunction broadcast)
{
    ShowDebuggerAttachDialogImpl(message, broadcast);
}
#else

bool waitForManagedDebugger = false;

#endif // UNITY_DOTSPLAYER_IL2CPP_WAIT_FOR_MANAGED_DEBUGGER
