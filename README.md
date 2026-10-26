# TEMPCHAIN Chat for Windows

템프체인 사내 전용 Windows 메신저입니다. 로그인, 사용자·대화 목록, 메시지 화면과 입력창을 WPF 컨트롤로 직접 구현했습니다. 제공받은 TEMPCHAIN 원본 로고와 고해상도 아이콘을 적용했습니다.

## v1.1.2 정식 릴리스

바탕화면·시작 메뉴 바로가기 생성, 3·4·5초 알림 시간·보낸 사람 표시 설정, 다른 방 새 메시지 알림, 비공개/공개 채팅방 생성·직원 초대·나가기·전체 폐쇄를 추가했습니다. 삭제(폐쇄)는 방 이름 재입력·동의와 NAS 권한 검사를 거치며 영구 기록 삭제를 보장하지 않습니다.

[기능·Windows 확인 절차·GitHub 권한 안내](desktop/README.md#v112-기능-및-확인-방법) · [소스 변경 PR #2](https://github.com/SCOTT0318/tempchain-chat/pull/2)

Windows 설치 업데이트와 사용자 파일 보존을 확인했습니다. 상세 검증 및 남은 현장 확인 항목은 [v1.1.2 릴리스 노트](docs/RELEASE-v1.1.2.md)에 있습니다. 파일 전송·스레드·암호화 대화 등은 아직 지원하지 않습니다.

## 버전 확인과 자동 업데이트

설정의 **버전 및 업데이트**에서 현재·최신 버전과 다운로드 상태를 확인합니다. 자동 확인은 기본 켜짐이며 앱 시작 시와 6시간마다 동작합니다. **업데이트 및 재시작**을 누르면 검증된 설치기를 실행하고 앱을 다시 엽니다. 작성 중인 메시지나 진행 중인 작업이 있으면 먼저 마무리해야 합니다.

업데이트는 이 저장소의 공개 정식 릴리스를 사용합니다. 비공개 상태에서는 직원 앱의 무인 조회·다운로드가 실패하며, GitHub 로그인 정보나 토큰을 앱에 넣지 않습니다. 공개 전환 뒤 사용할 수 있습니다.

## 실행

저장소 접근 권한이 있는 GitHub 계정으로 로그인한 뒤 [설치 EXE 직접 다운로드](https://github.com/SCOTT0318/tempchain-chat/releases/download/v1.1.2/TempChainChat-Setup-1.1.2.exe)를 이용할 수 있습니다. ZIP과 체크섬은 [최신 Windows 앱 다운로드](https://github.com/SCOTT0318/tempchain-chat/releases/latest)에서 받습니다.

- **설치 EXE:** `TempChainChat-Setup-1.1.2.exe`를 실행합니다. 현재 Windows 사용자용 프로그램과 바로가기를 만듭니다.
- **ZIP:** `TempChainChat-1.1.2-win-x64.zip` 전체를 압축 해제한 뒤 `TempChain.Chat.exe`를 실행합니다.

앱의 로그인 화면에서 기존 NAS 사용자 이름·비밀번호를 입력합니다. 직원이 서버 주소를 입력하거나 변경할 필요가 없으며, 로그인·메신저·설정 화면에 주소를 표시하지 않습니다. 접속 경로는 회사 배포본에서 중앙 관리합니다.

Windows 11 x64를 대상으로 하며 배포본에 .NET 실행 환경을 포함합니다. 현재 설치기는 코드 서명되지 않았습니다.

## 네이티브 기능

- 사용자 이름·비밀번호로 로그인
- 기존 대화·채널과 로컬 사용자 목록 조회
- 일대일 대화 시작, 텍스트 메시지 조회·전송
- 대화 내용 주기적 갱신, 동료·대화 검색
- 표시한 대화의 읽음 처리, 메시지를 보존하는 내 대화 목록 숨기기·다시 열기
- 시스템 트레이, 창 닫기 동작 설정, Windows 자동 실행 선택
- 중복 실행 시 기존 창 복원

숨기기는 본인 목록에만 적용하며 메시지를 삭제하거나 다른 참여자의 대화를 제거하지 않습니다. 첫 버전은 텍스트 대화에 집중합니다. 암호화된 대화의 해독·전송, 파일 업로드·다운로드, 스레드·투표 및 Socket.IO 기반 실시간 이벤트는 지원 범위에 포함하지 않습니다. 새 메시지는 3초 간격 조회로 반영합니다.

## v1.0.0에서 확인한 내용

네이티브 자동 테스트 41개와 선택 연동 테스트 45개가 통과했습니다. 최신 설치본에서 실제 NAS 로그인, 직원 24명 조회와 나에게 보내기 메시지 전송·재조회를 확인했습니다. 사외 GitHub 실행 서버의 HTTPS 인증서·API 연결·미인증 대화 접근 차단 검사도 통과했습니다. 설치 파일 472개의 해시와 기존 사용자 설정 보존을 확인했습니다. 직원 두 명 사이의 송수신과 실제 외부 단말의 계정 인증·송수신은 추가 현장 확인 항목입니다. [v1.0.0 릴리스 노트](docs/RELEASE-v1.0.0.md)

## 빌드

.NET 10 SDK가 설치된 Windows에서 실행합니다.

```powershell
dotnet build desktop/TempChain.Chat/TempChain.Chat.csproj --configuration Release
dotnet run --project desktop/TempChain.Chat.Tests/TempChain.Chat.Tests.csproj --configuration Release
```

게시 및 설치 파일 생성 방법은 [Windows 앱 안내](desktop/README.md)를 따릅니다.

## 운영 참고

직원 계정은 DSM의 기존 로컬 계정을 사용합니다. NAS 권한·접속 경로·내부 API 호환성은 [관리자 설정 안내](docs/SETUP.md)와 [Windows 앱 안내](desktop/README.md)에 있습니다. DSM·Chat 업데이트 후에는 로그인·대화·송수신 호환성을 확인합니다.

## 선택적 ERP 알림 연동

`bot/`은 ERP·모니터링 알림을 Chat에 보내는 별도 FastAPI 서비스입니다. Windows 메신저 실행에는 필요하지 않습니다. 사용하려면 `bot/.env.example`을 참고해 필요한 웹훅과 토큰을 설정한 뒤 `Start-IntegrationServer.ps1`을 실행합니다. 웹훅 비밀값은 아직 발급·설정하지 않았습니다.

NAS 권한과 알림 서버 운영 참고는 [설정 안내](docs/SETUP.md)에 있습니다.
