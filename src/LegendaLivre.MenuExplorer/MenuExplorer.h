#pragma once
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shlobj.h>
#include <shellapi.h>
#include <commctrl.h>
#include <oleacc.h>
#include <strsafe.h>
#include <new>

extern HINSTANCE modulo;
extern LONG objetos;
extern DWORD indiceThread;
extern const CLSID clsidMenu;

struct EntradaMenu {
    MSAAMENUINFO acessibilidade{};
    const wchar_t* texto = nullptr;
    bool idioma = false;
};

class MenuExplorer final : public IShellExtInit, public IContextMenu3 {
    LONG referencias = 1;
    HGLOBAL arquivos = nullptr;
    HBITMAP icone = nullptr;
    HFONT fonte = nullptr;
    HHOOK gancho = nullptr;
    HWND popupIdiomas = nullptr;
    UINT dpi = 96;
    bool anexado = false;
    bool capturando = false;
    EntradaMenu entradas[9]{};
    HMENU menus[4]{};
    void PrepararFonte();
    void PrepararEntrada(int indice, const wchar_t* texto, bool idioma);
    bool InserirEntrada(HMENU menu, UINT comando, int indice, HMENU filho);
    void PintarEntrada(DRAWITEMSTRUCT* desenho, EntradaMenu* entrada);
    bool EntradaPropria(ULONG_PTR valor);
    void PintarContorno(HWND janela, HDC destino = nullptr);
    void LimparSetas(HWND janela, HMENU menu, HDC destino = nullptr);
    static LRESULT CALLBACK CapturarJanela(int codigo, WPARAM w, LPARAM l);
    static LRESULT CALLBACK ProcedimentoMenu(HWND janela, UINT msg, WPARAM w, LPARAM l, UINT_PTR id, DWORD_PTR dados);
public:
    MenuExplorer();
    ~MenuExplorer();
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid, void** objeto) override;
    ULONG STDMETHODCALLTYPE AddRef() override;
    ULONG STDMETHODCALLTYPE Release() override;
    HRESULT STDMETHODCALLTYPE Initialize(PCIDLIST_ABSOLUTE, IDataObject*, HKEY) override;
    HRESULT STDMETHODCALLTYPE QueryContextMenu(HMENU, UINT, UINT, UINT, UINT) override;
    HRESULT STDMETHODCALLTYPE InvokeCommand(CMINVOKECOMMANDINFO*) override;
    HRESULT STDMETHODCALLTYPE GetCommandString(UINT_PTR, UINT, UINT*, LPSTR, UINT) override;
    HRESULT STDMETHODCALLTYPE HandleMenuMsg(UINT, WPARAM, LPARAM) override;
    HRESULT STDMETHODCALLTYPE HandleMenuMsg2(UINT, WPARAM, LPARAM, LRESULT*) override;
};
