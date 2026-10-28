# TChat · Windows 앱

현재 버전은 **1.4.0**입니다. 파일 메시지에서 Windows **다른 이름으로 저장**으로 다운로드할 수 있습니다. 업로드는 파일당 10MB·최대 20개이며, 초과 파일만 제외하고 정상 파일은 계속 첨부합니다. 다운로드는 최대 512MB이며 완료 전까지 기존 파일을 보존합니다.

잠금 화면은 작은 창으로 표시하고 올바른 PIN·문자 암호를 입력하면 자동으로 열립니다. 새 암호 설정에는 확인 입력과 설정 버튼이 필요합니다. 대화 목록·말풍선·입력창은 평면적인 Windows 메신저 스타일로 정리했습니다. [1.4.0 설치 파일](https://github.com/SCOTT0318/tempchain-chat/releases/download/v1.4.0/TChat-Setup-1.4.0.exe) · [적용 안내](../docs/RELEASE-v1.4.0.md).
1.1.4부터 앱 표시 이름은 **TChat**입니다. 알림은 페이드 없이 아래에서 올라오며, 화면의 반복 안내를 줄였습니다. 기존 설치 경로·내부 실행 파일명은 업데이트 호환을 위해 유지합니다. 이전 버전용 호환 설치 파일은 `TempChainChat-Setup-1.1.4.exe`이며 TChat 설치 파일과 바이트가 같습니다.


Windows 11용 **순수 WPF 사내 메신저**입니다. 로그인·직원 목록·대화 목록·메시지 화면을 네이티브 컨트롤로 구성하고 Synology Chat API와 직접 통신합니다. 제공받은 TEMPCHAIN 원본 로고와 고해상도 아이콘을 사용하며 선택 기능인 FastAPI 서버 없이 실행할 수 있습니다.

[설치 EXE 다운로드](https://github.com/SCOTT0318/tempchain-chat/releases/download/v1.3.0/TChat-Setup-1.3.0.exe)를 이용할 수 있습니다. ZIP과 체크섬은 [최신 설치 EXE·ZIP 다운로드](https://github.com/SCOTT0318/tempchain-chat/releases/latest)에서 받습니다.

## v1.1.4 기능 및 확인 방법

v1.1.4는 Windows 빌드·격리 화면 검증 대상입니다. 현재 PC의 설치본은 요청에 따라 업데이트하지 않습니다. [검증 범위 및 남은 현장 확인](../docs/RELEASE-v1.1.4.md)을 참고하세요.

- **설정 → 바로가기 아이콘:** 바탕화면·시작 메뉴에 현재 실행 파일을 연결하는 아이콘을 만듭니다. 설정의 추가 버튼은 해당 아이콘이 없을 때만 활성화됩니다. ZIP은 폴더를 최종 위치에 옮긴 후 사용하세요. 설치 EXE에서는 두 위치의 바로가기 생성을 각각 선택할 수 있습니다.
- **설정 → 새 메시지 알림:** **3·4·5초 표시 시간**, **보낸 사람 보이기/가리기**, 미리 보기를 제공합니다. Windows의 애니메이션 비활성화 설정을 존중합니다. 메시지 본문은 알림에 노출하지 않습니다. 가리기를 선택하면 직원 이름과 방 이름도 숨깁니다.
- **다른 방 알림:** 최초 로그인 때 기존 미읽음 기록은 알리지 않습니다. 이후 다른 방·숨긴 방의 새 일반 메시지를 확인하고, 현재 보고 있는 방·내가 보낸 최신 메시지는 제외합니다. 앱이 완전히 종료됐거나 네트워크가 끊기면 알림이 오지 않습니다. 약 3초 조회 방식이며 스레드·암호화 대화 알림은 지원하지 않습니다.
- **대화 상단 → 채팅방 관리:** 비공개/공개 방 만들기, 이름·설명 입력, 직원 다중 선택·검색, 초대, 나가기, 전체 방 닫기를 제공합니다. 기본방·나에게 보내기·일대일·암호화 대화의 관리는 차단합니다. 공개방 생성에는 별도 서버 권한이 필요합니다.
- **삭제 안전장치:** ‘숨기기’는 내 목록만 정리하고 ‘나가기’는 내 참여를 끝냅니다. ‘전체 채팅방 닫기’는 다른 참여자에게도 영향을 주며 방 이름 재입력·동의 후 실행합니다. 서버의 `Channel.close`를 사용하므로 **기록의 물리적 영구 삭제를 보장하지 않습니다.** 서버 보존 정책을 따릅니다.
- 서버 `Misc.acl` 권한을 확인하지 못하면 관리 기능을 비활성화합니다. 생성·초대·나가기·폐쇄 직전에도 권한을 다시 확인합니다. 생성 후 참여/초대만 실패했다면 방 ID를 안내하며 중복 생성이나 자동 재시도를 하지 않습니다.

### 사용자가 해 줄 확인

1. Windows 11 x64에서 기존 메신저를 **알림 영역 → 종료**로 완전히 닫고 설치 EXE를 실행합니다. 코드 서명되지 않았으므로 회사 정책에 따라 검토하고 SHA-256을 확인하세요. 보안 경고를 무조건 우회하지 마세요.
2. 로그인 후 설정에서 두 바로가기, 3초·4초·5초 알림 미리 보기, 보낸 사람 보이기·가리기, 설정 저장 후 재실행을 확인합니다.
3. 직원 계정 두 개로 **테스트 전용 비공개방**을 만들고 초대·송수신을 확인합니다. 다른 방을 보거나 최소화한 상태에서 상대가 보낸 메시지의 알림과 클릭 이동을 확인합니다.
4. 테스트 방에서만 숨기기·나가기·삭제(폐쇄)를 확인합니다. **실제 업무방으로 폐쇄를 시험하지 마세요.**
5. 관리 메뉴가 회색이면 Ctrl+R로 새로 고침합니다. 공식 웹 Chat에서도 해당 직원 계정의 생성·초대 권한이 있는지 관리자가 확인해야 합니다. 기능을 위해 직원에게 DSM 관리자 권한을 부여할 필요는 없습니다. 암호·세션을 채팅으로 보내지 마세요.

### 직접 빌드

Windows 11에서 빌드하고, 설치기는 별도 폴더 추출·해시 비교로 검증합니다.

직접 빌드는 main 브랜치를 내려받은 뒤 .NET 10 SDK, PowerShell 7, 공식 NSIS 3.x가 설치된 Windows에서 실행합니다.

```powershell
dotnet run --project desktop/TempChain.Chat.Tests/TempChain.Chat.Tests.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw '테스트 실패' }
dotnet run --project desktop/TempChain.Chat.UiTests/TempChain.Chat.UiTests.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw 'WPF 화면 검증 실패' }
./desktop/scripts/Build-Desktop.ps1 -Version 1.3.0 -NsisPath 'C:\Program Files (x86)\NSIS\makensis.exe'
```

결과는 `dist/TChat-Setup-1.3.0.exe`, `dist/TChat-1.3.0-win-x64.zip`, `dist/SHA256SUMS.txt`입니다. 설치는 기존 설치를 갱신하며 사용자 설정을 보존합니다. 설정에서 현재·최신 버전을 확인하고 자동 확인·다운로드를 선택할 수 있습니다. 준비된 업데이트는 ‘업데이트 및 재시작’으로 적용합니다. 비공개 저장소에서는 공개 전까지 자동 확인이 제한됩니다.

Windows WPF 빌드와 코어 회귀 테스트 95개가 통과했습니다. 이번 버전은 격리 화면에서 알림 이동·불투명도 유지와 바로가기 이전을 검증하며 기존 설치본은 변경하지 않습니다. 실제 서버의 두 계정 생성/초대/폐쇄와 제거는 아직 현장 검증하지 않았습니다. 클라이언트 스키마는 서버의 공식 `synochat-common.js`와 대조했고 실제 방을 개발 중 임의로 변경하지 않았습니다.

Linux 교차 빌드 환경에서는 먼저 자체 포함 게시물을 만들고 아래 스크립트로 같은 NSIS 소스·체크섬을 사용할 수 있습니다. 이 방식도 Windows 실행 검증을 대신하지 않습니다.

```sh
dotnet publish desktop/TempChain.Chat/TempChain.Chat.csproj -c Release -r win-x64 --self-contained true -p:EnableWindowsTargeting=true -p:DebugType=None -p:DebugSymbols=false -o artifacts/windows-publish
python3 desktop/scripts/Package-Preview.py --publish-dir artifacts/windows-publish --makensis /path/to/makensis --nsis-dir /path/to/nsis
```

## 버전 확인과 자동 업데이트

설정에서 현재·최신 정식 버전, 확인·다운로드 상태와 자동 확인 옵션을 제공합니다. 자동 확인은 앱 시작 시와 6시간마다 수행하며, 설치는 사용자가 **업데이트 및 재시작**을 눌렀을 때 실행합니다. 메시지를 작성하거나 전송·방 관리 작업 중이면 재시작을 보류합니다. 업데이트 후 다시 로그인합니다.

GitHub의 고정 저장소 정식 릴리스만 사용하며 사전 릴리스·동일 버전·낮은 버전은 설치하지 않습니다. 설치 파일의 정확한 이름·HTTPS 주소·크기·SHA-256을 확인하고 다운로드 직후와 설치 직전에 해시를 검사합니다. 앱이 종료된 뒤 설치하며 실패 시 결과 파일을 남깁니다. 자동 설치 모드는 기존 바로가기 유무와 설정을 보존합니다. ZIP에서 실행 중인 앱은 자동 설치 대상이 아닙니다.

공개 GitHub 정식 릴리스에서 인증 없이 업데이트를 확인합니다. 앱에 GitHub 토큰이나 서버 인증값을 업데이트 요청으로 보내지 않습니다.

## 지원 범위

- 기존 ERP 계정·비밀번호로 로그인
- Chat 직원 목록과 참여 대화 조회
- 동료 선택으로 암호화하지 않은 일대일 대화 시작
- 암호화하지 않은 대화의 텍스트 기록 조회와 본인 계정으로 전송
- 선택한 대화의 표시한 메시지를 서버에 읽음 처리
- 메시지를 보존하면서 내 대화 목록에서 숨기기, 연락처에서 일대일 대화 다시 열기
- HTTP API를 3초 간격으로 조회하여 갱신
- 트레이 숨김·복원, 같은 Windows 사용자의 중복 실행 시 기존 창 활성화
- 사용자가 선택한 경우 Windows 로그인 시 자동 실행
- 회사 배포본에서 중앙 관리하는 접속 경로, 직원 화면의 서버 주소·주소 수정 기능 제거

암호화된 대화의 키 교환·복호화는 구현하지 않아 해당 대화의 사용을 차단합니다. 1.4.0은 파일 업로드·다운로드를 지원합니다. 스티커·투표·화상회의는 지원하지 않습니다.

3초 갱신은 공식 클라이언트의 Socket.IO 수신과 다른 방식입니다. 트레이에 남기는 기능만으로 새 메시지 알림 도달을 보장하지 않으며, 앱을 종료한 후 작동하는 별도 푸시 기능은 없습니다. 직원 간 실제 송수신은 일반 계정으로 검증해야 합니다.

## v1.0.0에서 확인한 내용

네이티브 자동 테스트 41개가 통과했습니다. 최신 설치본에서 사용자가 직접 로그인한 뒤 실제 서버 인증, 직원 24명 조회와 나에게 보내기 메시지 전송·재조회를 확인했습니다. 로그인·설정 화면에서 주소 항목 제거를 확인했고, 설치한 472개 파일의 SHA-256이 배포본과 일치하며 기존 사용자 설정이 보존됐습니다. 최종 설치기의 실행 중 앱 감지 검사도 통과했으며 종료 코드 2로 중단하고 앱·설정을 변경하지 않았습니다.

사외 GitHub 실행 서버에서 유효한 TLS 인증서, 앱과 같은 POST 방식의 API discovery, 필요한 API 버전 및 미인증 대화 접근 차단을 검증했습니다. [외부 연결 검사 결과](https://github.com/SCOTT0318/tempchain-chat/actions/runs/39919757052). 실제 외부 단말의 계정 인증·직원 간 송수신과 제거 검증은 별도로 남아 있습니다.

## 대화 숨기기와 읽음 처리

대화 상단의 **숨기기** 또는 목록의 **대화 목록에서 숨기기**를 누르면 확인 후 본인 목록에서만 숨깁니다. 메시지 기록과 다른 참여자의 대화는 유지하며, 메시지 삭제·채널 삭제·대화 나가기를 실행하지 않습니다. 일대일 대화는 연락처에서 다시 열 수 있습니다. **나에게 보내기**는 숨기기 대상에서 제외합니다.

앱이 화면에 보이고 활성화된 상태에서 선택한 대화의 메시지를 표시하면 해당 시점까지 서버에 읽음 처리합니다. 최소화·트레이 숨김·다른 창 뒤에 있는 동안에는 읽음으로 처리하지 않습니다. 대화를 숨길 때도 이미 표시한 메시지까지만 읽음 처리합니다.

## 실행 환경과 개발 실행

직원 PC는 Windows 11 x64가 필요합니다. 자체 포함 배포물에는 .NET 실행 환경이 포함되므로 .NET SDK와 WebView2 Runtime을 별도로 설치하지 않습니다. 개발·게시에는 .NET 10 SDK, 패키징 스크립트에는 PowerShell 7 이상을 사용합니다.

프로젝트 루트에서 실행합니다.

```powershell
dotnet restore desktop/TempChain.Chat/TempChain.Chat.csproj
dotnet run --project desktop/TempChain.Chat/TempChain.Chat.csproj
```

앱의 접속 경로는 회사 배포본의 `ServerConfiguration.cs`에서 중앙 관리합니다. 직원용 로그인·메신저·설정 화면에는 주소를 표시하거나 수정하는 기능이 없습니다. 운영자는 [서버 설정 안내](../docs/SETUP.md)의 접속 경로 검증을 마친 뒤 배포본을 확정합니다. 서버 인증서 오류를 자동으로 무시하지 않습니다.

## 인증과 설정

비밀번호는 WPF 로그인 화면에서 입력합니다. 비밀번호를 설정 파일에 저장하지 않고 SID는 앱 메모리에서만 유지하므로 앱을 완전히 종료하면 다시 로그인해야 합니다. 앱에서는 사용자 이름과 비밀번호만 입력받으며 ERP 계정 정책은 변경하지 않습니다.

설정 파일은 `%LOCALAPPDATA%\TempChain\Chat\settings.json`입니다.

| 설정 | 기본값 | 의미 |
| --- | --- | --- |
| `MinimizeToTray` | `true` | 창 닫기 시 트레이에 남김 |
| `StartWithWindows` | `false` | 선택한 경우 Windows 로그인 시 실행 |
| `AutoUpdate` | `true` | 시작 시와 6시간마다 정식 업데이트 확인·다운로드 |
| `NotificationDurationSeconds` | `5` | 팝업 표시 시간, 3·4·5초 |
| `ShowNotificationSender` | `true` | 알림에 보낸 사람과 방 이름 표시 |

사용자 설정에는 서버 주소를 저장하지 않습니다. 트레이 아이콘을 두 번 누르거나 **TempChain Chat 열기**로 복원합니다. 완전히 종료하려면 트레이의 **종료**를 사용합니다. 설치 프로그램은 자동 실행을 임의로 켜지 않습니다.

## API 근거와 호환성

일반 사용자용 내부 API를 사용하며 봇으로 직원을 가장해 전송하지 않습니다. 현재 서버 **Synology Chat 2.4.8-22204**의 공개 클라이언트 JavaScript와 API discovery에서 다음 호출을 대조했습니다.

| 기능 | 현재 클라이언트의 호출 |
| --- | --- |
| DSM 인증 | `SYNO.API.Auth.login` v6 |
| Chat 초기화 | `SYNO.Chat.User.login` v2, 빈 업무 인자 |
| 직원 목록 | `SYNO.Chat.User.list` v2 → `data.users` |
| 대화 목록 | `SYNO.Chat.Channel.list` v5 → `data.channels` |
| 일대일 시작 | `SYNO.Chat.Channel.Anonymous.initiate` v2, `user_ids`에 상대·본인 Chat 사용자 ID 포함, `encrypted=false`, `channel_key_encs=[]` |
| 텍스트 기록 | `SYNO.Chat.Post.list` v5, `channel_id`, `prev_count`, `next_count`, 선택적 `post_id` |
| 텍스트 전송 | `SYNO.Chat.Post.create` v5, `type="normal"`, `channel_id`, `message` |
| 내 목록에서 숨기기 / 다시 표시 | `SYNO.Chat.Channel.hide` / `show` v2, `channel_id` |
| 표시한 메시지 읽음 처리 | `SYNO.Chat.Channel.view` v2, `channel_id`, `last_view_at` |

현재 discovery의 Chat `requestFormat`은 `JSON`입니다. HTTP 폼의 업무 인자값을 JSON으로 인코딩하며 라우팅·세션 인증값과 구별합니다. 게시물 ID는 64비트 정수로 처리합니다.

이 API는 Synology가 외부 개발자에게 안정성을 보장한 공개 Chat 클라이언트 API가 아닙니다. DSM·Chat 업데이트 후 로그인·직원 목록·대화·송수신 회귀 확인이 필요합니다. 독립 연구 자료보다 현재 서버의 실제 클라이언트와 응답을 우선합니다. [공식 DSM 인증·discovery 안내](https://kb.synology.com/en-global/DG/DSM_Login_Web_API_Guide/2)

## 패키징

공식 NSIS 3.13 컴파일러를 준비한 뒤 프로젝트 루트의 PowerShell 7에서 실행합니다.

```powershell
./desktop/scripts/Build-Desktop.ps1
```

기본 탐지 경로는 `C:\Users\Public\Documents\ESTsoft\CreatorTemp\TempChainChat-tools\nsis-3.13\makensis.exe`이며 PATH도 확인합니다. 다른 컴파일러 위치 또는 기존 자체 포함 게시 폴더를 지정할 수 있습니다.

```powershell
./desktop/scripts/Build-Desktop.ps1 -NsisPath 'C:\도구\nsis-3.13\makensis.exe'
./desktop/scripts/Build-Desktop.ps1 -PublishDirectory 'C:\게시물\TempChainChat'
./desktop/scripts/Build-Desktop.ps1 -SkipInstaller
```

직접 게시할 때는 다음 명령을 사용합니다. 배포 시 exe 한 개가 아닌 폴더 전체를 전달합니다.

```powershell
dotnet publish desktop/TempChain.Chat/TempChain.Chat.csproj -c Release -r win-x64 --self-contained true -p:PublishTrimmed=false -p:PublishSingleFile=false -o artifacts/windows/TempChain.Chat
```

스크립트는 중간 빌드·게시물을 저장소의 `artifacts/packaging` 실행별 별도 staging에 만들고 다음 결과를 생성합니다.

```text
dist/
  TChat-1.1.4-win-x64/
  TChat-1.1.4-win-x64.zip
  TChat-Setup-1.1.4.exe
  SHA256SUMS.txt
```

복사한 파일과 최종 배포물의 SHA-256을 재검증합니다. 사용자 프로필·설정·비밀값·WebView2 DLL은 포함하지 않습니다. 기존 동일 버전 배포 폴더에 새 게시물에 없는 파일이 있으면 삭제하지 않고 중단하므로 기존 폴더를 별도 보관한 뒤 다시 빌드합니다. `-SkipInstaller`는 설치기를 생성하지 않습니다.

[NSIS 공식 다운로드](https://nsis.sourceforge.io/Download), [NSIS 라이선스](https://nsis.sourceforge.io/License), [공식 .NET 게시 안내](https://learn.microsoft.com/en-us/dotnet/core/deploying/)

## 설치와 제거

설치 프로그램은 관리자 권한 없이 현재 사용자의 `%LOCALAPPDATA%\Programs\TempChainChat` 고정 경로에 설치하고 시작 메뉴·바탕화면 바로 가기를 만듭니다. 실행 중인 개발용·ZIP·설치 앱이 있으면 알림 영역에서 종료한 뒤 다시 시도하도록 안내합니다. 무인 설치는 실행 중인 앱이 있으면 종료 코드 2로 중단하고 자동 실행하지 않습니다. 완료 화면에서 앱 실행을 선택할 수 있습니다.

현재 앱과 설치기는 Authenticode 코드 서명되지 않았습니다. 최종 배포물의 파일명과 SHA-256은 릴리스에 함께 제공할 `SHA256SUMS.txt`로 확인합니다.

Windows 설정의 설치된 앱 또는 시작 메뉴의 제거를 사용합니다. 빌드 때 기록한 앱 파일만 삭제하고 폴더는 비어 있을 때만 제거합니다. `%LOCALAPPDATA%\TempChain\Chat`의 사용자 데이터는 전부 보존합니다. 자동 실행 등록값은 현재 설치 앱의 정확한 명령과 같을 때만 삭제합니다. 제거 전 트레이에서 앱을 종료하세요.

## 직원 배포 전 확인

- [ ] 게시 exe 및 설치·제거를 Windows 11에서 직접 확인
- [x] 네이티브 앱에서 실제 서버 인증 및 직원 24명 조회 확인
- [ ] 일반 직원 계정 로그인 확인
- [ ] 직원 두 명의 일대일 생성, 메시지 송수신·기록 재조회 확인
- [ ] 대화 숨기기·연락처에서 다시 열기 및 메시지 보존 확인
- [ ] 표시한 대화의 읽음 처리와 최소화·트레이 상태의 미읽음 보존 확인
- [ ] 암호화된 대화 차단·미지원 기능 안내 확인
- [ ] 트레이 숨김·복원·완전 종료 및 중복 실행 활성화 확인
- [ ] 시작 프로그램 옵션 켜기·끄기 확인
- [ ] 서버 단절·세션 만료·인증서 오류 안내 확인
- [ ] 제거 후 사용자 데이터 및 기존 ERP 보존 확인

소스 구현·자동 테스트·게시 성공·실제 서버 송수신은 따로 검증합니다. 직원 계정으로 확인하지 않은 항목을 운영 완료로 표시하지 않습니다.
