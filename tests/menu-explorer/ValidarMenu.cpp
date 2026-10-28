#include "DadosTeste.h"
#include <stdio.h>

static const GUID classe={0xa5b9460f,0x615c,0x49f8,{0x88,0xec,0x13,0x47,0x7d,0xe0,0x0e,0xd9}};
static const IID fabricaId={1,0,0,{0xc0,0,0,0,0,0,0,0x46}};
static const IID menuId={0xbcfce0a0,0xec17,0x11d0,{0x8d,0x10,0,0xa0,0xc9,0x0f,0x27,0x19}};
static const IID inicioId={0x000214e8,0,0,{0xc0,0,0,0,0,0,0,0x46}};
static IContextMenu3* menuAtivo=nullptr;
static bool falhou=false;
static wchar_t pasta[MAX_PATH];
static int captura=0;
static bool capturar=true;
static bool destacou=false;
static HMENU popupTeste=nullptr;
static void Exigir(bool valor,const char* nome);
static BOOL CALLBACK ConferirLimpeza(HWND janela,LPARAM) {
    Exigir(!GetPropW(janela,L"LegendaLivre.a5b9460f.Menu"),"popup fechado sem propriedade da extensao");return TRUE;
}

static void Exigir(bool valor,const char* nome) { if(!valor){printf("FALHA: %s\n",nome);falhou=true;} }
static bool SemSetas(HWND janela,HDC dc,const RECT& externo) {
    auto menu=(HMENU)SendMessageW(janela,0x01e1,0,0);
    bool semSetas=true;
    for(int i=0;i<GetMenuItemCount(menu);i++) {
        RECT item{};Exigir(GetMenuItemRect(nullptr,menu,i,&item)!=FALSE,"retangulo do idioma");
        OffsetRect(&item,-externo.left,-externo.top);
        COLORREF fundo=GetPixel(dc,item.right-2,(item.top+item.bottom)/2);
        if(fundo==CLR_INVALID)semSetas=false;
        for(int y=item.top+3;y<item.bottom-3;y++)for(int x=item.right-12;x<item.right-1;x++)
            if(GetPixel(dc,x,y)!=fundo)semSetas=false;
    }
    return semSetas;
}
static BOOL CALLBACK Capturar(HWND janela,LPARAM) {
    wchar_t tipo[16];GetClassNameW(janela,tipo,16);
    if(lstrcmpW(tipo,L"#32768") || !IsWindowVisible(janela))return TRUE;
    RECT r{};GetWindowRect(janela,&r);
    printf("Propriedade de desenho: %p\n",GetPropW(janela,L"LegendaLivre.a5b9460f.Menu"));
    int largura=r.right-r.left, altura=r.bottom-r.top;
    if(largura<=0 || altura<=0)return TRUE;
    // Lê a superfície já visível antes de PrintWindow provocar uma nova pintura.
    if(captura!=1) {
        HDC visivel=GetWindowDC(janela);
        Exigir(visivel && SemSetas(janela,visivel,r),"superficie visivel sem setas antes de PrintWindow");
        if(visivel)ReleaseDC(janela,visivel);
    }
    BITMAPINFO bi{};bi.bmiHeader.biSize=sizeof(BITMAPINFOHEADER);bi.bmiHeader.biWidth=largura;
    bi.bmiHeader.biHeight=-altura;bi.bmiHeader.biPlanes=1;bi.bmiHeader.biBitCount=32;
    HDC dc=CreateCompatibleDC(nullptr);void* pixels=nullptr;
    HBITMAP bitmap=CreateDIBSection(dc,&bi,DIB_RGB_COLORS,&pixels,nullptr,0);HGDIOBJ anterior=SelectObject(dc,bitmap);
    Exigir(PrintWindow(janela,dc,0)!=FALSE,"captura do popup nativo");
    COLORREF primeiro=GetPixel(dc,0,1);
    printf("Popup %d: %dx%d; borda RGB=%u,%u,%u\n",captura,largura,altura,GetRValue(primeiro),GetGValue(primeiro),GetBValue(primeiro));
    DWORD modo=1,tamanho=sizeof(modo);RegGetValueW(HKEY_CURRENT_USER,L"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize",L"AppsUseLightTheme",RRF_RT_REG_DWORD,nullptr,&modo,&tamanho);
    Exigir(modo!=0 || primeiro==RGB(80,80,80),"contorno discreto no tema escuro");
    int primeiroTexto=largura;
    for(int y=7;y<(altura-3<28?altura-3:28);y++)for(int x=4;x<(largura-3<55?largura-3:55);x++){
        COLORREF cor=GetPixel(dc,x,y);
        if(modo==0 && GetRValue(cor)>180 && GetGValue(cor)>180 && GetBValue(cor)>180 && x<primeiroTexto)primeiroTexto=x;
    }
    printf("Primeiro pixel do texto: %d\n",primeiroTexto);
    Exigir(modo!=0 || primeiroTexto<=15,"texto sem coluna reservada para icone");
    if(captura!=1)Exigir(SemSetas(janela,dc,r),"popup real sem setas nativas");
    wchar_t destino[MAX_PATH];StringCchPrintfW(destino,MAX_PATH,L"%s\\popup-%d.bmp",pasta,captura);
    BITMAPFILEHEADER arquivo{};arquivo.bfType=0x4d42;arquivo.bfOffBits=sizeof(arquivo)+sizeof(BITMAPINFOHEADER);
    arquivo.bfSize=arquivo.bfOffBits+largura*altura*4;
    HANDLE saida=CreateFileW(destino,GENERIC_WRITE,0,nullptr,CREATE_ALWAYS,FILE_ATTRIBUTE_NORMAL,nullptr);DWORD escrito;
    WriteFile(saida,&arquivo,sizeof(arquivo),&escrito,nullptr);WriteFile(saida,&bi.bmiHeader,sizeof(BITMAPINFOHEADER),&escrito,nullptr);
    WriteFile(saida,pixels,largura*altura*4,&escrito,nullptr);CloseHandle(saida);
    SelectObject(dc,anterior);DeleteObject(bitmap);DeleteDC(dc);return TRUE;
}
static LRESULT CALLBACK Procedimento(HWND janela,UINT msg,WPARAM w,LPARAM l) {
    if(msg==WM_TIMER) {
        KillTimer(janela,1);
        if(capturar && captura>=2 && !destacou) {
            Exigir(HiliteMenuItem(janela,popupTeste,captura-2,MF_BYPOSITION|MF_HILITE)!=FALSE,"destacar idioma no popup real");
            Exigir((GetMenuState(popupTeste,captura-2,MF_BYPOSITION)&MF_HILITE)!=0,"idioma ficou destacado");
            destacou=true;SetTimer(janela,1,60,nullptr);return 0;
        }
        if(capturar)EnumThreadWindows(GetCurrentThreadId(),Capturar,0);EndMenu();return 0;
    }
    if(menuAtivo && (msg==WM_INITMENUPOPUP || msg==WM_MEASUREITEM || msg==WM_DRAWITEM || msg==WM_MENUCHAR)) {
        if(msg==WM_INITMENUPOPUP && capturar)printf("Inicializou popup %p\n",(void*)w);
        LRESULT resultado=0;if(SUCCEEDED(menuAtivo->HandleMenuMsg2(msg,w,l,&resultado)))return resultado;
    }
    return DefWindowProcW(janela,msg,w,l);
}
int wmain(int argc,wchar_t** argv) {
    if(argc!=2)return 2;
    StringCchCopyW(pasta,MAX_PATH,argv[1]);
    CoInitializeEx(nullptr,COINIT_APARTMENTTHREADED);
    wchar_t dll[MAX_PATH];StringCchPrintfW(dll,MAX_PATH,L"%s\\MenuExplorer.dll",pasta);
    HMODULE moduloTeste=LoadLibraryW(dll);if(!moduloTeste){printf("LoadLibrary falhou: %lu\n",GetLastError());return 3;}
    auto criar=(HRESULT(__stdcall*)(REFCLSID,REFIID,void**))GetProcAddress(moduloTeste,"DllGetClassObject");
    auto descarregar=(HRESULT(__stdcall*)())GetProcAddress(moduloTeste,"DllCanUnloadNow");
    Exigir(criar && descarregar,"exports COM sem decoracao");
    DWORD antesGdi=GetGuiResources(GetCurrentProcess(),GR_GDIOBJECTS);
    IClassFactory* fabrica=nullptr;Exigir(SUCCEEDED(criar(classe,fabricaId,(void**)&fabrica)),"criacao da fabrica COM");
    IContextMenu3* contexto=nullptr;Exigir(SUCCEEDED(fabrica->CreateInstance(nullptr,menuId,(void**)&contexto)),"criacao de IContextMenu3");fabrica->Release();
    IShellExtInit* inicio=nullptr;Exigir(SUCCEEDED(contexto->QueryInterface(inicioId,(void**)&inicio)),"interface de selecao");
    wchar_t video[MAX_PATH];StringCchPrintfW(video,MAX_PATH,L"%s\\Video de teste.mkv",pasta);DadosTeste dados(video);
    Exigir(SUCCEEDED(inicio->Initialize(nullptr,&dados,nullptr)),"recebimento do arquivo selecionado");
    for(int caso=0;caso<6;caso++) {
        BYTE bloco[sizeof(DROPFILES)+16]{};auto cabecalho=(DROPFILES*)bloco;
        cabecalho->pFiles=sizeof(DROPFILES);cabecalho->fWide=TRUE;
        auto nomes=(wchar_t*)(bloco+sizeof(DROPFILES));nomes[0]=L'a';nomes[1]=0;nomes[2]=0;
        SIZE_T tamanho=sizeof(DROPFILES)+6;
        if(caso==0)nomes[2]=L'b';
        if(caso==1)tamanho--;
        if(caso==2)cabecalho->pFiles++;
        if(caso==3)nomes[0]=0;
        if(caso==4)cabecalho->pFiles=1;
        if(caso==5) {cabecalho->fWide=FALSE;FillMemory(bloco+sizeof(DROPFILES),6,'a');}
        DadosTeste invalida(bloco,tamanho);
        Exigir(FAILED(inicio->Initialize(nullptr,&invalida,nullptr)),"CF_HDROP malformado rejeitado");
        CMINVOKECOMMANDINFO comando{sizeof(comando)};
        Exigir(contexto->InvokeCommand(&comando)==E_INVALIDARG,"selecao antiga descartada apos entrada invalida");
        Exigir(SUCCEEDED(inicio->Initialize(nullptr,&dados,nullptr)),"reinicializacao valida");
    }
    Exigir(inicio->Initialize(nullptr,nullptr,nullptr)==E_INVALIDARG,"dados nulos rejeitados");
    BYTE ansi[sizeof(DROPFILES)+4]{};auto cabecalhoAnsi=(DROPFILES*)ansi;
    cabecalhoAnsi->pFiles=sizeof(DROPFILES);ansi[sizeof(DROPFILES)]='a';
    DadosTeste dadosAnsi(ansi,sizeof(ansi));
    Exigir(SUCCEEDED(inicio->Initialize(nullptr,&dadosAnsi,nullptr)),"lista ANSI valida");
    SIZE_T tamanhoExcesso=sizeof(DROPFILES)+(4097*2+1)*sizeof(wchar_t);
    BYTE* excesso=(BYTE*)HeapAlloc(GetProcessHeap(),HEAP_ZERO_MEMORY,tamanhoExcesso);
    auto cabecalhoExcesso=(DROPFILES*)excesso;cabecalhoExcesso->pFiles=sizeof(DROPFILES);cabecalhoExcesso->fWide=TRUE;
    auto nomesExcesso=(wchar_t*)(excesso+sizeof(DROPFILES));for(int n=0;n<4097;n++)nomesExcesso[n*2]=L'a';
    DadosTeste dadosExcesso(excesso,tamanhoExcesso);HeapFree(GetProcessHeap(),0,excesso);
    Exigir(FAILED(inicio->Initialize(nullptr,&dadosExcesso,nullptr)),"4097 arquivos rejeitados");
    Exigir(SUCCEEDED(inicio->Initialize(nullptr,&dados,nullptr)),"selecao restaurada");inicio->Release();
    WNDCLASSW classeJanela{};classeJanela.lpfnWndProc=Procedimento;classeJanela.hInstance=GetModuleHandleW(nullptr);classeJanela.lpszClassName=L"LegendaLivre.ValidacaoNativa";
    RegisterClassW(&classeJanela);
    HWND janela=CreateWindowW(classeJanela.lpszClassName,L"Teste isolado do menu LegendaLivre",WS_OVERLAPPEDWINDOW,80,80,420,180,nullptr,nullptr,classeJanela.hInstance,nullptr);
    ShowWindow(janela,SW_SHOW);SetForegroundWindow(janela);
    HMENU principal=CreatePopupMenu();
    Exigir(contexto->QueryContextMenu(principal,0,1000,2000,CMF_DEFAULTONLY)==S_OK && GetMenuItemCount(principal)==0,"respeita menu somente de acao padrao");
    Exigir(contexto->QueryContextMenu(principal,0,1000,2000,0)==7,"reserva sete identificadores de comando");
    Exigir(GetMenuItemCount(principal)==1,"uma entrada LegendaLivre");
    HMENU idiomas=GetSubMenu(principal,0);Exigir(GetMenuItemCount(idiomas)==3,"tres idiomas");
    const wchar_t* nomes[]={L"Português (Brasil)",L"Português (Portugal)",L"Inglês"};
    // Desenha um idioma em bitmap para conferir que a área direita contém somente fundo.
    MENUITEMINFOW idiomaDesenho{sizeof(idiomaDesenho)};idiomaDesenho.fMask=MIIM_DATA;
    GetMenuItemInfoW(idiomas,0,TRUE,&idiomaDesenho);
    HDC dcIdioma=CreateCompatibleDC(nullptr);HBITMAP bitmapIdioma=CreateBitmap(240,32,1,32,nullptr);
    HGDIOBJ anteriorIdioma=SelectObject(dcIdioma,bitmapIdioma);
    DRAWITEMSTRUCT desenhoIdioma{};desenhoIdioma.CtlType=ODT_MENU;desenhoIdioma.hDC=dcIdioma;
    desenhoIdioma.rcItem={0,0,240,32};desenhoIdioma.itemData=idiomaDesenho.dwItemData;
    LRESULT resultadoDesenho=0;
    Exigir(SUCCEEDED(contexto->HandleMenuMsg2(WM_DRAWITEM,0,(LPARAM)&desenhoIdioma,&resultadoDesenho)),"desenho do idioma");
    bool semSeta=true;COLORREF fundoIdioma=GetPixel(dcIdioma,239,16);
    for(int y=0;y<32;y++)for(int x=210;x<240;x++)if(GetPixel(dcIdioma,x,y)!=fundoIdioma)semSeta=false;
    Exigir(semSeta,"idiomas sem setas na area direita");
    SelectObject(dcIdioma,anteriorIdioma);DeleteObject(bitmapIdioma);DeleteDC(dcIdioma);
    for(int i=0;i<3;i++) {
        MENUITEMINFOW item{sizeof(item)};item.fMask=MIIM_DATA|MIIM_SUBMENU;Exigir(GetMenuItemInfoW(idiomas,i,TRUE,&item)!=FALSE,"dados do idioma");
        auto acessibilidade=(MSAAMENUINFO*)item.dwItemData;
        Exigir(acessibilidade && acessibilidade->dwMSAASignature==MSAA_MENU_SIG && !lstrcmpW(acessibilidade->pszWText,nomes[i]),"nome acessivel do idioma");
        Exigir(GetMenuItemCount(item.hSubMenu)==2,"duas opcoes por idioma");
        MENUINFO estilo{sizeof(estilo)};estilo.fMask=MIM_STYLE;GetMenuInfo(item.hSubMenu,&estilo);
        Exigir((estilo.dwStyle&MNS_NOCHECK)!=0,"submenus sem reserva de marcacao");
        for(int a=0;a<2;a++)Exigir(GetMenuItemID(item.hSubMenu,a)==(UINT)(1000+2*i+a),"modo e idioma preservados");
    }
    CMINVOKECOMMANDINFO invalido{sizeof(invalido)};invalido.lpVerb=(LPCSTR)7;
    Exigir(contexto->InvokeCommand(&invalido)==E_INVALIDARG,"comando inexistente rejeitado");
    for(UINT i=0;i<6;i++){char verbo[32];Exigir(SUCCEEDED(contexto->GetCommandString(i,GCS_VERBA,nullptr,verbo,32)),"verbo canonico");printf("Verbo %u: %s\n",i,verbo);}
    for(UINT i=0;i<6;i++) {
        CMINVOKECOMMANDINFO comando{sizeof(comando)};comando.lpVerb=(LPCSTR)(UINT_PTR)i;
        Exigir(SUCCEEDED(contexto->InvokeCommand(&comando)),"execucao do modo e idioma selecionados");
    }
    CMINVOKECOMMANDINFOEX comandoUnicode{sizeof(comandoUnicode)};
    comandoUnicode.fMask=CMIC_MASK_UNICODE;comandoUnicode.lpVerbW=L"legenda_top_36";
    Exigir(SUCCEEDED(contexto->InvokeCommand((CMINVOKECOMMANDINFO*)&comandoUnicode)),"execucao de verbo Unicode");
    wchar_t segundoVideo[MAX_PATH];StringCchPrintfW(segundoVideo,MAX_PATH,L"%s\\Outro vídeo.mkv",pasta);
    DadosTeste dadosDuplos(video,segundoVideo);
    contexto->QueryInterface(inicioId,(void**)&inicio);inicio->Initialize(nullptr,&dadosDuplos,nullptr);inicio->Release();
    CMINVOKECOMMANDINFO comandoDuplo{sizeof(comandoDuplo)};comandoDuplo.lpVerb="legenda_melhor_14";
    Exigir(SUCCEEDED(contexto->InvokeCommand(&comandoDuplo)),"execucao para dois arquivos");
    menuAtivo=contexto;
    for(captura=0;captura<4;captura++) {
        destacou=false;popupTeste=captura==1?GetSubMenu(idiomas,0):idiomas;
        SetTimer(janela,1,300,nullptr);
        TrackPopupMenuEx(popupTeste,TPM_RETURNCMD,200,260,janela,nullptr);
        EnumThreadWindows(GetCurrentThreadId(),ConferirLimpeza,0);
    }
    menuAtivo=nullptr;DestroyWindow(janela);DestroyMenu(principal);contexto->Release();
    Exigir(descarregar()==S_OK,"COM liberado e modulo descarregavel");
    FreeLibrary(moduloTeste);CoUninitialize();
    DWORD depoisGdi=GetGuiResources(GetCurrentProcess(),GR_GDIOBJECTS);
    printf("GDI antes=%lu depois=%lu\n",antesGdi,depoisGdi);
    DWORD depoisAquecimento=depoisGdi;
    HMODULE repetirModulo=LoadLibraryW(dll);
    auto repetirCriar=(HRESULT(__stdcall*)(REFCLSID,REFIID,void**))GetProcAddress(repetirModulo,"DllGetClassObject");
    auto repetirDescarregar=(HRESULT(__stdcall*)())GetProcAddress(repetirModulo,"DllCanUnloadNow");
    CoInitializeEx(nullptr,COINIT_APARTMENTTHREADED);
    for(int rodada=0;rodada<50;rodada++) {
        IClassFactory* f=nullptr;IContextMenu3* c=nullptr;IShellExtInit* i=nullptr;
        Exigir(SUCCEEDED(repetirCriar(classe,fabricaId,(void**)&f)),"fabrica repetida");
        Exigir(SUCCEEDED(f->CreateInstance(nullptr,menuId,(void**)&c)),"contexto repetido");f->Release();
        c->QueryInterface(inicioId,(void**)&i);Exigir(SUCCEEDED(i->Initialize(nullptr,&dados,nullptr)),"inicializacao repetida");i->Release();
        HMENU m=CreatePopupMenu();Exigir(c->QueryContextMenu(m,0,1000,2000,0)==7,"montagem repetida");
        HWND h=CreateWindowW(classeJanela.lpszClassName,L"Teste",WS_OVERLAPPED,80,80,420,180,nullptr,nullptr,classeJanela.hInstance,nullptr);
        menuAtivo=c;capturar=false;
        SetTimer(h,1,10,nullptr);TrackPopupMenuEx(GetSubMenu(m,0),TPM_RETURNCMD,200,260,h,nullptr);
        SetTimer(h,1,10,nullptr);TrackPopupMenuEx(GetSubMenu(GetSubMenu(m,0),0),TPM_RETURNCMD,200,260,h,nullptr);
        EnumThreadWindows(GetCurrentThreadId(),ConferirLimpeza,0);
        HMENU alheio=CreatePopupMenu();AppendMenuW(alheio,MF_STRING,55,L"Outro programa");
        SetTimer(h,1,10,nullptr);TrackPopupMenuEx(alheio,TPM_RETURNCMD,200,260,h,nullptr);
        EnumThreadWindows(GetCurrentThreadId(),ConferirLimpeza,0);
        DestroyMenu(alheio);menuAtivo=nullptr;DestroyWindow(h);DestroyMenu(m);c->Release();
        Exigir(repetirDescarregar()==S_OK,"contexto repetido liberado");
        if(rodada==0)depoisAquecimento=GetGuiResources(GetCurrentProcess(),GR_GDIOBJECTS);
    }
    FreeLibrary(repetirModulo);CoUninitialize();
    DWORD depoisRepeticao=GetGuiResources(GetCurrentProcess(),GR_GDIOBJECTS);
    printf("GDI depois do aquecimento=%lu; depois de 50 contextos=%lu\n",depoisAquecimento,depoisRepeticao);
    Exigir(depoisRepeticao<=depoisAquecimento,"sem crescimento de objetos GDI");
    printf("Resultado: %s\n",falhou?"FALHOU":"APROVADO");return falhou?1:0;
}
