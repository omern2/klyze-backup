#pragma once
// StrongDetections.hpp - Kernel-level güce yakın usermode tespit teknikleri

#include <Windows.h>
#include <TlHelp32.h>
#include <Psapi.h>
#include <intrin.h>
#include <vector>
#include <string>
#include <set>
#include "../Common/Logger.hpp"

// NtQuerySystemInformation function pointer
typedef NTSTATUS(NTAPI* pfnNtQuerySystemInformation)(
    ULONG  SystemInformationClass,
    PVOID  SystemInformation,
    ULONG  SystemInformationLength,
    PULONG ReturnLength
);

// winternl.h'deki _SYSTEM_PROCESS_INFORMATION ile çakışmamak için kendi namespace'imizde tanımlıyoruz
namespace UAC_Internal
{
    struct PROCESS_INFO_ENTRY {
        ULONG  NextEntryOffset;
        ULONG  NumberOfThreads;
        BYTE   Reserved1[48];
        PVOID  Reserved2[3];
        HANDLE UniqueProcessId;
        PVOID  Reserved3;
        ULONG  HandleCount;
        BYTE   Reserved4[4];
        PVOID  Reserved5[11];
        SIZE_T PeakPagefileUsage;
        SIZE_T PrivatePageCount;
        LARGE_INTEGER Reserved6[6];
    };
}

class StrongDetections
{
public:

    // --------------------------------------------------------
    // TEKNİK 1: Çift Kaynak Process Tespiti
    // WinAPI vs NtQuerySystemInformation karşılaştırması
    // --------------------------------------------------------
    static bool DetectHiddenProcesses()
    {
        std::set<DWORD> winApiPids;
        std::set<DWORD> ntQueryPids;

        // Kaynak 1: WinAPI
        HANDLE hSnap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (hSnap != INVALID_HANDLE_VALUE)
        {
            PROCESSENTRY32W pe = { sizeof(pe) };
            if (Process32FirstW(hSnap, &pe))
            {
                do { winApiPids.insert(pe.th32ProcessID); }
                while (Process32NextW(hSnap, &pe));
            }
            CloseHandle(hSnap);
        }

        // Kaynak 2: NtQuerySystemInformation
        HMODULE hNtdll = GetModuleHandleA("ntdll.dll");
        if (!hNtdll) return false;

        auto NtQSI = (pfnNtQuerySystemInformation)GetProcAddress(hNtdll, "NtQuerySystemInformation");
        if (!NtQSI) return false;

        ULONG bufSize = 1024 * 1024;
        BYTE* buf = (BYTE*)VirtualAlloc(NULL, bufSize, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
        if (!buf) return false;

        ULONG retLen = 0;
        NTSTATUS status;

        while ((status = NtQSI(5, buf, bufSize, &retLen)) == (NTSTATUS)0xC0000004L)
        {
            VirtualFree(buf, 0, MEM_RELEASE);
            bufSize = retLen + 4096;
            buf = (BYTE*)VirtualAlloc(NULL, bufSize, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
            if (!buf) return false;
        }

        if (status == 0) // STATUS_SUCCESS
        {
            UAC_Internal::PROCESS_INFO_ENTRY* entry = (UAC_Internal::PROCESS_INFO_ENTRY*)buf;
            while (true)
            {
                ntQueryPids.insert((DWORD)(uintptr_t)entry->UniqueProcessId);
                if (entry->NextEntryOffset == 0) break;
                entry = (UAC_Internal::PROCESS_INFO_ENTRY*)((BYTE*)entry + entry->NextEntryOffset);
            }
        }

        VirtualFree(buf, 0, MEM_RELEASE);

        bool foundHidden = false;
        for (DWORD pid : ntQueryPids)
        {
            if (pid == 0 || pid == 4) continue;
            if (winApiPids.find(pid) == winApiPids.end())
            {
                Logger::logf(Detection, "[StrongDetections] Hidden process! PID: %d (NtQuery'de var, WinAPI'de yok)", pid);
                foundHidden = true;
            }
        }

        return foundHidden;
    }

    // --------------------------------------------------------
    // TEKNİK 2: NTDLL Syscall Hook Tespiti
    // --------------------------------------------------------
    static bool DetectNtdllHooks()
    {
        const char* criticalFunctions[] = {
            "NtQuerySystemInformation",
            "NtQueryInformationProcess",
            "NtOpenProcess",
            "NtReadVirtualMemory",
            "NtWriteVirtualMemory",
            "NtAllocateVirtualMemory",
            "NtProtectVirtualMemory",
            "NtCreateThreadEx",
            "NtQueryVirtualMemory",
            "NtGetContextThread",
            nullptr
        };

        HMODULE hNtdll = GetModuleHandleA("ntdll.dll");
        if (!hNtdll) return false;

        bool hookFound = false;

        for (int i = 0; criticalFunctions[i] != nullptr; i++)
        {
            BYTE* funcAddr = (BYTE*)GetProcAddress(hNtdll, criticalFunctions[i]);
            if (!funcAddr) continue;

            BYTE firstByte  = funcAddr[0];
            BYTE secondByte = funcAddr[1];

            bool isHooked = false;

            if (firstByte == 0xE9 || firstByte == 0xEB)           // jmp rel
                isHooked = true;
            else if (firstByte == 0xFF && secondByte == 0x25)      // jmp [rip+x]
                isHooked = true;
            else if (firstByte == 0x90 && secondByte == 0x90)      // NOP sled
                isHooked = true;

            if (isHooked)
            {
                Logger::logf(Detection, "[StrongDetections] NTDLL hook tespit edildi: %s (ilk byte: 0x%02X)", criticalFunctions[i], firstByte);
                hookFound = true;
            }
        }

        return hookFound;
    }

    // --------------------------------------------------------
    // TEKNİK 3: Gizli Modül / VAD Taraması
    // Yüklü modüller listesinde olmayan çalıştırılabilir bellek bölgelerini bul
    // --------------------------------------------------------
    static int ScanForHiddenModules()
    {
        // Bilinen modül aralıklarını topla
        struct ModRange { uintptr_t base; uintptr_t end; };
        std::vector<ModRange> knownRanges;

        HANDLE hSnap = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE, GetCurrentProcessId());
        if (hSnap != INVALID_HANDLE_VALUE)
        {
            MODULEENTRY32W me = { sizeof(me) };
            if (Module32FirstW(hSnap, &me))
            {
                do {
                    ModRange r;
                    r.base = (uintptr_t)me.modBaseAddr;
                    r.end  = r.base + me.modBaseSize;
                    knownRanges.push_back(r);
                } while (Module32NextW(hSnap, &me));
            }
            CloseHandle(hSnap);
        }

        int suspiciousCount = 0;
        uintptr_t addr = 0;
        MEMORY_BASIC_INFORMATION mbi = {};

        while (VirtualQuery((LPCVOID)addr, &mbi, sizeof(mbi)) == sizeof(mbi))
        {
            if (mbi.State == MEM_COMMIT &&
                mbi.Type == MEM_PRIVATE &&
                (mbi.Protect & (PAGE_EXECUTE | PAGE_EXECUTE_READ | PAGE_EXECUTE_READWRITE | PAGE_EXECUTE_WRITECOPY)))
            {
                uintptr_t regionBase = (uintptr_t)mbi.BaseAddress;
                bool isKnown = false;

                for (const auto& r : knownRanges)
                {
                    if (regionBase >= r.base && regionBase < r.end)
                    {
                        isKnown = true;
                        break;
                    }
                }

                if (!isKnown && mbi.RegionSize >= 0x1000)
                {
                    // MZ header kontrolü - SEH olmadan, sadece okuma
                    BYTE header[2] = { 0, 0 };
                    SIZE_T bytesRead = 0;
                    if (ReadProcessMemory(GetCurrentProcess(), (LPCVOID)regionBase, header, 2, &bytesRead) && bytesRead == 2)
                    {
                        if (header[0] == 'M' && header[1] == 'Z')
                        {
                            Logger::logf(Detection, "[StrongDetections] Gizli PE modülü: 0x%llX (boyut: %zu)", regionBase, mbi.RegionSize);
                            suspiciousCount++;
                        }
                        else if (mbi.RegionSize > 0x10000) // 64KB+ private executable
                        {
                            Logger::logf(Detection, "[StrongDetections] Şüpheli çalıştırılabilir bölge: 0x%llX (boyut: %zu)", regionBase, mbi.RegionSize);
                            suspiciousCount++;
                        }
                    }
                }
            }

            uintptr_t next = (uintptr_t)mbi.BaseAddress + mbi.RegionSize;
            if (next <= addr) break; // overflow koruması
            addr = next;
        }

        return suspiciousCount;
    }

    // --------------------------------------------------------
    // TEKNİK 4: Hardware Breakpoint Tespiti (DR Register)
    // --------------------------------------------------------
    static bool DetectHardwareBreakpoints()
    {
        HANDLE hSnap = CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0);
        if (hSnap == INVALID_HANDLE_VALUE) return false;

        bool found = false;
        DWORD currentPid = GetCurrentProcessId();
        THREADENTRY32 te = { sizeof(te) };

        if (Thread32First(hSnap, &te))
        {
            do
            {
                if (te.th32OwnerProcessID != currentPid) continue;

                HANDLE hThread = OpenThread(THREAD_GET_CONTEXT | THREAD_SUSPEND_RESUME, FALSE, te.th32ThreadID);
                if (!hThread) continue;

                CONTEXT ctx = {};
                ctx.ContextFlags = CONTEXT_DEBUG_REGISTERS;

                if (GetThreadContext(hThread, &ctx))
                {
                    if (ctx.Dr0 || ctx.Dr1 || ctx.Dr2 || ctx.Dr3 || ctx.Dr7 > 1)
                    {
                        Logger::logf(Detection, "[StrongDetections] Hardware breakpoint! Thread: %d | DR0=%llX DR1=%llX DR2=%llX DR3=%llX DR7=%llX",
                            te.th32ThreadID, ctx.Dr0, ctx.Dr1, ctx.Dr2, ctx.Dr3, ctx.Dr7);
                        found = true;
                    }
                }

                CloseHandle(hThread);

            } while (Thread32Next(hSnap, &te));
        }

        CloseHandle(hSnap);
        return found;
    }

    // --------------------------------------------------------
    // TEKNİK 5: Timing Anomali Tespiti
    // --------------------------------------------------------
    static bool DetectTimingAnomaly()
    {
        LARGE_INTEGER freq, start, end;
        QueryPerformanceFrequency(&freq);
        QueryPerformanceCounter(&start);

        volatile int dummy = 0;
        for (int i = 0; i < 1000; i++) dummy += i;

        QueryPerformanceCounter(&end);

        double elapsedMs = (double)(end.QuadPart - start.QuadPart) * 1000.0 / freq.QuadPart;

        if (elapsedMs > 50.0)
        {
            Logger::logf(Detection, "[StrongDetections] Timing anomalisi! Gecikme: %.2f ms (beklenen < 1ms)", elapsedMs);
            return true;
        }

        return false;
    }

    // --------------------------------------------------------
    // Tüm kontrolleri çalıştır
    // --------------------------------------------------------
    static int RunAllChecks()
    {
        int detectionCount = 0;

        Logger::logf(Info, "[StrongDetections] Gelismis tespit suite baslatiliyor...");

        if (DetectHiddenProcesses())    detectionCount++;
        if (DetectNtdllHooks())         detectionCount++;
        if (ScanForHiddenModules() > 0) detectionCount++;
        if (DetectHardwareBreakpoints()) detectionCount++;
        if (DetectTimingAnomaly())      detectionCount++;

        Logger::logf(Info, "[StrongDetections] Tamamlandi. %d tespit.", detectionCount);
        return detectionCount;
    }
};
