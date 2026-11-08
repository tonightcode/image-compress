Unicode true
Name "letsmall"
!ifndef APP_VERSION
!error "Build using scripts/build-release.ps1 to supply APP_VERSION from version.props"
!endif
VIProductVersion "${APP_VERSION}.0"
VIAddVersionKey /LANG=1033 "ProductName" "letsmall"
VIAddVersionKey /LANG=1033 "ProductVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=1033 "FileVersion" "${APP_VERSION}.0"
VIAddVersionKey /LANG=1033 "FileDescription" "letsmall installer"
VIAddVersionKey /LANG=1033 "LegalCopyright" "letsmall"
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
!include "FileFunc.nsh"
!define MUI_ICON "..\src\LetSmall\Assets\letsmall.ico"
!define MUI_UNICON "..\src\LetSmall\Assets\letsmall.ico"
Var UpdatePid
Var InstallMutex
Var RestartAfterUpdate

!define MUI_PAGE_CUSTOMFUNCTION_PRE SkipUpdateDirectory
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

Function SkipUpdateDirectory
  ${If} $RestartAfterUpdate == "1"
    Abort
  ${EndIf}
FunctionEnd

Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "letsmall requires 64-bit Windows." /SD IDOK
    SetErrorLevel 1
    Abort
  ${EndIf}
  StrCpy $RestartAfterUpdate "0"
  ${GetParameters} $0
  ClearErrors
  ${GetOptions} $0 "/UPDATEPID=" $UpdatePid
  ${IfNot} ${Errors}
    IntFmt $1 "%u" $UpdatePid
    ${If} $1 != $UpdatePid
    ${OrIf} $UpdatePid == "0"
      MessageBox MB_ICONSTOP "Invalid update process." /SD IDOK
      SetErrorLevel 1
      Abort
    ${EndIf}
    ReadRegStr $1 HKCU "Software\letsmall" "InstallDir"
    ${If} $1 == ""
    ${OrIf} $1 != $INSTDIR
      MessageBox MB_ICONSTOP "The update must use the existing installation directory. Please reinstall from the website." /SD IDOK
      SetErrorLevel 1
      Abort
    ${EndIf}
    StrCpy $RestartAfterUpdate "1"
  ${EndIf}
FunctionEnd

Function .onGUIEnd
  ${If} $InstallMutex != ""
    System::Call 'kernel32::CloseHandle(p $InstallMutex)'
  ${EndIf}
FunctionEnd

Section "Install"
  System::Call 'kernel32::CreateMutexW(p 0, i 0, w "Local\letsmall-installing") p .r0 ?e'
  Pop $1
  StrCpy $InstallMutex $0
  ${If} $0 == 0
  ${OrIf} $1 == 183
    MessageBox MB_ICONSTOP "Another installation is running. Please try again later." /SD IDOK
    SetErrorLevel 1
    Abort
  ${EndIf}
  StrCpy $2 0
  wait_for_app:
    System::Call 'kernel32::OpenMutexW(i 0x100000, i 0, w "Local\letsmall-running") p .r0'
    ${If} $0 != 0
      System::Call 'kernel32::CloseHandle(p r0)'
      ${If} $2 >= 30
        MessageBox MB_ICONSTOP "Close all letsmall windows and run the installer again. No files were replaced." /SD IDOK
        SetErrorLevel 1
        Abort
      ${EndIf}
      Sleep 1000
      IntOp $2 $2 + 1
      Goto wait_for_app
    ${EndIf}
  SetOverwrite on
  ClearErrors
  SetOutPath "$INSTDIR"
  File "${APP_BUILD_DIR}\letsmall.exe"
!ifndef MEASURE_BASE
  File "${APP_BUILD_DIR}\libwebp.dll"
  File "${APP_BUILD_DIR}\libsharpyuv.dll"
!endif
  ${If} ${Errors}
    MessageBox MB_ICONSTOP "Installation failed. Please run the full installer again to repair the application." /SD IDOK
    SetErrorLevel 1
    Abort
  ${EndIf}
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  ${If} ${Errors}
    MessageBox MB_ICONSTOP "Installation failed. Could not create the uninstaller." /SD IDOK
    SetErrorLevel 1
    Abort
  ${EndIf}

  CreateDirectory "$SMPROGRAMS\letsmall"
  CreateShortcut "$SMPROGRAMS\letsmall\letsmall.lnk" "$INSTDIR\letsmall.exe"
  WriteRegStr HKCU "Software\letsmall" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\letsmall" "DisplayName" "letsmall"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\letsmall" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\letsmall" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\letsmall" "DisplayIcon" "$INSTDIR\letsmall.exe"
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\letsmall" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\letsmall" "NoRepair" 1

  WriteRegStr HKCU "Software\Classes\letsmall" "" "URL:letsmall Activation"
  WriteRegStr HKCU "Software\Classes\letsmall" "URL Protocol" ""
  WriteRegStr HKCU "Software\Classes\letsmall\shell\open\command" "" '"$INSTDIR\letsmall.exe" "%1"'
  ${If} ${Errors}
    MessageBox MB_ICONSTOP "Installation could not be completed. Please run the full installer again." /SD IDOK
    SetErrorLevel 1
    Abort
  ${EndIf}
  System::Call 'kernel32::CloseHandle(p $InstallMutex)'
  StrCpy $InstallMutex ""
  ${If} $RestartAfterUpdate == "1"
    Exec '"$INSTDIR\letsmall.exe"'
  ${EndIf}
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
