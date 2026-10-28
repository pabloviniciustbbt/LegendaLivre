#include "MenuExplorer.h"
#include <dwmapi.h>

struct PaletaMenu { COLORREF fundo,texto,selecao,textoSelecionado,borda; };
static const wchar_t* propriedadeMenu=L"LegendaLivre.a5b9460f.Menu";
static const UINT limparSetas=WM_APP+76;
struct EstadoPopup { MenuExplorer* dono; COLORREF bordaAnterior; bool restaurar; };
static PaletaMenu Cores() {
    HIGHCONTRASTW contraste{sizeof(contraste)};
    if(SystemParametersInfoW(SPI_GETHIGHCONTRAST,sizeof(contraste),&contraste,0) && (contraste.dwFlags&HCF_HIGHCONTRASTON))
        return {GetSysColor(COLOR_WINDOW),GetSysColor(COLOR_WINDOWTEXT),GetSysColor(COLOR_HIGHLIGHT),GetSysColor(COLOR_HIGHLIGHTTEXT),GetSysColor(COLOR_WINDOWTEXT)};
    DWORD claro=1, tamanho=sizeof(claro);
    RegGetValueW(HKEY_CURRENT_USER,L"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize",L"AppsUseLightTheme",RRF_RT_REG_DWORD,nullptr,&claro,&tamanho);
    return claro ? PaletaMenu{RGB(243,243,243),RGB(27,27,27),RGB(225,225,225),RGB(27,27,27),RGB(160,160,160)}
        : PaletaMenu{RGB(32,32,32),RGB(245,245,245),RGB(55,55,55),RGB(245,245,245),RGB(80,80,80)};
}
void MenuExplorer::PrepararFonte() {
    dpi=GetDpiForWindow(GetActiveWindow()); if(!dpi)dpi=96;
    NONCLIENTMETRICSW metricas{sizeof(metricas)};
    if(!SystemParametersInfoForDpi(SPI_GETNONCLIENTMETRICS,sizeof(metricas),&metricas,0,dpi))
        SystemParametersInfoW(SPI_GETNONCLIENTMETRICS,sizeof(metricas),&metricas,0);
    fonte=CreateFontIndirectW(&metricas.lfMenuFont);
}
bool MenuExplorer::EntradaPropria(ULONG_PTR valor) {
    ULONG_PTR inicio=reinterpret_cast<ULONG_PTR>(&entradas[0]);
    ULONG_PTR fim=reinterpret_cast<ULONG_PTR>(&entradas[9]);
    return valor>=inicio && valor<fim && (valor-inicio)%sizeof(EntradaMenu)==0;
}
void MenuExplorer::PintarEntrada(DRAWITEMSTRUCT* desenho,EntradaMenu* entrada) {
    auto cores=Cores();
    bool selecionada=(desenho->itemState&ODS_SELECTED)!=0;
    HBRUSH pincel=CreateSolidBrush(selecionada?cores.selecao:cores.fundo);
    FillRect(desenho->hDC,&desenho->rcItem,pincel); DeleteObject(pincel);
    HGDIOBJ anterior=SelectObject(desenho->hDC,fonte);
    int modo=SetBkMode(desenho->hDC,TRANSPARENT);
    COLORREF cor=selecionada?cores.textoSelecionado:cores.texto;
    COLORREF anteriorTexto=SetTextColor(desenho->hDC,cor);
    RECT texto=desenho->rcItem;
    texto.left+=MulDiv(8,dpi,96); texto.right-=MulDiv(8,dpi,96);
    DrawTextW(desenho->hDC,entrada->texto,-1,&texto,DT_SINGLELINE|DT_VCENTER|DT_NOPREFIX);
    SetTextColor(desenho->hDC,anteriorTexto); SetBkMode(desenho->hDC,modo); SelectObject(desenho->hDC,anterior);
    // O Windows pinta sua seta depois do owner-draw, inclusive ao trocar a seleção.
    if(entrada->idioma && popupIdiomas)PostMessageW(popupIdiomas,limparSetas,0,0);
}
void MenuExplorer::LimparSetas(HWND janela,HMENU menu,HDC destino) {
    HDC dc=destino?destino:GetDC(janela);if(!dc)return;
    auto cores=Cores();
    for(int i=0;i<GetMenuItemCount(menu);i++) {
        if(!GetSubMenu(menu,i))continue;
        RECT area{};if(!GetMenuItemRect(nullptr,menu,i,&area))continue;
        MapWindowPoints(nullptr,janela,reinterpret_cast<POINT*>(&area),2);
        area.left=area.right-MulDiv(16,dpi,96);
        HBRUSH pincel=CreateSolidBrush((GetMenuState(menu,i,MF_BYPOSITION)&MF_HILITE)?cores.selecao:cores.fundo);
        FillRect(dc,&area,pincel);DeleteObject(pincel);
    }
    if(!destino)ReleaseDC(janela,dc);
}
void MenuExplorer::PintarContorno(HWND janela,HDC destino) {
    RECT externo{},cliente{}; POINT origem{};
    if(!GetWindowRect(janela,&externo) || !GetClientRect(janela,&cliente) || !ClientToScreen(janela,&origem))return;
    OffsetRect(&cliente,origem.x-externo.left,origem.y-externo.top);
    OffsetRect(&externo,-externo.left,-externo.top);
    HDC dc=destino?destino:GetWindowDC(janela); if(!dc)return;
    int salvo=SaveDC(dc);
    ExcludeClipRect(dc,cliente.left,cliente.top,cliente.right,cliente.bottom);
    auto cores=Cores(); HBRUSH pincel=CreateSolidBrush(cores.fundo);
    FillRect(dc,&externo,pincel); DeleteObject(pincel);
    HPEN caneta=CreatePen(PS_SOLID,1,cores.borda);
    HGDIOBJ anteriorCaneta=SelectObject(dc,caneta), anteriorPincel=SelectObject(dc,GetStockObject(NULL_BRUSH));
    Rectangle(dc,0,0,externo.right,externo.bottom);
    SelectObject(dc,anteriorCaneta); SelectObject(dc,anteriorPincel); DeleteObject(caneta);
    RestoreDC(dc,salvo); if(!destino)ReleaseDC(janela,dc);
}
LRESULT CALLBACK MenuExplorer::CapturarJanela(int codigo,WPARAM w,LPARAM l) {
    auto dono=static_cast<MenuExplorer*>(TlsGetValue(indiceThread));
    if(codigo>=0 && dono && !dono->capturando) {
        auto evento=reinterpret_cast<CWPSTRUCT*>(l);
        if(evento->message==WM_WINDOWPOSCHANGING || evento->message==WM_SHOWWINDOW || evento->message==WM_NCPAINT) {
            wchar_t classe[16];
            if(GetClassNameW(evento->hwnd,classe,16) && !lstrcmpW(classe,L"#32768")) {
                dono->capturando=true;
                // Identifica o HMENU do popup; nunca aplica o desenho a outros menus do Explorer.
                auto menu=reinterpret_cast<HMENU>(SendMessageW(evento->hwnd,0x01e1,0,0));
                bool proprio=false; for(auto candidato:dono->menus) if(candidato==menu && menu)proprio=true;
                if(proprio && !GetPropW(evento->hwnd,propriedadeMenu)) {
                    auto estado=(EstadoPopup*)HeapAlloc(GetProcessHeap(),HEAP_ZERO_MEMORY,sizeof(EstadoPopup));
                    if(estado) {
                        estado->dono=dono;dono->AddRef();
                        estado->restaurar=SUCCEEDED(DwmGetWindowAttribute(evento->hwnd,34,&estado->bordaAnterior,sizeof(COLORREF)));
                        if(!SetPropW(evento->hwnd,propriedadeMenu,reinterpret_cast<HANDLE>(estado)) ||
                            !SetWindowSubclass(evento->hwnd,ProcedimentoMenu,1,reinterpret_cast<DWORD_PTR>(estado))) {
                            RemovePropW(evento->hwnd,propriedadeMenu);HeapFree(GetProcessHeap(),0,estado);dono->Release();
                        } else {
                            if(menu==dono->menus[0])dono->popupIdiomas=evento->hwnd;
                            if(estado->restaurar) {auto cores=Cores();DwmSetWindowAttribute(evento->hwnd,34,&cores.borda,sizeof(cores.borda));}
                        }
                    }
                }
                dono->capturando=false;
            }
        }
    }
    return CallNextHookEx(nullptr,codigo,w,l);
}
LRESULT CALLBACK MenuExplorer::ProcedimentoMenu(HWND janela,UINT msg,WPARAM w,LPARAM l,UINT_PTR id,DWORD_PTR dados) {
    auto estado=reinterpret_cast<EstadoPopup*>(dados);
    auto dono=estado->dono;
    if(msg==WM_NCDESTROY || (msg==WM_SHOWWINDOW && !w)) {
        if(dono->popupIdiomas==janela)dono->popupIdiomas=nullptr;
        RemoveWindowSubclass(janela,ProcedimentoMenu,id);
        RemovePropW(janela,propriedadeMenu);
        if(estado->restaurar)DwmSetWindowAttribute(janela,34,&estado->bordaAnterior,sizeof(COLORREF));
        HeapFree(GetProcessHeap(),0,estado);
        LRESULT resultado=DefSubclassProc(janela,msg,w,l); dono->Release();return resultado;
    }
    LRESULT resultado=DefSubclassProc(janela,msg,w,l);
    if(msg!=WM_NCPAINT && msg!=WM_PAINT && msg!=WM_PRINT && msg!=WM_PRINTCLIENT && msg!=limparSetas)return resultado;
    auto menu=reinterpret_cast<HMENU>(SendMessageW(janela,0x01e1,0,0));
    bool proprio=false;for(auto candidato:dono->menus)if(candidato && candidato==menu)proprio=true;
    if(proprio && (msg==WM_PAINT || msg==limparSetas))dono->LimparSetas(janela,menu);
    else if(proprio && (msg==WM_PRINTCLIENT || (msg==WM_PRINT && (l&PRF_CLIENT))))dono->LimparSetas(janela,menu,reinterpret_cast<HDC>(w));
    if(proprio && (msg==WM_NCPAINT || msg==WM_PAINT)) dono->PintarContorno(janela);
    else if(proprio && msg==WM_PRINT && (l&PRF_NONCLIENT)) dono->PintarContorno(janela,reinterpret_cast<HDC>(w));
    return resultado;
}
HRESULT MenuExplorer::HandleMenuMsg(UINT msg,WPARAM w,LPARAM l) { LRESULT resultado=0;return HandleMenuMsg2(msg,w,l,&resultado); }
HRESULT MenuExplorer::HandleMenuMsg2(UINT msg,WPARAM w,LPARAM l,LRESULT* resultado) {
    if(resultado)*resultado=0;
    if(msg==WM_INITMENUPOPUP) {
        bool proprio=false; for(auto menu:menus)if(menu==(HMENU)w && menu)proprio=true;
        if(!proprio)return E_NOTIMPL;
        TlsSetValue(indiceThread,this);
        if(!gancho)gancho=SetWindowsHookExW(WH_CALLWNDPROC,CapturarJanela,modulo,GetCurrentThreadId());
        return S_OK;
    }
    if(msg==WM_MEASUREITEM) {
        auto item=reinterpret_cast<MEASUREITEMSTRUCT*>(l);
        if(!item || item->CtlType!=ODT_MENU || !EntradaPropria(item->itemData))return E_NOTIMPL;
        auto entrada=reinterpret_cast<EntradaMenu*>(item->itemData);
        HDC dc=GetDC(nullptr);HGDIOBJ anterior=SelectObject(dc,fonte);SIZE tamanho{};
        GetTextExtentPoint32W(dc,entrada->texto,lstrlenW(entrada->texto),&tamanho);
        SelectObject(dc,anterior);ReleaseDC(nullptr,dc);
        item->itemWidth=tamanho.cx+MulDiv(16,dpi,96);
        UINT alturaTexto=tamanho.cy+MulDiv(8,dpi,96), alturaMinima=MulDiv(24,dpi,96);
        item->itemHeight=alturaTexto>alturaMinima?alturaTexto:alturaMinima;
        if(resultado)*resultado=TRUE;return S_OK;
    }
    if(msg==WM_DRAWITEM) {
        auto desenho=reinterpret_cast<DRAWITEMSTRUCT*>(l);
        if(!desenho || desenho->CtlType!=ODT_MENU || !EntradaPropria(desenho->itemData))return E_NOTIMPL;
        PintarEntrada(desenho,reinterpret_cast<EntradaMenu*>(desenho->itemData));
        if(resultado)*resultado=TRUE;return S_OK;
    }
    if(msg==WM_MENUCHAR) {
        auto menu=(HMENU)l; bool proprio=false;for(auto candidato:menus)if(candidato==menu && menu)proprio=true;
        if(!proprio)return E_NOTIMPL;
        wchar_t letra=(wchar_t)LOWORD(w);CharUpperBuffW(&letra,1);
        int total=GetMenuItemCount(menu), selecionado=-1;
        for(int i=0;i<total;i++)if(GetMenuState(menu,i,MF_BYPOSITION)&MF_HILITE)selecionado=i;
        for(int passo=1;passo<=total;passo++) {
            int i=(selecionado+passo)%total; MENUITEMINFOW item{sizeof(item)};item.fMask=MIIM_DATA;
            if(GetMenuItemInfoW(menu,i,TRUE,&item) && EntradaPropria(item.dwItemData)) {
                wchar_t inicio=reinterpret_cast<EntradaMenu*>(item.dwItemData)->texto[0];CharUpperBuffW(&inicio,1);
                if(inicio==letra){ if(resultado)*resultado=MAKELRESULT(i,MNC_SELECT);return S_OK; }
            }
        }
        if(resultado)*resultado=MAKELRESULT(0,MNC_IGNORE);return S_OK;
    }
    return E_NOTIMPL;
}
