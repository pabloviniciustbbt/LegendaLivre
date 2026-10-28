#include "MenuExplorer.h"

HINSTANCE modulo = nullptr;
LONG objetos = 0;
DWORD indiceThread = TLS_OUT_OF_INDEXES;
const CLSID clsidMenu = {0xa5b9460f,0x615c,0x49f8,{0x88,0xec,0x13,0x47,0x7d,0xe0,0x0e,0xd9}};
static const IID iidDesconhecido={0,0,0,{0xc0,0,0,0,0,0,0,0x46}};
static const IID iidFabrica={1,0,0,{0xc0,0,0,0,0,0,0,0x46}};
static const IID iidMenu={0x000214e4,0,0,{0xc0,0,0,0,0,0,0,0x46}};
static const IID iidMenu2={0x000214f4,0,0,{0xc0,0,0,0,0,0,0,0x46}};
static const IID iidMenu3={0xbcfce0a0,0xec17,0x11d0,{0x8d,0x10,0,0xa0,0xc9,0x0f,0x27,0x19}};
static const IID iidInicializacao={0x000214e8,0,0,{0xc0,0,0,0,0,0,0,0x46}};
static const int codigos[] = {6,36,14};
static const char* verbos[] = {"legenda_melhor_6","legenda_top_6","legenda_melhor_36","legenda_top_36","legenda_melhor_14","legenda_top_14"};

MenuExplorer::MenuExplorer() { InterlockedIncrement(&objetos); }
MenuExplorer::~MenuExplorer() {
    if (gancho) UnhookWindowsHookEx(gancho);
    if (TlsGetValue(indiceThread) == this) TlsSetValue(indiceThread, nullptr);
    if (!anexado && menus[0]) DestroyMenu(menus[0]);
    if (arquivos) GlobalFree(arquivos);
    if (icone) DeleteObject(icone);
    if (fonte) DeleteObject(fonte);
    InterlockedDecrement(&objetos);
}
HRESULT MenuExplorer::QueryInterface(REFIID iid, void** objeto) {
    if (!objeto) return E_POINTER;
    *objeto = nullptr;
    if (iid == iidDesconhecido || iid == iidMenu || iid == iidMenu2 || iid == iidMenu3)
        *objeto = static_cast<IContextMenu3*>(this);
    else if (iid == iidInicializacao) *objeto = static_cast<IShellExtInit*>(this);
    else return E_NOINTERFACE;
    AddRef(); return S_OK;
}
ULONG MenuExplorer::AddRef() { return InterlockedIncrement(&referencias); }
ULONG MenuExplorer::Release() {
    ULONG total = InterlockedDecrement(&referencias);
    if (!total) { this->~MenuExplorer(); HeapFree(GetProcessHeap(), 0, this); }
    return total;
}
HRESULT MenuExplorer::Initialize(PCIDLIST_ABSOLUTE, IDataObject* dados, HKEY) {
    if (arquivos) { GlobalFree(arquivos); arquivos=nullptr; }
    if (!dados) return E_INVALIDARG;
    FORMATETC formato = {CF_HDROP,nullptr,DVASPECT_CONTENT,-1,TYMED_HGLOBAL};
    STGMEDIUM conteudo{};
    HRESULT resultado = dados->GetData(&formato, &conteudo);
    if (FAILED(resultado)) return resultado;
    if(conteudo.tymed!=TYMED_HGLOBAL || !conteudo.hGlobal) { ReleaseStgMedium(&conteudo);return DV_E_TYMED; }
    SIZE_T tamanho = GlobalSize(conteudo.hGlobal);
    void* origem = GlobalLock(conteudo.hGlobal);
    bool valido=origem && tamanho>=sizeof(DROPFILES) && tamanho<=16*1024*1024;
    UINT quantidade=0;
    if(valido) {
        auto cabecalho=(DROPFILES*)origem;
        SIZE_T passo=cabecalho->fWide?sizeof(wchar_t):1, deslocamento=cabecalho->pFiles;
        valido=deslocamento>=sizeof(DROPFILES) && deslocamento<tamanho && deslocamento%passo==0 && (tamanho-deslocamento)%passo==0;
        bool terminou=false;SIZE_T inicio=deslocamento;
        if(valido) for(SIZE_T p=deslocamento;p+passo<=tamanho;p+=passo) {
            bool zero=cabecalho->fWide?*(wchar_t*)((BYTE*)origem+p)==0:*((BYTE*)origem+p)==0;
            if(!zero)continue;
            if(p==inicio){ terminou=quantidade>0;break; }
            if((p-inicio)/passo>32700 || ++quantidade>4096)break;
            inicio=p+passo;
        }
        valido=valido && terminou && quantidade<=4096;
    }
    if (valido) {
        arquivos = GlobalAlloc(GHND, tamanho);
        void* destino = arquivos ? GlobalLock(arquivos) : nullptr;
        if (destino) { CopyMemory(destino, origem, tamanho); GlobalUnlock(arquivos); }
        else if(arquivos) {GlobalFree(arquivos);arquivos=nullptr;}
    }
    if (origem) GlobalUnlock(conteudo.hGlobal);
    ReleaseStgMedium(&conteudo);
    return arquivos ? S_OK : E_INVALIDARG;
}
void MenuExplorer::PrepararEntrada(int indice, const wchar_t* texto, bool idioma) {
    entradas[indice].texto = texto;
    entradas[indice].idioma = idioma;
    entradas[indice].acessibilidade.dwMSAASignature = MSAA_MENU_SIG;
    entradas[indice].acessibilidade.cchWText = (DWORD)lstrlenW(texto);
    entradas[indice].acessibilidade.pszWText = const_cast<wchar_t*>(texto);
}
bool MenuExplorer::InserirEntrada(HMENU menu, UINT comando, int indice, HMENU filho) {
    MENUITEMINFOW item{sizeof(item)};
    item.fMask = MIIM_FTYPE|MIIM_DATA|MIIM_ID|MIIM_STRING;
    item.fType = MFT_OWNERDRAW;
    item.wID = comando;
    item.dwItemData = reinterpret_cast<ULONG_PTR>(&entradas[indice]);
    item.dwTypeData = const_cast<wchar_t*>(entradas[indice].texto);
    if (filho) { item.fMask |= MIIM_SUBMENU; item.hSubMenu = filho; }
    return InsertMenuItemW(menu, GetMenuItemCount(menu), TRUE, &item) != FALSE;
}
HRESULT MenuExplorer::QueryContextMenu(HMENU principal, UINT posicao, UINT primeiro, UINT ultimo, UINT flags) {
    if ((flags & CMF_DEFAULTONLY) || !arquivos) return S_OK;
    if (ultimo<primeiro || ultimo - primeiro < 6 || menus[0]) return E_INVALIDARG;
    PrepararFonte();
    const wchar_t* idiomas[] = {L"Português (Brasil)",L"Português (Portugal)",L"Inglês"};
    menus[0] = CreatePopupMenu();
    if(!menus[0])return E_OUTOFMEMORY;
    for (int i = 0; i < 3; i++) {
        menus[i+1] = CreatePopupMenu();
        if (!menus[i+1]) return E_OUTOFMEMORY;
        PrepararEntrada(i, idiomas[i], true);
        PrepararEntrada(3+2*i, L"Melhor legenda", false);
        PrepararEntrada(4+2*i, L"Top 10", false);
        if (!InserirEntrada(menus[0], primeiro+6, i, menus[i+1])) { DestroyMenu(menus[i+1]);menus[i+1]=nullptr;return E_FAIL; }
        if (!InserirEntrada(menus[i+1], primeiro+2*i, 3+2*i, nullptr) ||
            !InserirEntrada(menus[i+1], primeiro+2*i+1, 4+2*i, nullptr)) return E_FAIL;
    }
    MENUINFO estilo{sizeof(estilo)};
    estilo.fMask = MIM_STYLE|MIM_APPLYTOSUBMENUS;
    estilo.dwStyle = MNS_NOCHECK;
    if (!SetMenuInfo(menus[0], &estilo)) return HRESULT_FROM_WIN32(GetLastError());
    wchar_t caminho[MAX_PATH];
    HICON imagem = nullptr;
    if (GetModuleFileNameW(modulo,caminho,MAX_PATH)) {
        wchar_t* barra = wcsrchr(caminho,L'\\');
        if (barra) { StringCchCopyW(barra+1,MAX_PATH-(barra+1-caminho),L"LegendaLivre.exe"); ExtractIconExW(caminho,0,nullptr,&imagem,1); }
    }
    if (imagem) {
        BITMAPINFO bi{}; bi.bmiHeader.biSize=sizeof(BITMAPINFOHEADER);
        bi.bmiHeader.biWidth=16; bi.bmiHeader.biHeight=-16; bi.bmiHeader.biPlanes=1; bi.bmiHeader.biBitCount=32;
        void* pixels=nullptr; HDC dc=CreateCompatibleDC(nullptr);
        icone=CreateDIBSection(dc,&bi,DIB_RGB_COLORS,&pixels,nullptr,0);
        if(icone) { ZeroMemory(pixels,16*16*4); HGDIOBJ anterior=SelectObject(dc,icone); DrawIconEx(dc,0,0,imagem,16,16,0,nullptr,DI_NORMAL); SelectObject(dc,anterior); }
        DeleteDC(dc); DestroyIcon(imagem);
    }
    MENUITEMINFOW raiz{sizeof(raiz)};
    raiz.fMask=MIIM_STRING|MIIM_SUBMENU|MIIM_ID;
    raiz.dwTypeData=const_cast<wchar_t*>(L"LegendaLivre"); raiz.hSubMenu=menus[0]; raiz.wID=primeiro+6;
    if(icone) { raiz.fMask|=MIIM_BITMAP; raiz.hbmpItem=icone; }
    if (!InsertMenuItemW(principal,posicao,TRUE,&raiz)) return E_FAIL;
    anexado=true;
    return MAKE_HRESULT(SEVERITY_SUCCESS,0,7);
}
HRESULT MenuExplorer::InvokeCommand(CMINVOKECOMMANDINFO* comando) {
    if (!comando || comando->cbSize<sizeof(CMINVOKECOMMANDINFO) || !arquivos) return E_INVALIDARG;
    UINT indice=LOWORD(comando->lpVerb);
    auto estendido=reinterpret_cast<CMINVOKECOMMANDINFOEX*>(comando);
    if(comando->cbSize>=sizeof(CMINVOKECOMMANDINFOEX) && (comando->fMask&CMIC_MASK_UNICODE) && HIWORD(estendido->lpVerbW)) {
        indice=6;wchar_t verbo[32];
        for(UINT i=0;i<6;i++) { MultiByteToWideChar(CP_ACP,0,verbos[i],-1,verbo,32);if(!lstrcmpiW(estendido->lpVerbW,verbo)){indice=i;break;} }
    } else if (HIWORD(comando->lpVerb)) {
        indice=6;
        for(UINT i=0;i<6;i++) if(!lstrcmpiA(comando->lpVerb,verbos[i])) { indice=i; break; }
    }
    if(indice>=6) return E_INVALIDARG;
    wchar_t exe[MAX_PATH];
    DWORD tamanho=GetModuleFileNameW(modulo,exe,MAX_PATH);
    if(!tamanho || tamanho>=MAX_PATH) return HRESULT_FROM_WIN32(ERROR_FILENAME_EXCED_RANGE);
    wchar_t* barra=wcsrchr(exe,L'\\'); if(!barra) return E_FAIL;
    if(FAILED(StringCchCopyW(barra+1,MAX_PATH-(barra+1-exe),L"LegendaLivre.exe"))) return E_FAIL;
    UINT quantidade=DragQueryFileW((HDROP)arquivos,0xffffffff,nullptr,0);
    for(UINT i=0;i<quantidade;i++) {
        UINT caracteres=DragQueryFileW((HDROP)arquivos,i,nullptr,0);
        if(!caracteres || caracteres>32700) return E_INVALIDARG;
        auto video=(wchar_t*)HeapAlloc(GetProcessHeap(),HEAP_ZERO_MEMORY,(caracteres+1)*sizeof(wchar_t));
        SIZE_T capacidade=caracteres+lstrlenW(exe)+40;
        auto linha=(wchar_t*)HeapAlloc(GetProcessHeap(),HEAP_ZERO_MEMORY,capacidade*sizeof(wchar_t));
        if(!video || !linha) { if(video)HeapFree(GetProcessHeap(),0,video); if(linha)HeapFree(GetProcessHeap(),0,linha); return E_OUTOFMEMORY; }
        DragQueryFileW((HDROP)arquivos,i,video,caracteres+1);
        HRESULT montagem=wcschr(video,L'"') ? E_INVALIDARG : StringCchPrintfW(linha,capacidade,L"\"%ls\" %ls %d \"%ls\"",exe,indice%2?L"top":L"melhor",codigos[indice/2],video);
        STARTUPINFOW inicio{sizeof(inicio)}; PROCESS_INFORMATION processo{};
        BOOL abriu=SUCCEEDED(montagem) && CreateProcessW(exe,linha,nullptr,nullptr,FALSE,0,nullptr,nullptr,&inicio,&processo);
        DWORD erro=abriu ? 0 : GetLastError();
        HeapFree(GetProcessHeap(),0,video); HeapFree(GetProcessHeap(),0,linha);
        if(FAILED(montagem)) return montagem;
        if(!abriu) return HRESULT_FROM_WIN32(erro);
        CloseHandle(processo.hThread); CloseHandle(processo.hProcess);
    }
    return S_OK;
}
HRESULT MenuExplorer::GetCommandString(UINT_PTR indice, UINT flags, UINT*, LPSTR texto, UINT tamanho) {
    if(indice>=6) return E_INVALIDARG;
    if(flags==GCS_VALIDATEA || flags==GCS_VALIDATEW) return S_OK;
    if(!texto || !tamanho) return E_POINTER;
    if(flags==GCS_VERBA) return StringCchCopyA(texto,tamanho,verbos[indice]);
    if(flags==GCS_VERBW) { return MultiByteToWideChar(CP_ACP,0,verbos[indice],-1,(LPWSTR)texto,tamanho) ? S_OK : E_FAIL; }
    return E_NOTIMPL;
}

class Fabrica final : public IClassFactory {
    LONG referencias=1;
public:
    Fabrica() { InterlockedIncrement(&objetos); }
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid,void** objeto) override {
        if(!objeto)return E_POINTER; *objeto=nullptr;
        if(iid!=iidDesconhecido && iid!=iidFabrica)return E_NOINTERFACE;
        *objeto=this; AddRef(); return S_OK;
    }
    ULONG STDMETHODCALLTYPE AddRef() override { return InterlockedIncrement(&referencias); }
    ULONG STDMETHODCALLTYPE Release() override { ULONG n=InterlockedDecrement(&referencias); if(!n){ this->~Fabrica();HeapFree(GetProcessHeap(),0,this);InterlockedDecrement(&objetos); } return n; }
    HRESULT STDMETHODCALLTYPE CreateInstance(IUnknown* externo,REFIID iid,void** objeto) override {
        if(externo)return CLASS_E_NOAGGREGATION;
        void* memoria=HeapAlloc(GetProcessHeap(),HEAP_ZERO_MEMORY,sizeof(MenuExplorer)); if(!memoria)return E_OUTOFMEMORY;
        auto menu=new(memoria) MenuExplorer(); HRESULT h=menu->QueryInterface(iid,objeto);menu->Release();return h;
    }
    HRESULT STDMETHODCALLTYPE LockServer(BOOL travar) override { if(travar)InterlockedIncrement(&objetos);else InterlockedDecrement(&objetos);return S_OK; }
};
extern "C" HRESULT __stdcall DllGetClassObject(REFCLSID clsid,REFIID iid,void** objeto) {
    if(clsid!=clsidMenu)return CLASS_E_CLASSNOTAVAILABLE;
    void* memoria=HeapAlloc(GetProcessHeap(),HEAP_ZERO_MEMORY,sizeof(Fabrica)); if(!memoria)return E_OUTOFMEMORY;
    auto fabrica=new(memoria) Fabrica();HRESULT h=fabrica->QueryInterface(iid,objeto);fabrica->Release();return h;
}
extern "C" HRESULT __stdcall DllCanUnloadNow() { return objetos==0 ? S_OK : S_FALSE; }
BOOL WINAPI DllMain(HINSTANCE instancia,DWORD motivo,LPVOID) {
    if(motivo==DLL_PROCESS_ATTACH){ modulo=instancia; indiceThread=TlsAlloc();DisableThreadLibraryCalls(instancia);return indiceThread!=TLS_OUT_OF_INDEXES; }
    if(motivo==DLL_PROCESS_DETACH && indiceThread!=TLS_OUT_OF_INDEXES)TlsFree(indiceThread);
    return TRUE;
}
