#include <Windows.h>

//import
void *(*mono_runtime_invoke)(void *, void *, void **, void **);
void *(*mono_method_desc_new)(const char *name, BOOL include_namespace);
void *(*mono_method_desc_search_in_image)(void *, void*);
void *(*mono_array_new)(void *, void *, uintptr_t);
void *(*mono_get_string_class)();
void *(*mono_jit_init_version)(void *, void *);
void *(*mono_domain_set_config)(void *, char *, char *);
void *(*mono_assembly_load_from)(void *, char *, int *);
void *(*mono_debug_init)(int v);
void *(*mono_image_open_from_data)(char *, int, BOOL, int *);


static int bad_dir(const char *top) {
	MessageBoxA(NULL, "You must place this exe in the game folder (next to other launchers).", top, MB_OK);
	ExitProcess(1);
	return 1;
}

void *hook(void *fname, void *ver) {
	void *domain = mono_jit_init_version(fname, ver);
	HRSRC hres;
	int i;
	static void *images[32];
	static void *asms[32];
	for (i = 0; (hres = FindResource(NULL, MAKEINTRESOURCE(10000+i), MAKEINTRESOURCE(RT_RCDATA))); i++) {
		HGLOBAL hglob = LoadResource(NULL, hres);
		void *res_data = (char*)LockResource(hglob);
		int res_len = SizeofResource(NULL, hres);
		images[i] = mono_image_open_from_data(res_data, res_len, FALSE, NULL);
	}
	for (int j = 0; j < i; j++)
		asms[j] = mono_assembly_load_from(images[j], "patchwork.exe", NULL);

	void *desc = mono_method_desc_new("*:Main", FALSE);
	void *meth = mono_method_desc_search_in_image(desc, images[0]);
	void *args = mono_array_new(domain, mono_get_string_class(), 0);
	mono_runtime_invoke(meth, NULL, args, NULL);
	return domain;
}


wchar_t name[MAX_PATH];
int nlen;
DWORD WINAPI wrapGetModuleFileNameW(HMODULE hModule, LPWSTR  lpFilename, DWORD   nSize) {
	// Unity asks for its exe name?
	if (hModule == NULL) {
		lstrcpyW(lpFilename, name);
		return lstrlenW(lpFilename);
	}
	return GetModuleFileNameW(hModule, lpFilename, nSize);
}

void *wrapGetProcAddress(HMODULE h, const char *name) {
	if (!lstrcmpA(name, "mono_jit_init_version")) {
		return &hook;
	}
	return GetProcAddress(h, name);
}

int APIENTRY wWinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, LPWSTR lpCmdLine, int nCmdShow) {
	// load mono
	nlen = GetModuleFileName(NULL, name, MAX_PATH);
	while (name[--nlen] != '\\');

	lstrcpyW(name + nlen, L"\\iphlpapi.dll");
	DeleteFileW(name);

	lstrcpyW(name + nlen, L"\\koikatu_data\\mono\\mono.dll");
	if (GetEnvironmentVariableA("KK_RUNSTUDIO", NULL, 0))
		lstrcpyW(name + nlen, L"\\charastudio_data\\mono\\mono.dll");
	HMODULE mono = LoadLibrary(name);
	if (!mono)
		return bad_dir("mono.dll not found");

	// resolve symbols
#define LOAD(n) \
	n = (void*)GetProcAddress(mono, #n); \
	if (!n) { \
		MessageBoxA(NULL, #n, "Method missing", MB_OK | MB_ICONASTERISK); \
		ExitProcess(1); \
	}
	LOAD(mono_runtime_invoke);
	LOAD(mono_method_desc_new);
	LOAD(mono_method_desc_search_in_image);
	LOAD(mono_array_new);
	LOAD(mono_get_string_class);
	LOAD(mono_jit_init_version);
	LOAD(mono_image_open_from_data);
	LOAD(mono_assembly_load_from);


	// alright, now do the horrible thing and run the exe via loadlibrary.
	// it will re-load mono, too.
	lstrcpyW(name + nlen, L"\\Koikatu.exe");
	if (GetEnvironmentVariableA("KK_RUNSTUDIO", NULL, 0))
		lstrcpyW(name + nlen, L"\\CharaStudio.exe");
	SetEnvironmentVariableA("DOORSTOP_DISABLE", "1");
	HINSTANCE unityexe = LoadLibraryW(name);
	if (unityexe == NULL)
		return bad_dir("koikatu/charastudio.exe not found or unsupported version of Windows");
	PIMAGE_DOS_HEADER mz = (PIMAGE_DOS_HEADER)unityexe;
	PIMAGE_IMPORT_DESCRIPTOR imports;
#define RVA2PTR(t,base,rva) ((t)(((PCHAR) base) + rva))
	PIMAGE_NT_HEADERS nth = RVA2PTR(PIMAGE_NT_HEADERS, mz, mz->e_lfanew);
	imports = RVA2PTR(PIMAGE_IMPORT_DESCRIPTOR, mz, nth->
		OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT].VirtualAddress);
	for (int i = 0; imports[i].Characteristics; i++) {
		PIMAGE_IMPORT_BY_NAME import;

		PIMAGE_THUNK_DATA thunk = RVA2PTR(PIMAGE_THUNK_DATA, mz, imports[i].FirstThunk);
		char *dllname = RVA2PTR(char*, mz, imports[i].Name);
		HMODULE dll = LoadLibraryA(dllname);
		if (!dll) {
			MessageBoxA(NULL, dllname, "Missing DLL", MB_OK);
			return 0;
		}
		for (; thunk->u1.Function; thunk++) {
			DWORD oldp;
			MEMORY_BASIC_INFORMATION vmi;
			void *tfun;
			if (thunk->u1.Ordinal & IMAGE_ORDINAL_FLAG64) {
				tfun = GetProcAddress(dll, (char*)MAKEINTRESOURCE(IMAGE_ORDINAL64(thunk->u1.Ordinal)));
			}
			else {
				import = RVA2PTR(PIMAGE_IMPORT_BY_NAME, mz, thunk->u1.Function);
				if (!lstrcmpA(import->Name, "GetModuleFileNameW")) {
					tfun = &wrapGetModuleFileNameW;
				} else
				if (!lstrcmpA(import->Name, "GetProcAddress")) {
					tfun = &wrapGetProcAddress;
				} else
				tfun = GetProcAddress(dll, import->Name);
			}
			if (!tfun) {
				//MessageBoxA(NULL, dllname, "oops", MB_OK);
				return 0;
			}
			VirtualQuery(thunk, &vmi, sizeof(vmi));
			if (!VirtualProtect(vmi.BaseAddress, vmi.RegionSize, PAGE_READWRITE, &oldp)) {
				//MessageBoxA(NULL, dllname, "WP", MB_OK);
				return 0;
			}
			thunk->u1.Function = (ULONG_PTR)tfun;
			VirtualProtect(vmi.BaseAddress, vmi.RegionSize, oldp, &oldp);
		}

	}
	int (CALLBACK *dllWinMain)(HINSTANCE, HINSTANCE, LPWSTR, int) = RVA2PTR(void*, mz, nth->OptionalHeader.AddressOfEntryPoint);
	return dllWinMain(hInstance, hPrevInstance, lpCmdLine, nCmdShow);
}


















