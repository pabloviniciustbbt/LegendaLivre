#pragma once
#include "../../src/LegendaLivre.MenuExplorer/MenuExplorer.h"
class DadosTeste : public IDataObject {
    LONG referencias=1;
    HGLOBAL conteudo;
public:
    DadosTeste(const void* dados,SIZE_T tamanho) {
        conteudo=GlobalAlloc(GHND,tamanho);void* destino=GlobalLock(conteudo);
        CopyMemory(destino,dados,tamanho);GlobalUnlock(conteudo);
    }
    DadosTeste(const wchar_t* video,const wchar_t* segundo=nullptr) {
        SIZE_T caracteres=lstrlenW(video)+2+(segundo?lstrlenW(segundo)+1:0);
        SIZE_T tamanho=sizeof(DROPFILES)+caracteres*sizeof(wchar_t);
        conteudo=GlobalAlloc(GHND,tamanho);
        auto dados=(DROPFILES*)GlobalLock(conteudo);dados->pFiles=sizeof(DROPFILES);dados->fWide=TRUE;
        auto nomes=(wchar_t*)((BYTE*)dados+sizeof(DROPFILES));
        StringCchCopyW(nomes,caracteres,video);
        if(segundo)StringCchCopyW(nomes+lstrlenW(video)+1,caracteres-lstrlenW(video)-1,segundo);
        GlobalUnlock(conteudo);
    }
    ~DadosTeste(){GlobalFree(conteudo);}
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid,void** p) override { if(!p)return E_POINTER; *p=this;AddRef();return S_OK; }
    ULONG STDMETHODCALLTYPE AddRef() override{return InterlockedIncrement(&referencias);}
    ULONG STDMETHODCALLTYPE Release() override{return InterlockedDecrement(&referencias);}
    HRESULT STDMETHODCALLTYPE GetData(FORMATETC* f,STGMEDIUM* s) override {
        if(!f || !s || f->cfFormat!=CF_HDROP)return DV_E_FORMATETC;
        SIZE_T tamanho=GlobalSize(conteudo);s->tymed=TYMED_HGLOBAL;s->pUnkForRelease=nullptr;s->hGlobal=GlobalAlloc(GHND,tamanho);
        void* a=GlobalLock(conteudo),*b=GlobalLock(s->hGlobal);CopyMemory(b,a,tamanho);GlobalUnlock(s->hGlobal);GlobalUnlock(conteudo);return S_OK;
    }
    HRESULT STDMETHODCALLTYPE GetDataHere(FORMATETC*,STGMEDIUM*) override{return E_NOTIMPL;}
    HRESULT STDMETHODCALLTYPE QueryGetData(FORMATETC* f) override{return f && f->cfFormat==CF_HDROP?S_OK:DV_E_FORMATETC;}
    HRESULT STDMETHODCALLTYPE GetCanonicalFormatEtc(FORMATETC*,FORMATETC*) override{return E_NOTIMPL;}
    HRESULT STDMETHODCALLTYPE SetData(FORMATETC*,STGMEDIUM*,BOOL) override{return E_NOTIMPL;}
    HRESULT STDMETHODCALLTYPE EnumFormatEtc(DWORD,IEnumFORMATETC**) override{return E_NOTIMPL;}
    HRESULT STDMETHODCALLTYPE DAdvise(FORMATETC*,DWORD,IAdviseSink*,DWORD*) override{return OLE_E_ADVISENOTSUPPORTED;}
    HRESULT STDMETHODCALLTYPE DUnadvise(DWORD) override{return OLE_E_ADVISENOTSUPPORTED;}
    HRESULT STDMETHODCALLTYPE EnumDAdvise(IEnumSTATDATA**) override{return OLE_E_ADVISENOTSUPPORTED;}
};

