# TChat

TEMPCHAIN 사내 Windows 메신저입니다.

[설치 EXE](https://github.com/SCOTT0318/tempchain-chat/releases/download/v1.1.6/TChat-Setup-1.1.6.exe) · [ZIP](https://github.com/SCOTT0318/tempchain-chat/releases/download/v1.1.6/TChat-1.1.6-win-x64.zip) · [릴리스](https://github.com/SCOTT0318/tempchain-chat/releases/tag/v1.1.6)

## 1.1.6 변경

- 사용자 목록에 이름과 이메일 표시
- 이름·이메일로 사용자 검색
- TChat 설치 파일 하나로 배포
- 업데이트 배포 파일·연결·저장 오류를 구분하고 실패해도 최신 버전 표시

## 사용

Windows 11 x64에서 설치 EXE를 실행한 뒤 기존 NAS 계정으로 로그인합니다. .NET 실행 환경이 포함됩니다. ZIP은 전체를 압축 해제해 실행하세요.

설치할 때 바탕화면·시작 메뉴 바로가기를 선택합니다. 설정에서는 없는 바로가기를 추가할 수 있습니다.

알림은 **3·4·5초**, **보낸 사람 보이기·가리기**를 선택합니다. 메시지 본문은 알림에 표시하지 않습니다.

## 업데이트

설정에서 현재·최신 버전을 확인합니다. 자동 확인은 시작 시와 6시간마다 실행하며, 다운로드 후 **업데이트 및 재시작**으로 적용합니다.

1.1.4 이하 앱은 위 설치 파일을 직접 내려받아 업데이트해야 합니다. 1.1.5부터는 TChat 설치 파일로 자동 업데이트를 조회합니다. 설치 경로와 내부 실행 파일명은 유지합니다.

## 개발

.NET 10 SDK와 Windows에서 실행합니다.

```powershell
dotnet build desktop/TempChain.Chat/TempChain.Chat.csproj -c Release
dotnet run --project desktop/TempChain.Chat.Tests/TempChain.Chat.Tests.csproj -c Release
./desktop/scripts/Build-Desktop.ps1 -Version 1.1.6
```

[Windows 앱·개발 안내](desktop/README.md) · [NAS 설정](docs/SETUP.md) · [1.1.6 릴리스 노트](docs/RELEASE-v1.1.6.md)

텍스트 대화·직원 목록·채팅방 관리·읽음 처리·트레이를 지원합니다. 암호화 대화·파일 전송·스레드는 미지원이며, 새 메시지는 3초 간격으로 조회합니다. 앱과 설치기는 코드 서명되지 않았습니다.