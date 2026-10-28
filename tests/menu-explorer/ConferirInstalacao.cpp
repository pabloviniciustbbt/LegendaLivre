#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shlobj.h>
#include <stdio.h>

int wmain(int argc,wchar_t** argv) {
    if(argc!=2)return 2;
    CoInitializeEx(nullptr,COINIT_APARTMENTTHREADED);
    PIDLIST_ABSOLUTE pidl=nullptr;
    HRESULT h=SHParseDisplayName(argv[1],nullptr,&pidl,0,nullptr);
    if(FAILED(h)){printf("SHParseDisplayName: %08lx\n",h);return 3;}
    IShellFolder* pasta=nullptr;PCUITEMID_CHILD filho=nullptr;
    h=SHBindToParent(pidl,IID_IShellFolder,(void**)&pasta,&filho);
    IContextMenu* contexto=nullptr;
    if(SUCCEEDED(h))h=pasta->GetUIObjectOf(nullptr,1,&filho,IID_IContextMenu,nullptr,(void**)&contexto);
    if(FAILED(h)){printf("GetUIObjectOf: %08lx\n",h);CoTaskMemFree(pidl);if(pasta)pasta->Release();return 4;}
    HMENU menu=CreatePopupMenu();
    h=contexto->QueryContextMenu(menu,0,1,0x7fff,CMF_NORMAL);
    int encontradas=0;bool correto=SUCCEEDED(h);
    for(int i=0;i<GetMenuItemCount(menu);i++) {
        wchar_t nome[256];MENUITEMINFOW item{sizeof(item)};
        item.fMask=MIIM_STRING|MIIM_SUBMENU|MIIM_BITMAP;item.dwTypeData=nome;item.cch=256;
        if(!GetMenuItemInfoW(menu,i,TRUE,&item) || lstrcmpW(nome,L"LegendaLivre"))continue;
        encontradas++;
        correto=correto && item.hbmpItem && item.hSubMenu && GetMenuItemCount(item.hSubMenu)==3;
        for(int idioma=0;idioma<3;idioma++) {
            HMENU acoes=GetSubMenu(item.hSubMenu,idioma);
            MENUINFO estilo{sizeof(estilo)};estilo.fMask=MIM_STYLE;
            correto=correto && acoes && GetMenuItemCount(acoes)==2 && GetMenuInfo(acoes,&estilo) && (estilo.dwStyle&MNS_NOCHECK);
        }
    }
    correto=correto && encontradas==1;
    printf("Menu montado pelo Shell: LegendaLivre=%d; hierarquia/icone/margem=%s\n",encontradas,correto?"APROVADO":"FALHOU");
    DestroyMenu(menu);contexto->Release();pasta->Release();CoTaskMemFree(pidl);CoUninitialize();
    return correto?0:1;
}
