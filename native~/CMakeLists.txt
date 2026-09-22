cmake_minimum_required(VERSION 3.21)
project(ceffy_native LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# ---------- CEF SDK location ----------
# Set by setup.ps1 or pass via cmake -DCEF_ROOT=<path>
if(NOT CEF_ROOT)
    set(CEF_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/cef" CACHE PATH "CEF binary distribution root")
endif()

if(NOT EXISTS "${CEF_ROOT}/include/cef_app.h")
    message(FATAL_ERROR
        "CEF SDK not found at ${CEF_ROOT}.\n"
        "Run setup.ps1 first, or pass -DCEF_ROOT=<path> to cmake.")
endif()

add_definitions(-DUNICODE -D_UNICODE -DNOMINMAX)

# ---------- Build the CEF C++ wrapper (libcef_dll_wrapper) ----------
set(CEF_WRAPPER_DIR "${CEF_ROOT}/libcef_dll")
file(GLOB_RECURSE CEF_WRAPPER_SRCS
    "${CEF_WRAPPER_DIR}/*.cc"
    "${CEF_WRAPPER_DIR}/*.h"
)
add_library(libcef_dll_wrapper STATIC ${CEF_WRAPPER_SRCS})
target_include_directories(libcef_dll_wrapper PUBLIC "${CEF_ROOT}")
target_compile_definitions(libcef_dll_wrapper PUBLIC
    WRAPPING_CEF_SHARED
    NOMINMAX
)

# ---------- ceffy_native.dll (Unity native plugin) ----------
set(BRIDGE_SOURCES
    src/ceffy_bridge.cpp
    src/browser_manager.cpp
    src/cef_handlers.cpp
    src/texture_manager.cpp
    src/unity_plugin.cpp
)
set(BRIDGE_HEADERS
    src/ceffy_bridge.h
    src/browser_manager.h
    src/cef_handlers.h
    src/texture_manager.h
    src/callback_queue.h
)

add_library(ceffy_native SHARED ${BRIDGE_SOURCES} ${BRIDGE_HEADERS})
target_compile_definitions(ceffy_native PRIVATE Ceffy_NATIVE_EXPORTS)
target_include_directories(ceffy_native PRIVATE "${CEF_ROOT}" src)
target_link_libraries(ceffy_native PRIVATE
    libcef_dll_wrapper
    "${CEF_ROOT}/Release/libcef.lib"
    d3d11.lib
    dxgi.lib
)

# ---------- CeffyHelper.ceffy (CEF sub-process) ----------
add_executable(CeffyHelper WIN32
    helper/helper_main.cpp
    helper/helper_app.cpp
    helper/helper_app.h
)
set_target_properties(CeffyHelper PROPERTIES SUFFIX ".ceffy")
target_include_directories(CeffyHelper PRIVATE "${CEF_ROOT}" helper)
target_link_libraries(CeffyHelper PRIVATE
    libcef_dll_wrapper
    "${CEF_ROOT}/Release/libcef.lib"
)

# ---------- Post-build: copy CEF runtime files next to outputs ----------
set(CEF_RUNTIME_FILES
    libcef.dll
    chrome_elf.dll
    d3dcompiler_47.dll
    dxcompiler.dll
    dxil.dll
    libEGL.dll
    libGLESv2.dll
    v8_context_snapshot.bin
    vk_swiftshader.dll
    vk_swiftshader_icd.json
    vulkan-1.dll
)

set(CEF_RESOURCE_FILES
    icudtl.dat
    chrome_100_percent.pak
    chrome_200_percent.pak
    resources.pak
)

foreach(_file ${CEF_RUNTIME_FILES})
    if(EXISTS "${CEF_ROOT}/Release/${_file}")
        add_custom_command(TARGET ceffy_native POST_BUILD
            COMMAND ${CMAKE_COMMAND} -E copy_if_different
                "${CEF_ROOT}/Release/${_file}"
                "$<TARGET_FILE_DIR:ceffy_native>/${_file}")
    endif()
endforeach()

foreach(_file ${CEF_RESOURCE_FILES})
    if(EXISTS "${CEF_ROOT}/Resources/${_file}")
        add_custom_command(TARGET ceffy_native POST_BUILD
            COMMAND ${CMAKE_COMMAND} -E copy_if_different
                "${CEF_ROOT}/Resources/${_file}"
                "$<TARGET_FILE_DIR:ceffy_native>/${_file}")
    endif()
endforeach()

# Copy only english locale
set(CEF_LOCALE "en-US.pak")

if(EXISTS "${CEF_ROOT}/Resources/locales/${CEF_LOCALE}")
    add_custom_command(TARGET ceffy_native POST_BUILD
            COMMAND ${CMAKE_COMMAND} -E make_directory
            "$<TARGET_FILE_DIR:ceffy_native>/locales"
            COMMAND ${CMAKE_COMMAND} -E copy_if_different
            "${CEF_ROOT}/Resources/locales/${CEF_LOCALE}"
            "$<TARGET_FILE_DIR:ceffy_native>/locales/${CEF_LOCALE}")
endif()
