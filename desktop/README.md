# TempChain Chat · Windows 앱

Windows 11용 **순수 WPF 사내 메신저**입니다. 로그인·직원 목록·대화 목록·메시지 화면을 네이티브 컨트롤로 구성하고 Synology Chat API와 직접 통신합니다. 제공받은 TEMPCHAIN 원본 로고와 고해상도 아이콘을 사용하며 선택 기능인 FastAPI 서버 없이 실행할 수 있습니다.

GitHub 로그인 없이 [설치 EXE 직접 다운로드](https://github.com/SCOTT0318/tempchain-chat/releases/download/v1.0.0/TempChainChat-Setup-1.0.0.exe)를 이용할 수 있습니다. ZIP과 체크섬은 [최신 설치 EXE·ZIP 다운로드](https://github.com/SCOTT0318/tempchain-chat/releases/latest)에서 받습니다.

## 지원 범위

- 기존 NAS 계정·비밀번호 및 필요한 OTP로 로그인
- Chat 직원 목록과 참여 대화 조회
- 동료 선택으로 암호화하지 않은 일대일 대화 시작
- 암호화하지 않은 대화의 텍스트 기록 조회와 본인 계정으로 전송
- 선택한 대화의 표시한 메시지를 NAS에 읽음 처리
- 메시지를 보존하면서 내 대화 목록에서 숨기기, 연락처에서 일대일 대화 다시 열기
- HTTP API를 3초 간격으로 조회하여 갱신
- 트레이 숨김·복원, 같은 Windows 사용자의 중복 실행 시 기존 창 활성화
- 사용자가 선택한 경우 Windows 로그인 시 자동 실행
- 회사 배포본에서 중앙 관리하는 접속 경로, 직원 화면의 서버 주소·주소 수정 기능 제거

암호화된 대화의 키 교환·복호화는 구현하지 않아 해당 대화의 사용을 차단합니다. 파일 업로드·다운로드, 스티커·투표·화상회의는 이 버전의 지원 기능에 포함하지 않습니다.

3초 갱신은 공식 클라이언트의 Socket.IO 수신과 다른 방식입니다. 트레이에 남기는 기능만으로 새 메시지 알림 도달을 보장하지 않으며, 앱을 종료한 후 작동하는 별도 푸시 기능은 없습니다. 직원 간 실제 송수신은 일반 계정으로 검증해야 합니다.

## 확인한 내용

네이티브 자동 테스트 41개가 통과했습니다. 최신 설치본에서 사용자가 직접 로그인한 뒤 실제 NAS 인증, 직원 24명 조회와 나에게 보내기 메시지 전송·재조회를 확인했습니다. 로그인·설정 화면에서 주소 항목 제거를 확인했고, 설치한 472개 파일의 SHA-256이 배포본과 일치하며 기존 사용자 설정이 보존됐습니다. 최종 설치기의 실행 중 앱 감지 검사도 통과했으며 종료 코드 2로 중단하고 앱·설정을 변경하지 않았습니다.

사외 GitHub 실행 서버에서 유효한 TLS 인증서, 앱과 같은 POST 방식의 API discovery, 필요한 API 버전 및 미인증 대화 접근 차단을 검증했습니다. [외부 연결 검사 결과](https://github.com/SCOTT0318/tempchain-chat/actions/runs/39919757052). 실제 외부 단말의 계정 인증·직원 간 송수신과 제거 검증은 별도로 남아 있습니다.

## 대화 숨기기와 읽음 처리

대화 상단의 **숨기기** 또는 목록의 **대화 목록에서 숨기기**를 누르면 확인 후 본인 목록에서만 숨깁니다. 메시지 기록과 다른 참여자의 대화는 유지하며, 메시지 삭제·채널 삭제·대화 나가기를 실행하지 않습니다. 일대일 대화는 연락처에서 다시 열 수 있습니다. **나에게 보내기**는 숨기기 대상에서 제외합니다.

앱이 화면에 보이고 활성화된 상태에서 선택한 대화의 메시지를 표시하면 해당 시점까지 NAS에 읽음 처리합니다. 최소화·트레이 숨김·다른 창 뒤에 있는 동안에는 읽음으로 처리하지 않습니다. 대화를 숨길 때도 이미 표시한 메시지까지만 읽음 처리합니다.

## 실행 환경과 개발 실행

직원 PC는 Windows 11 x64가 필요합니다. 자체 포함 배포물에는 .NET 실행 환경이 포함되므로 .NET SDK와 WebView2 Runtime을 별도로 설치하지 않습니다. 개발·게시에는 .NET 10 SDK, 패키징 스크립트에는 PowerShell 7 이상을 사용합니다.

프로젝트 루트에서 실행합니다.

```powershell
dotnet restore desktop/TempChain.Chat/TempChain.Chat.csproj
dotnet run --project desktop/TempChain.Chat/TempChain.Chat.csproj
```

앱의 접속 경로는 회사 배포본의 `ServerConfiguration.cs`에서 중앙 관리합니다. 직원용 로그인·메신저·설정 화면에는 주소를 표시하거나 수정하는 기능이 없습니다. 운영자는 [NAS 설정 안내](../docs/SETUP.md)의 접속 경로 검증을 마친 뒤 배포본을 확정합니다. 서버 인증서 오류를 자동으로 무시하지 않습니다.

## 인증과 설정

비밀번호는 WPF 로그인 화면에서 입력합니다. 비밀번호를 설정 파일에 저장하지 않고 SID는 앱 메모리에서만 유지하므로 앱을 완전히 종료하면 다시 로그인해야 합니다. NAS MFA 정책을 그대로 따릅니다.

설정 파일은 `%LOCALAPPDATA%\TempChain\Chat\settings.json`입니다.

| 설정 | 기본값 | 의미 |
| --- | --- | --- |
| `MinimizeToTray` | `true` | 창 닫기 시 트레이에 남김 |
| `StartWithWindows` | `false` | 선택한 경우 Windows 로그인 시 실행 |

사용자 설정에는 서버 주소를 저장하지 않습니다. 트레이 아이콘을 두 번 누르거나 **TempChain Chat 열기**로 복원합니다. 완전히 종료하려면 트레이의 **종료**를 사용합니다. 설치 프로그램은 자동 실행을 임의로 켜지 않습니다.

## API 근거와 호환성

일반 사용자용 내부 API를 사용하며 봇으로 직원을 가장해 전송하지 않습니다. 현재 NAS **Synology Chat 2.4.8-22204**의 공개 클라이언트 JavaScript와 API discovery에서 다음 호출을 대조했습니다.

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

이 API는 Synology가 외부 개발자에게 안정성을 보장한 공개 Chat 클라이언트 API가 아닙니다. DSM·Chat 업데이트 후 로그인·직원 목록·대화·송수신 회귀 확인이 필요합니다. 독립 연구 자료보다 현재 NAS의 실제 클라이언트와 응답을 우선합니다. [공식 DSM 인증·discovery 안내](https://kb.synology.com/en-global/DG/DSM_Login_Web_API_Guide/2)

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

스크립트는 중간 빌드·게시물을 Public CreatorTemp의 실행별 별도 staging에 만들고 다음 결과를 생성합니다.

```text
dist/
  TempChainChat-1.0.0-win-x64/
  TempChainChat-1.0.0-win-x64.zip
  TempChainChat-Setup-1.0.0.exe
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
- [x] 네이티브 앱에서 실제 NAS 인증 및 직원 24명 조회 확인
- [ ] 일반 직원 계정 로그인·OTP 확인
- [ ] 직원 두 명의 일대일 생성, 메시지 송수신·기록 재조회 확인
- [ ] 대화 숨기기·연락처에서 다시 열기 및 메시지 보존 확인
- [ ] 표시한 대화의 읽음 처리와 최소화·트레이 상태의 미읽음 보존 확인
- [ ] 암호화된 대화 차단·미지원 기능 안내 확인
- [ ] 트레이 숨김·복원·완전 종료 및 중복 실행 활성화 확인
- [ ] 시작 프로그램 옵션 켜기·끄기 확인
- [ ] NAS 단절·세션 만료·인증서 오류 안내 확인
- [ ] 제거 후 사용자 데이터 및 기존 ERP 보존 확인

소스 구현·자동 테스트·게시 성공·실제 NAS 송수신은 따로 검증합니다. 직원 계정으로 확인하지 않은 항목을 운영 완료로 표시하지 않습니다.
