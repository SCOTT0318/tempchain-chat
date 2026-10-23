Unicode true
RequestExecutionLevel user
SetCompressor /SOLID lzma
SetDatablockOptimize on

!include "MUI2.nsh"
!include "LogicLib.nsh"

!ifndef APP_VERSION
  !error "APP_VERSION is required"
!endif
!ifndef PUBLISH_DIR
  !error "PUBLISH_DIR is required"
!endif
!ifndef OUTPUT_FILE
  !error "OUTPUT_FILE is required"
!endif
!ifndef PAYLOAD_UNINSTALL_INCLUDE
  !error "PAYLOAD_UNINSTALL_INCLUDE is required"
!endif
!ifndef APP_ICON
  !error "APP_ICON is required"
!endif
!ifndef ESTIMATED_SIZE_KB
  !define ESTIMATED_SIZE_KB 0
!endif

Name "TempChain Chat"
OutFile "${OUTPUT_FILE}"
InstallDir "$LOCALAPPDATA\Programs\TempChainChat"
BrandingText "TempChain Chat"
VIProductVersion "${APP_VERSION}.0"
VIAddVersionKey /LANG=1042 "ProductName" "TempChain Chat"
VIAddVersionKey /LANG=1042 "FileDescription" "TempChain Chat 설치 프로그램"
VIAddVersionKey /LANG=1042 "FileVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=1042 "ProductVersion" "${APP_VERSION}"

!define MUI_ICON "${APP_ICON}"
!define MUI_UNICON "${APP_ICON}"
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TITLE "TempChain Chat 설치"
!define MUI_WELCOMEPAGE_TEXT "현재 Windows 사용자에게 TempChain Chat을 설치합니다.$\r$\n$\r$\n실행 중인 TempChain Chat이 있다면 알림 영역에서 종료해 주세요.$\r$\n$\r$\n기존 NAS 계정으로 로그인하며, Windows 자동 실행은 앱 설정에서 직접 선택할 수 있습니다."
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\TempChain.Chat.exe"
!define MUI_FINISHPAGE_RUN_TEXT "TempChain Chat 실행"
!define MUI_FINISHPAGE_RUN_NOTCHECKED
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "Korean"

Var PayloadDeleteFailed

Function .onInit
  SetShellVarContext current
  ; No Directory page, and ignore /D overrides: only this fixed per-user path.
  StrCpy $INSTDIR "$LOCALAPPDATA\Programs\TempChainChat"
  IfFileExists "$INSTDIR\*.*" 0 install_path_ok
  IfFileExists "$INSTDIR\.tempchain-chat-install" install_path_ok 0
  MessageBox MB_OK|MB_ICONSTOP "설치 폴더에 이 설치 프로그램이 관리하지 않는 파일이 있습니다.$\r$\n$INSTDIR$\r$\n$\r$\n기존 파일을 확인한 후 다시 설치해 주세요." /SD IDOK
  SetErrorLevel 2
  Abort
install_path_ok:
FunctionEnd

Section "TempChain Chat" MainSection
  SetShellVarContext current
  StrCpy $INSTDIR "$LOCALAPPDATA\Programs\TempChainChat"
  SetOutPath "$INSTDIR"
  SetOverwrite on
  ClearErrors
  File /r "${PUBLISH_DIR}\*.*"
  IfErrors install_failed
  FileOpen $0 "$INSTDIR\.tempchain-chat-install" w
  IfErrors install_failed
  FileWrite $0 "TempChainChat managed application$\r$\n"
  FileClose $0
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  IfErrors install_failed

  CreateDirectory "$SMPROGRAMS\TempChain Chat"
  CreateShortcut "$SMPROGRAMS\TempChain Chat\TempChain Chat.lnk" "$INSTDIR\TempChain.Chat.exe" "" "$INSTDIR\TempChain.Chat.exe" 0
  CreateShortcut "$SMPROGRAMS\TempChain Chat\제거.lnk" "$INSTDIR\Uninstall.exe"
  CreateShortcut "$DESKTOP\TempChain Chat.lnk" "$INSTDIR\TempChain.Chat.exe" "" "$INSTDIR\TempChain.Chat.exe" 0

  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TempChainChat" "DisplayName" "TempChain Chat"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TempChainChat" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TempChainChat" "Publisher" "TempChain"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TempChainChat" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TempChainChat" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TempChainChat" "QuietUninstallString" '$\"$INSTDIR\Uninstall.exe$\" /S'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TempChainChat" "DisplayIcon" "$INSTDIR\TempChain.Chat.exe,0"
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TempChainChat" "EstimatedSize" ${ESTIMATED_SIZE_KB}
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TempChainChat" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TempChainChat" "NoRepair" 1
  Goto install_done
install_failed:
  MessageBox MB_OK|MB_ICONSTOP "설치를 완료하지 못했습니다. TempChain Chat을 완전히 종료하고 폴더의 쓰기 권한을 확인한 후 다시 시도해 주세요." /SD IDOK
  SetErrorLevel 2
  Abort
install_done:
SectionEnd

Function un.onInit
  SetShellVarContext current
  StrCpy $INSTDIR "$LOCALAPPDATA\Programs\TempChainChat"
  IfFileExists "$INSTDIR\.tempchain-chat-install" 0 uninstall_not_owned
  FileOpen $0 "$INSTDIR\.tempchain-chat-install" r
  IfErrors uninstall_not_owned
  FileRead $0 $1
  FileClose $0
  StrCmp $1 "TempChainChat managed application$\r$\n" uninstall_path_ok
uninstall_not_owned:
  MessageBox MB_OK|MB_ICONSTOP "이 제거 프로그램이 관리하는 설치 폴더를 확인하지 못했습니다. 파일을 삭제하지 않았습니다." /SD IDOK
  SetErrorLevel 2
  Abort
uninstall_path_ok:
FunctionEnd

Section "Uninstall"
  SetShellVarContext current
  StrCpy $INSTDIR "$LOCALAPPDATA\Programs\TempChainChat"
  StrCpy $PayloadDeleteFailed "0"
  ; Generated exact file names and empty directories only; never recurse.
  !include "${PAYLOAD_UNINSTALL_INCLUDE}"
  StrCmp $PayloadDeleteFailed "0" uninstall_files_done
  MessageBox MB_OK|MB_ICONSTOP "일부 앱 파일을 제거하지 못했습니다. 알림 영역에서 TempChain Chat을 종료한 후 제거를 다시 실행해 주세요.$\r$\n$\r$\n로그인 세션과 사용자 설정은 보존했습니다." /SD IDOK
  SetErrorLevel 2
  Quit
uninstall_files_done:
  ReadRegStr $0 HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "TempChainChat"
  StrCpy $1 '$\"$INSTDIR\TempChain.Chat.exe$\" --minimized'
  StrCmp $0 $1 0 +2
    DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "TempChainChat"

  Delete "$SMPROGRAMS\TempChain Chat\TempChain Chat.lnk"
  Delete "$SMPROGRAMS\TempChain Chat\제거.lnk"
  RMDir "$SMPROGRAMS\TempChain Chat"
  Delete "$DESKTOP\TempChain Chat.lnk"
  ReadRegStr $0 HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TempChainChat" "InstallLocation"
  StrCmp $0 $INSTDIR 0 +2
    DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\TempChainChat"
  Delete "$INSTDIR\.tempchain-chat-install"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  ; %LOCALAPPDATA%\TempChain\Chat is deliberately never referenced for deletion.
SectionEnd
