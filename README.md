# TEMPCHAIN Chat for Windows

템프체인 사내 전용 Windows 메신저입니다. 로그인, 사용자·대화 목록, 메시지 화면과 입력창을 WPF 컨트롤로 직접 구현했습니다. 제공받은 TEMPCHAIN 원본 로고와 고해상도 아이콘을 적용했습니다.

## 실행

[최신 Windows 앱 다운로드](https://github.com/SCOTT0318/tempchain-chat/releases/latest)에서 설치 EXE 또는 ZIP을 받습니다. 비공개 저장소이므로 접근 권한이 있는 GitHub 계정으로 로그인해야 합니다. 최종 v1.0.0 게시물은 현재 준비 중입니다.

- **설치 EXE:** `TempChainChat-Setup-1.0.0.exe`를 실행합니다. 현재 Windows 사용자용 프로그램과 바로가기를 만듭니다.
- **ZIP:** `TempChainChat-1.0.0-win-x64.zip` 전체를 압축 해제한 뒤 `TempChain.Chat.exe`를 실행합니다.

앱의 로그인 화면에서 기존 NAS 사용자 이름·비밀번호와 필요한 일회용 인증 코드를 입력합니다. 직원이 서버 주소를 입력하거나 변경할 필요가 없으며, 로그인·메신저·설정 화면에 주소를 표시하지 않습니다. 접속 경로는 회사 배포본에서 중앙 관리합니다.

Windows 11 x64를 대상으로 하며 배포본에 .NET 실행 환경을 포함합니다. 현재 설치기는 코드 서명되지 않았습니다.

## 네이티브 기능

- 사용자 이름·비밀번호·일회용 인증 코드를 사용하는 로그인
- 기존 대화·채널과 로컬 사용자 목록 조회
- 일대일 대화 시작, 텍스트 메시지 조회·전송
- 대화 내용 주기적 갱신, 동료·대화 검색
- 표시한 대화의 읽음 처리, 메시지를 보존하는 내 대화 목록 숨기기·다시 열기
- 시스템 트레이, 창 닫기 동작 설정, Windows 자동 실행 선택
- 중복 실행 시 기존 창 복원

숨기기는 본인 목록에만 적용하며 메시지를 삭제하거나 다른 참여자의 대화를 제거하지 않습니다. 첫 버전은 텍스트 대화에 집중합니다. 암호화된 대화의 해독·전송, 파일 업로드·다운로드, 스레드·투표 및 Socket.IO 기반 실시간 이벤트는 지원 범위에 포함하지 않습니다. 새 메시지는 3초 간격 조회로 반영합니다.

## 확인 상태

네이티브 자동 테스트 41개가 통과했고, 실제 NAS 인증과 직원 24명 조회를 확인했습니다. 최종 게시물과 외부 접속 경로는 준비·검증 중이며, 외부 접속 완료 또는 직원 두 명 사이의 송수신 완료를 의미하지 않습니다. [v1.0.0 릴리스 노트 초안](docs/RELEASE-v1.0.0.md)

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
