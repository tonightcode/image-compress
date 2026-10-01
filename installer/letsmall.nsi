Unicode true
Name "letsmall"
!ifndef APP_BUILD_DIR
!define APP_BUILD_DIR "..\src\LetSmall\bin\Release"
!endif
!ifdef OUTPUT_FILE
OutFile "${OUTPUT_FILE}"
!else
!ifdef MEASURE_BASE
OutFile "..\artifacts\letsmall-base-measure.exe"
!else
OutFile "..\artifacts\letsmall-setup.exe"
!endif
!endif
InstallDir "$LOCALAPPDATA\Programs\letsmall"
InstallDirRegKey HKCU "Software\letsmall" "InstallDir"
RequestExecutionLevel user
ShowInstDetails nevershow
ShowUninstDetails nevershow

!include "MUI2.nsh"
!include "x64.nsh"

!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "letsmall requires 64-bit Windows."
    Abort
  ${EndIf}
FunctionEnd

Section "Install"
  SetOutPath "$INSTDIR"
  File "${APP_BUILD_DIR}\letsmall.exe"
!ifndef MEASURE_BASE
  File "${APP_BUILD_DIR}\libwebp.dll"
  File "${APP_BUILD_DIR}\libsharpyuv.dll"
!endif
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  CreateDirectory "$SMPROGRAMS\letsmall"
  CreateShortcut "$SMPROGRAMS\letsmall\letsmall.lnk" "$INSTDIR\letsmall.exe"
  WriteRegStr HKCU "Software\letsmall" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\letsmall" "DisplayName" "letsmall"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\letsmall" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\letsmall" "DisplayIcon" "$INSTDIR\letsmall.exe"
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\letsmall" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\letsmall" "NoRepair" 1

  WriteRegStr HKCU "Software\Classes\letsmall" "" "URL:letsmall Activation"
  WriteRegStr HKCU "Software\Classes\letsmall" "URL Protocol" ""
  WriteRegStr HKCU "Software\Classes\letsmall\shell\open\command" "" '"$INSTDIR\letsmall.exe" "%1"'
SectionEnd

Section "Uninstall"
  Delete "$INSTDIR\letsmall.exe"
  Delete "$INSTDIR\libwebp.dll"
  Delete "$INSTDIR\libsharpyuv.dll"
  Delete "$INSTDIR\Uninstall.exe"
  Delete "$SMPROGRAMS\letsmall\letsmall.lnk"
  RMDir "$SMPROGRAMS\letsmall"
  RMDir "$INSTDIR"
  DeleteRegKey HKCU "Software\Classes\letsmall"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\letsmall"
  DeleteRegKey HKCU "Software\letsmall"
  ; %LOCALAPPDATA%\letsmall contains the license and must survive uninstall.
SectionEnd
