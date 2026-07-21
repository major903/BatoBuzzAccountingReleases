# Windows 7 SP1 Legacy Edition

This separate desktop build is for Windows 7 SP1 only. It uses .NET Framework
4.8 and is not a replacement for the supported Windows 10/11 product.

## Customer prerequisites

- Windows 7 Service Pack 1, 32-bit or 64-bit.
- .NET Framework 4.8 installed before BatoBuzz.
- A fully patched PC with local administrator access for installation.

The legacy edition uses a native text report viewer and PDF/Excel export rather
than WebView2. In-app updating is deliberately disabled: distribute only the
separate Windows 7 Legacy installer to these machines.

## Build and package

```powershell
.\scripts\build-desktop-win7.ps1 -Configuration Release
```

The output is placed in `publish-win7`. Compile
`installer\BatoBuzzAccountingWin7.iss` with Inno Setup to create the separate
installer. Test it in a clean Windows 7 SP1 virtual machine before release.

Do not replace the regular installer or its Windows 10/11 requirements.
