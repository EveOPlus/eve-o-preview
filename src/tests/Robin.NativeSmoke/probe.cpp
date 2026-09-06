#include <windows.h>
#include <d3d11.h>
#include <dxgi1_2.h>
#include <wrl/client.h>
#include <chrono>
#include <iostream>
#include <stdexcept>
#include <string>
#include <thread>
#include <vector>
using Microsoft::WRL::ComPtr;
using Clock = std::chrono::steady_clock;
static HWND window;
static void check(bool ok, const char* message) { if (!ok) throw std::runtime_error(message); }
static double elapsed(Clock::time_point start) { return std::chrono::duration<double, std::milli>(Clock::now() - start).count(); }
static void setFps(int fps) {
    std::wstring path = L"\\\\.\\pipe\\EveoRobin_" + std::to_wstring((intptr_t)window);
    HANDLE pipe = INVALID_HANDLE_VALUE;
    for (int i = 0; i < 50 && pipe == INVALID_HANDLE_VALUE; ++i) {
        pipe = CreateFileW(path.c_str(), GENERIC_READ | GENERIC_WRITE, 0, nullptr, OPEN_EXISTING, 0, nullptr);
        if (pipe == INVALID_HANDLE_VALUE) Sleep(20);
    }
    check(pipe != INVALID_HANDLE_VALUE, "FPS pipe connect");
    unsigned char bytes[16] = {0xA2, 0xF1};
    memcpy(bytes + 2, &fps, 4); bytes[6] = 0xF2;
    memcpy(bytes + 7, &fps, 4); bytes[11] = 0xF3;
    memcpy(bytes + 12, &fps, 4);
    DWORD count; unsigned char reply = 0;
    bool ok = WriteFile(pipe, bytes, 16, &count, nullptr) && ReadFile(pipe, &reply, 1, &count, nullptr);
    CloseHandle(pipe); check(ok && reply == 1, "FPS pipe reply");
}
static LONG foreignGuards = 0;
static LONG WINAPI crashDetails(EXCEPTION_POINTERS* p) {
    if (p->ExceptionRecord->ExceptionCode == EXCEPTION_ACCESS_VIOLATION || p->ExceptionRecord->ExceptionCode == EXCEPTION_SINGLE_STEP) {
        HMODULE module = nullptr; wchar_t name[MAX_PATH]{};
        GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            (LPCWSTR)p->ContextRecord->Rip, &module);
        GetModuleFileNameW(module, name, MAX_PATH);
        std::wcerr << L"Exception=" << std::hex << p->ExceptionRecord->ExceptionCode << L" module=" << name << L" offset=" << (p->ContextRecord->Rip - (uintptr_t)module)
            << L" address=" << p->ExceptionRecord->ExceptionInformation[1] << L" dr6=" << p->ContextRecord->Dr6 << L" dr7=" << p->ContextRecord->Dr7 << std::endl;
    }
    return EXCEPTION_CONTINUE_SEARCH;
}
static LONG WINAPI foreignHandler(EXCEPTION_POINTERS* p) {
    if (p->ExceptionRecord->ExceptionCode != STATUS_GUARD_PAGE_VIOLATION) return EXCEPTION_CONTINUE_SEARCH;
    InterlockedIncrement(&foreignGuards); return EXCEPTION_CONTINUE_EXECUTION;
}
int main() {
    SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX);
    try {
        AddVectoredExceptionHandler(0, crashDetails);
        WNDCLASSW wc{}; wc.lpfnWndProc = DefWindowProcW; wc.lpszClassName = L"RobinNativeSmoke"; wc.hInstance = GetModuleHandleW(nullptr);
        RegisterClassW(&wc);
        // An off-screen, nonactivating window gives DXGI and Process.MainWindowHandle a real HWND.
        window = CreateWindowExW(WS_EX_NOACTIVATE, wc.lpszClassName, L"Robin native smoke", WS_POPUP,
            -30000, -30000, 64, 64, nullptr, nullptr, wc.hInstance, nullptr);
        check(window != nullptr, "CreateWindow"); ShowWindow(window, SW_SHOWNOACTIVATE);
        ComPtr<ID3D11Device> device; ComPtr<ID3D11DeviceContext> context; ComPtr<IDXGISwapChain> chain;
        DXGI_SWAP_CHAIN_DESC desc{}; desc.BufferDesc.Width = desc.BufferDesc.Height = 64;
        desc.BufferDesc.Format = DXGI_FORMAT_R8G8B8A8_UNORM; desc.SampleDesc.Count = 1;
        desc.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT; desc.BufferCount = 1; desc.OutputWindow = window; desc.Windowed = TRUE;
        check(SUCCEEDED(D3D11CreateDeviceAndSwapChain(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, 0, nullptr, 0,
            D3D11_SDK_VERSION, &desc, &chain, &device, nullptr, &context)), "D3D11CreateDeviceAndSwapChain");
        auto audio = LoadLibraryW(L"_audio2.dll"); check(audio != nullptr, "Load synthetic audio");
        auto post = (unsigned(*)(unsigned, uint64_t))GetProcAddress(audio, "?PostEvent@SoundEngine@AK@@YAII_KIP6AXW4AkCallbackType@@PEAUAkCallbackInfo@@@ZPEAXIPEAUAkExternalSourceInfo@@I@Z");
        auto stops = (LONG(*)())GetProcAddress(audio, "StopCount");
        auto last = (LONG(*)())GetProcAddress(audio, "LastPlaying");
        check(post && stops && last, "Synthetic audio exports");
        std::cout << (intptr_t)window << std::endl;
        std::string command; std::getline(std::cin, command); check(command == "test", "Driver start command");
        setFps(30); chain->Present(0, 0);
        auto start = Clock::now();
        for (int i = 0; i < 15; ++i) check(SUCCEEDED(chain->Present(0, 0)), "Present");
        auto presentMs = elapsed(start); check(presentMs > 430 && presentMs < 1200, "Present pacing outside 30 FPS budget");
        ComPtr<IDXGISwapChain1> chain1; check(SUCCEEDED(chain.As(&chain1)), "IDXGISwapChain1");
        DXGI_PRESENT_PARAMETERS parameters{}; start = Clock::now();
        for (int i = 0; i < 15; ++i) check(SUCCEEDED(chain1->Present1(0, 0, &parameters)), "Present1");
        auto present1Ms = elapsed(start);
        std::cerr << "Present=" << presentMs << "ms Present1=" << present1Ms << "ms" << std::endl;
        check(present1Ms > 430 && present1Ms < 1200, "Present1 pacing or nested double throttle");
        start = Clock::now(); for (int i = 0; i < 50; ++i) chain->Present(0, DXGI_PRESENT_TEST);
        check(elapsed(start) < 200, "TEST presents were throttled");
        setFps(1); std::thread disable([] { Sleep(50); setFps(0); });
        start = Clock::now(); chain->Present(0, 0); auto disableMs = elapsed(start); disable.join();
        check(disableMs < 250, "Disabling did not interrupt low FPS wait");
        AddVectoredExceptionHandler(0, crashDetails);
        std::cerr << "Starting audio" << std::endl;
        // Driver configured both 42 and nested event 99. Unmuted events must not stop.
        post(7, 0x123456789ABCDEF0ULL); check(stops() == 0, "Unmuted event was stopped");
        std::cerr << "Unmuted audio passed" << std::endl;
        post(42, 0x123456789ABCDEF0ULL); check(stops() == 1 && last() == 1042, "Muted event playing ID");
        post(99, 0x123456789ABCDEF0ULL); check(stops() == 3 && last() == 1099, "Nested PostEvent return tracking");
        for (int i = 0; i < 100; ++i) post(42, 0x123456789ABCDEF0ULL);
        check(stops() == 103, "Repeated audio interception");
        auto veh = AddVectoredExceptionHandler(0, foreignHandler);
        auto page = (volatile char*)VirtualAlloc(nullptr, 4096, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE | PAGE_GUARD);
        check(page != nullptr, "Foreign guard page"); *page = 1;
        check(foreignGuards == 1, "Robin swallowed a foreign guard exception");
        VirtualFree((void*)page, 0, MEM_RELEASE); RemoveVectoredExceptionHandler(veh);
        std::cout << "PASS Present=" << presentMs << "ms Present1=" << present1Ms << "ms disable=" << disableMs << "ms audio=103 foreign-guard=1" << std::endl;
        std::getline(std::cin, command);
        check(command == "focus", "Focus test command");
        for (int i = 0; i < 8; ++i) {
            setFps(1);
            Sleep(275); // Let the previous focus handoff expire.
            chain->Present(0, 0); // Establish this thread's frame timestamp.
            std::cout << "FOCUS_READY" << std::endl;
            start = Clock::now();
            if (i % 2) chain1->Present1(0, 0, &parameters);
            else chain->Present(0, 0);
            LARGE_INTEGER completed; QueryPerformanceCounter(&completed);
            std::cout << "FOCUS_DONE " << completed.QuadPart << " " << elapsed(start) << std::endl;
            std::getline(std::cin, command); check(command == "next", "Focus continuation");
        }
        std::getline(std::cin, command); // Keep Robin alive for owner-exit and shutdown checks.
        return 0;
    } catch (const std::exception& ex) { std::cerr << ex.what() << std::endl; return 1; }
}
