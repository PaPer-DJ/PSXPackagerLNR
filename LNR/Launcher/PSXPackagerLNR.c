// PSXPackagerLNR: minimal Windows launcher (LNR-specific code).
// This is NOT the PSXPackager engine and contains no emulator/converter code.
// It starts the GUI shipped beside it with the --lnr-batch argument.
// Copyright (c) 2026 LaNaveRETRO. Distributed under the repository MIT license.

#define WIN32_LEAN_AND_MEAN
#define UNICODE
#define _UNICODE
#include <windows.h>
#include <shellapi.h>
#include <strsafe.h>
#include <wchar.h>

#pragma comment(lib, "shell32.lib")

#define LNR_PATH_CHARS 32768

static int ShowFailure(const wchar_t* message)
{
    MessageBoxW(NULL, message, L"PSXPackagerLNR - Error de inicio",
                MB_OK | MB_ICONERROR | MB_SETFOREGROUND);
    return 1;
}

int WINAPI wWinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance,
                    PWSTR lpCmdLine, int nCmdShow)
{
    wchar_t folder[LNR_PATH_CHARS];
    wchar_t guiExe[LNR_PATH_CHARS];
    DWORD length;

    (void)hInstance;
    (void)hPrevInstance;
    (void)lpCmdLine;
    (void)nCmdShow;

    length = GetModuleFileNameW(NULL, folder, LNR_PATH_CHARS);
    if (length == 0 || length >= LNR_PATH_CHARS)
        return ShowFailure(L"No se puede localizar la carpeta de este ejecutable.");

    wchar_t* separator = wcsrchr(folder, L'\\');
    if (separator == NULL)
        return ShowFailure(L"No se puede localizar la carpeta de PSXPackagerLNR.");
    *separator = L'\0';

    if (FAILED(StringCchCopyW(guiExe, LNR_PATH_CHARS, folder)) ||
        FAILED(StringCchCatW(guiExe, LNR_PATH_CHARS, L"\\PSXPackagerGUI.exe")))
        return ShowFailure(L"La ruta de la aplicacion es demasiado larga.");

    DWORD attributes = GetFileAttributesW(guiExe);
    if (attributes == INVALID_FILE_ATTRIBUTES || (attributes & FILE_ATTRIBUTE_DIRECTORY))
        return ShowFailure(
            L"No se encuentra PSXPackagerGUI.exe en esta carpeta.\n\n"
            L"Extrae el ZIP completo, con los dos ejecutables,\n"
            L"los recursos y psxpackager.exe en la misma carpeta.");

    // ShellExecute starts the full GUI as a separate process.
    // The --lnr-batch switch opens the LNR batch window directly.
    // No console window, no duplicated .NET runtime, no persistent process.
    HINSTANCE result = ShellExecuteW(NULL, L"open", guiExe,
                                     L"--lnr-batch", folder, SW_SHOWNORMAL);
    if ((INT_PTR)result <= 32)
        return ShowFailure(
            L"Windows no pudo iniciar PSXPackagerGUI.exe.\n\n"
            L"Comprueba que has extraido el ZIP completo y que\n"
            L"tu antivirus no ha bloqueado los archivos.");

    return 0;
}
