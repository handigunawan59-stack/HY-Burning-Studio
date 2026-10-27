Unicode True
!include "MUI2.nsh"

!ifndef PUBLISH_DIR
  !error "PUBLISH_DIR must point to the self-contained application publish directory."
!endif
!ifndef OUTPUT_FILE
  !error "OUTPUT_FILE must point to the installer output file."
!endif
!ifndef UNINSTALL_INCLUDE
  !error "UNINSTALL_INCLUDE must point to the generated file list."
!endif

Name "HY Burning Studio"
OutFile "${OUTPUT_FILE}"
InstallDir "$LOCALAPPDATA\Programs\HY Burning Studio"
InstallDirRegKey HKCU "Software\HY Burning Studio" "InstallDir"
RequestExecutionLevel user
SetCompressor /SOLID zlib

!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\HYBurningStudio.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Jalankan HY Burning Studio"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "Indonesian"

Section "HY Burning Studio" MainSection
  SetShellVarContext current
  SetOutPath "$INSTDIR"
  File /r "${PUBLISH_DIR}\*"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateDirectory "$SMPROGRAMS\HY Burning Studio"
  CreateShortCut "$SMPROGRAMS\HY Burning Studio\HY Burning Studio.lnk" "$INSTDIR\HYBurningStudio.exe"
  CreateShortCut "$SMPROGRAMS\HY Burning Studio\Hapus HY Burning Studio.lnk" "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "Software\HY Burning Studio" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\HYBurningStudio" "DisplayName" "HY Burning Studio"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\HYBurningStudio" "DisplayVersion" "1.0.0"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\HYBurningStudio" "Publisher" "HY Burning Studio"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\HYBurningStudio" "DisplayIcon" "$INSTDIR\HYBurningStudio.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\HYBurningStudio" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\HYBurningStudio" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\HYBurningStudio" "NoRepair" 1
SectionEnd

Section "Uninstall"
  SetShellVarContext current
  !include "${UNINSTALL_INCLUDE}"
  Delete "$INSTDIR\Uninstall.exe"
  Delete "$SMPROGRAMS\HY Burning Studio\HY Burning Studio.lnk"
  Delete "$SMPROGRAMS\HY Burning Studio\Hapus HY Burning Studio.lnk"
  RMDir "$SMPROGRAMS\HY Burning Studio"
  RMDir "$INSTDIR"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\HYBurningStudio"
  DeleteRegKey HKCU "Software\HY Burning Studio"
SectionEnd
