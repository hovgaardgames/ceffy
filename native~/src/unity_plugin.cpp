#include <cstdio>

struct IUnityInterfaces;

extern "C" {

__declspec(dllexport) void UnityPluginLoad(IUnityInterfaces* unityInterfaces)
{
    (void)unityInterfaces;
    printf(">> ceffy_native: UnityPluginLoad\n");
}

__declspec(dllexport) void UnityPluginUnload()
{
    printf(">> ceffy_native: UnityPluginUnload\n");
}

} // extern "C"
