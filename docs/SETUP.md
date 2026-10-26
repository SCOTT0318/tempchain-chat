# TempChain Chat의 NAS 설정 및 선택 연동 안내

> 2026-10-26 TChat 운영 변경: 공개 TChat 프록시의 대상은 `http://127.0.0.1:8091`이며, ERP 그룹 검사와 원본 프로필 동기화를 적용했습니다. 아래의 users 그룹 허용·3000 직접 연결 내용은 초기 구축 이력입니다. 현재 TChat 구성은 [ERP 접근 검사 운영 안내](../server/README.md)를 우선합니다. 외부 주소 `https://chat.tempchain.myds.me`와 기존 ERP 프록시는 유지합니다.

Windows 직원은 이 저장소의 자체 WPF 앱 **TempChain Chat**을 사용합니다. 설치·실행·설정은 [Windows 앱 안내](../desktop/README.md)를 따릅니다. 이 문서는 운영 담당자를 위한 NAS 권한·접속 경로와 선택 알림 연동 안내입니다. 서버 주소는 직원 화면에 표시하거나 직원별로 설정하지 않습니다.

## 1. 기존 환경과 확인 상태

| 항목 | 확인된 내용 | 남은 확인 |
| --- | --- | --- |
| NAS | 내부 IP `192.168.0.7`, DS925+, DSM 7.4.1-90080 Update 1, 메모리 4 GB | 선택 연동 서버를 NAS에 배포할 경우 컨테이너 실행 가능 여부 |
| DSM 관리자 접속 | HTTP `3000` / HTTPS `3300` | 내부 IP 직접 접속 시 인증서·접속 경로 |
| Chat 관리 콘솔 | 열림, 로컬 사용자 선택, 사용자 자동 추가 선택, 게스트 해제 | 일반 직원 로그인 및 송수신 |
| 로컬 계정 | 26개, 기본 admin·guest 비활성화, 네이티브 앱에서 직원 24명 조회 | 직원 계정별 실제 사용 확인 |
| Chat 앱 권한 | 사용자 확인 후 users 그룹 허용 저장, 재열람 시 허용 선택·거부 해제 확인, administrators 그룹 보존 | 일반 직원 계정의 실제 로그인 검증 |
| Chat 로그인 포털 | 별칭·도메인·사용자 지정 포트 모두 비어 있음 | 별도 주소를 도입하는 경우 DNS·인증서 검증 |
| Windows 앱 | WPF 로그인 화면에서 실제 NAS 인증과 직원 24명 조회 확인 | 최종 배포물 및 직원 간 송수신 확인 |
| 외부 접속 경로 | 전용 HTTPS 프록시를 사용자 승인 후 저장·재열람, 공인 DNS·인증서 및 로컬 클라이언트의 공인 주소 경유 API HTTP 200 JSON 확인 | 외부 네트워크의 실제 계정 인증·송수신 검증 |
| 공식 웹 Chat | DSM 앱 실행으로 Chat 화면 열림 확인 | 새 접속 경로의 웹·모바일 보조 접속 시 직원 계정 확인 |
| 기존 서비스 | ERP의 `https://tempchain.myds.me:443` → `http://192.168.0.141:8080` 및 `moa-erp-intro` 프록시 보존, ERP HTTP 200·기존 theme 표식 유지 | 기존 로그인·공지 등 주요 기능 재확인 |
| 선택 연동 서버 | JSON API로 구성, 로컬 .env에 참고 Chat 주소 입력, 웹훅 비밀값 미설정 | 실제 배포 및 웹훅 전달 |

위 내용은 2026-10-23 실제 DSM 화면과 네이티브 앱에서 확인한 상태입니다. 저장한 앱 권한은 재열람해 확인했습니다. 계정 한 개의 인증 성공과 직원 두 명의 실제 송수신 검증은 별개입니다. DSM 기본 HTTPS 포트로 알려진 `5001`은 이 NAS에서 확인하지 않았으므로 접속 주소로 가정하지 않습니다.

## 2. 모든 DSM 로컬 사용자에게 Chat 사용 허용

현재 users 그룹의 허용 적용 및 저장 확인은 완료했습니다. 아래는 재설정 또는 신규 환경 구축 시 참고하는 절차입니다.

1. 관리자 계정으로 DSM에 로그인합니다.
2. **제어판 → 응용 프로그램 권한 → Synology Chat Server → 편집**으로 이동합니다. DSM 버전에 따라 **사용자 및 그룹 → 그룹 → users → 편집 → 응용 프로그램**에서도 확인할 수 있습니다.
3. 로컬 그룹 **users**의 Chat 사용을 허용합니다. DSM 로컬 사용자는 기본적으로 이 그룹에 속하므로 신규 계정에도 그룹 정책을 적용할 수 있습니다.
4. 사용자별 또는 다른 소속 그룹의 **거부**가 충돌하는지 확인합니다. 거부는 허용보다 우선합니다. **권한 뷰어**에서 일반 직원 계정의 최종 Chat 권한을 확인합니다.
5. **Synology Chat 관리 콘솔 → 설정 → 일반**에서 **계정 유형: 로컬 사용자**, **사용자 자동 추가**를 확인하고 필요한 경우 저장합니다.
6. 일반 직원 계정으로 실제 Chat에 로그인합니다. Chat 관리 콘솔 권한을 직원에게 부여할 필요는 없습니다.

사용자 자동 추가는 **응용 프로그램 권한을 받은 사용자를 Chat에 등록하는 기능**입니다. 앱 권한을 대신 부여하지 않습니다. 자동 추가가 꺼져 있으면 첫 로그인 때 추가됩니다. [공식 관리 콘솔 설명](https://kb.synology.com/index.php/en-us/DSM/help/Chat/chat_admin_console?version=7), [DSM 그룹 규격](https://www.synology.com/en-us/dsm/7.3/software_spec/dsm), [거부 우선 규칙](https://kb.synology.com/en-in/DSM/tutorial/you_are_not_authorized_to_use_service_all_users)

## 3. 앱 서버 주소와 보조 접속 경로

확정한 HTTPS 기준 주소는 다음과 같습니다. 앱의 접속 경로는 `desktop/TempChain.Chat/ServerConfiguration.cs`에서 배포본별로 중앙 관리하며, 직원용 설정 파일과 로그인·메신저·설정 화면에 주소 입력·표시 기능이 없습니다.

```text
https://chat.tempchain.myds.me
```

아래 전용 역방향 프록시는 사용자 승인 후 저장했고 재열람으로 확인했습니다.

| 항목 | 확정 구성 및 확인 상태 |
| --- | --- |
| 전용 HTTPS 진입점 | `https://chat.tempchain.myds.me:443` |
| NAS 역방향 프록시 대상 | `http://127.0.0.1:3000` |
| 저장 상태 | DSM 목록에서 Chat·기존 ERP·moa-erp-intro 규칙 3개 확인 |
| 확인 결과 | 공인 DNS·HTTPS 인증서 검증, 로컬 클라이언트에서 공인 주소를 경유한 Chat·인증 API discovery HTTP 200 JSON |
| 남은 확인 | 사외 네트워크의 실제 계정 인증·직원 조회·메시지 송수신 |

기존 Synology DDNS의 하위 호스트를 사용하며 별도 도메인 구매·신규 DDNS 등록·인증서 발급은 하지 않았습니다. 수동 GitHub Actions 워크플로 `.github/workflows/external-connectivity.yml`을 사외 실행 서버에서 실행하여 유효한 HTTPS 인증서, 네이티브 앱과 같은 POST 방식의 API discovery·필요 버전, 미인증 대화 접근 차단을 확인했습니다. [외부 검사 결과](https://github.com/SCOTT0318/tempchain-chat/actions/runs/39919757052). 계정 인증과 메시지 송수신은 별도 검증 단계이며, 최신 설치본의 사내 공인 주소 경유 로그인·본인 대화 전송·재조회는 확인했습니다.

공식 웹 Chat의 보조 접속은 DSM 메인 메뉴에서 **Synology Chat**을 실행해 확인합니다. 선택 연동 서버의 `bot/.env`에 있는 `SYNO_CHAT_URL`은 검증한 웹 Chat 참고 주소를 `/api/status`로 제공하는 값이며, 네이티브 앱의 서버 설정과 로그인에는 사용하지 않습니다. 통합 토큰이 포함된 웹훅 URL을 이 값에 넣지 않습니다.

1. DSM 메인 메뉴에서 관리 콘솔이 아닌 **Synology Chat**을 실행합니다.
2. 열린 Chat 주소를 기록하고 일반 직원 계정으로 로그인합니다.
3. 두 직원 계정에서 메시지 송신과 수신을 각각 확인합니다.
4. 관리자 콘솔의 `launchApp=SYNO.SDS.Chat.Admin.Application` 주소를 직원용 접속 경로로 안내하지 않습니다.

현재 기본 구성에는 새 별칭이 필요하지 않습니다. 이후 별도 내부 호스트명 또는 **제어판 → 로그인 포털 → 응용 프로그램**의 공식 Chat 별칭을 사용할 계획이라면 DNS·인증서·실시간 메시지·PC/모바일 로그인을 검증한 후 배포합니다. 이 안내는 특정 새 호스트명이나 `/chat` 경로의 동작을 보장하지 않습니다.

**기존 ERP 보존:** `tempchain.myds.me`의 `/` 및 기존 ERP 경로를 Chat으로 돌리지 않습니다. 기존 역방향 프록시 규칙과 인증서를 덮어쓰지 않습니다. 새 규칙이 필요하면 별도의 검증된 호스트로 추가하고 작업 전후 ERP 공지·로그인 화면을 확인합니다. 기존 주소의 철자는 `myds.me`이며 `mysd.me`와 구별합니다.

이번 저장 후에도 ERP·moa-erp-intro 프록시 규칙을 보존했고, 기존 ERP의 HTTP 200 응답과 theme 표식 유지를 확인했습니다.

## 4. 직원 접속

- **Windows 11 x64:** 자체 **TempChain Chat**의 최종 설치기 또는 ZIP을 배포합니다. ZIP은 전체 폴더를 압축 해제한 뒤 `TempChain.Chat.exe`를 실행합니다. .NET 실행 환경이 포함되며 WebView2를 사용하지 않습니다. [Windows 앱 안내](../desktop/README.md)
- **웹 보조 접속:** 확인한 공식 Chat 주소로 접속하고 기존 NAS 사용자 이름과 비밀번호로 로그인합니다.
- **macOS / Linux:** 필요한 경우 [Synology 다운로드 센터](https://www.synology.com/ko-kr/support/download)의 데스크톱 유틸리티에서 **Synology Chat Client**를 사용합니다. 서버 주소는 해당 단말에서 실제 접속을 검증한 값을 안내합니다. [공식 설치 절차](https://kb.synology.com/ko-kr/DSM/help/ChatClient/chatclient)
- **Android:** Synology Inc.가 배포한 [Google Play 앱](https://play.google.com/store/apps/details?id=com.synology.dschat)을 사용합니다.
- **iPhone / iPad:** Synology Inc.가 배포한 [App Store 앱](https://apps.apple.com/kr/app/synology-chat/id1121875041)을 사용합니다.

Windows 앱은 WPF 로그인 화면에서 사용자 이름과 비밀번호를 입력받아 NAS에 직접 인증합니다. 비밀번호는 설정 파일에 저장하지 않으며 SID는 메모리에서만 유지합니다. 모바일 알림은 사무실 Wi-Fi와 실제 원격 접속 조건에서 각각 테스트하고, VPN·푸시 도달 여부를 결과로 남깁니다.

## 5. 선택: ERP 알림과 봇 연동

Windows 메신저를 사용하는 데 웹훅이나 이 FastAPI 서버는 필요하지 않습니다. ERP 알림 등 필요한 기능이 생길 때만 연결합니다. 이 서버는 JSON API이며 직원용 채팅 화면을 제공하지 않습니다.

| 환경 설정 | 용도 |
| --- | --- |
| `SYNO_CHAT_URL` | `/api/status`에 제공할 선택적인 공식 웹 Chat 참고 주소 |
| `SYNO_INCOMING_WEBHOOK_URL` | 연동 서버가 지정 채널에 메시지를 보내는 URL |
| `SYNO_OUTGOING_TOKEN` | 일반 발신 웹훅 요청 검증 |
| `NOTIFY_TOKEN` | 사내 시스템의 `/notify` 호출 검증 |
| `SYNO_ACTION_TOKEN` | 봇 버튼 이벤트 요청 검증 |

정확한 변수명과 추가 설정은 [환경 설정 예시](../bot/.env.example)를 확인합니다. `.env`와 웹훅 URL은 비밀값이며 Git에 추가하지 않습니다.

### Incoming Webhook

Chat → 프로필 → **통합 → 수신 Webhook**에서 대상 채널을 지정하고 발급 URL을 `SYNO_INCOMING_WEBHOOK_URL`에 넣습니다. 공식 요청은 POST의 `payload` 매개변수에 JSON 문자열을 넣거나 JSON 본문을 사용합니다. 이 서버가 보내는 성공 응답만으로 직원 단말 수신을 완료했다고 판단하지 말고 실제 대상 채널에서 확인합니다.

### Outgoing Webhook 및 봇 버튼

일반 발신 웹훅은 서버에서 접근 가능한 `/syno/outgoing` URL을 등록합니다. 채널 또는 트리거 단어가 필요하며 둘 다 지정하면 두 조건을 모두 만족해야 실행됩니다.

봇의 버튼 이벤트는 **봇에 등록한 동일한 outgoing URL**로 전달됩니다. 이 저장소의 `/syno/outgoing`은 일반 메시지와 버튼 이벤트를 분기 처리합니다. 같은 봇의 일반 메시지를 받으려면 발급 봇 토큰을 `SYNO_OUTGOING_TOKEN`에, 버튼도 받으려면 같은 토큰을 `SYNO_ACTION_TOKEN`에도 설정합니다. `/syno/action`은 버튼 이벤트만 처리하는 별칭입니다. 버튼 데이터는 JSON `payload` 안의 `token`, `actions`, `callback_id`, `post_id`, `user`를 포함합니다. 버튼 응답은 기존 게시물을 갱신할 수 있습니다. 이 서버는 버튼 수신만 확인하며 승인·배포 같은 업무 작업을 실행하지 않습니다.

공식 연동의 상세 지원 범위는 [Chat 통합 도움말](https://kb.synology.com/en-global/DSM/help/Chat/chat_integration?version=7)을 확인합니다. 봇 조회 API는 봇에게 보이는 대화·사용자·메시지 범위입니다. 전체 사용자용 커스텀 클라이언트 API로 가정하지 않습니다.

## 6. 연동 서버 실행 및 상태 해석

프로젝트 루트에서 필요한 의존성을 설치하고 로컬 JSON API를 실행합니다. 실행 스크립트는 `conda scott` 환경의 Python을 사용하며, 설정 파일이 없으면 예시에서 `bot/.env`를 만듭니다.

```powershell
conda run -n scott python -m pip install -r bot/requirements.txt
./Start-IntegrationServer.ps1
```

기본 주소는 `http://127.0.0.1:8787`입니다. 이 스크립트는 로컬에서만 수신하므로 다른 호스트의 NAS에서는 웹훅을 호출할 수 없습니다. 사내 네트워크에서 운영하려면 실제 서버 주소와 접근 범위를 정해 별도 실행 구성을 검증합니다.

Docker를 선택할 경우 프로젝트 루트의 Compose 구성을 사용합니다. Docker 빌드·실행은 아직 확인하지 않았습니다.

```powershell
docker compose up -d --build
```

기본 바인딩은 `127.0.0.1:8787`입니다. 사내 연결이 필요하면 실행 셸의 `LAN_BIND_IP`를 서버의 실제 내부 IP로 지정하고 허용한 네트워크에서 테스트합니다.

Compose는 `bot/.env`를 컨테이너 환경 설정으로 전달합니다. `LAN_BIND_IP`와 `PORT` 같은 Compose 치환 변수는 실행 셸 또는 루트 `.env`에 설정합니다. `bot/.env`에만 `LAN_BIND_IP`를 적어도 공개 주소는 바뀌지 않습니다.

| 확인 항목 | 의미 | 의미하지 않는 것 |
| --- | --- | --- |
| `/healthz` 응답 | FastAPI 프로세스가 응답함 | NAS 또는 Chat의 정상 상태 |
| `/api/status` 응답 | 설정값 존재 여부를 조회함 | 로그인 성공·네트워크 연결·전달 성공 |
| `/`의 JSON 응답 | 알림 연동 서비스가 응답함 | 직원용 채팅 화면 또는 NAS 연결 확인 |

운영자는 `/api/status`의 `incoming_configured`, `outgoing_configured`, `notify_configured`, `action_configured`로 설정 존재 여부만 확인할 수 있습니다. 이 응답은 NAS에 상태 확인 요청을 보내지 않습니다. 네이티브 메신저는 이 연동 서버에 의존하지 않습니다.

필요한 경우 DSM의 공식 API discovery 요청으로 지원 API 정보를 읽을 수 있습니다.

```text
GET https://<확인한-NAS-주소>/webapi/entry.cgi?api=SYNO.API.Info&version=1&method=query&query=SYNO.API.Auth
```

이 요청도 실제 메시지 전달 검증을 대신하지 않습니다. 인증 점검은 [공식 DSM 로그인 API 안내](https://kb.synology.com/en-global/DG/DSM_Login_Web_API_Guide/3)에 따라 지원 경로·버전을 먼저 확인하고 기존 MFA를 유지합니다. 비밀번호를 셸 명령 기록이나 URL에 남기지 않습니다.

## 7. 배포 완료 조건

- [x] users 그룹의 Chat 앱 허용 저장 및 재열람 확인
- [x] 기존 관리자 세션에서 공식 Chat 실행 화면 확인
- [x] 자체 Windows 앱의 실제 NAS 인증 및 직원 24명 조회 확인
- [x] 전용 HTTPS 프록시 사용자 승인·저장·재열람 및 공인 DNS·인증서 확인
- [x] 로컬 클라이언트에서 공인 주소 경유 Chat·인증 API discovery HTTP 200 JSON 확인
- [x] 기존 ERP·moa-erp-intro 규칙 및 ERP HTTP 200·theme 표식 보존 확인
- [ ] 외부 runner의 공개 경로 확인 및 사외 네트워크의 실제 계정 인증·송수신 확인
- [ ] 일반 로컬 사용자 두 명의 Chat 로그인 확인
- [ ] Windows 앱에서 두 직원의 텍스트 송수신·읽음·숨기기 및 기록 보존 확인
- [ ] 파일 기능이 필요한 경우 공식 웹·모바일 Chat에서 업로드·다운로드 확인
- [ ] 새 로컬 계정의 앱 권한 상속과 Chat 자동 추가 확인
- [ ] 직원용 접속 주소, 인증서 및 PC/모바일 로그인 확인
- [ ] 실제 사용 환경에서 알림 수신 확인
- [ ] 사용하는 연동만 정상 토큰·잘못된 토큰·전달 결과 확인
- [ ] 기존 ERP 로그인·공지 등 대표 화면 보존 확인
- [ ] NAS 모델의 동시 사용자 수, 보존 기간, 백업·복구 절차 확인

Chat은 일반 그룹 대화 최대 9명, 서버당 봇 최대 5개를 지원합니다. 더 큰 대화는 채널을 사용합니다. 암호화된 대화는 관리자가 조회할 수 없으므로 실제 사내 정책에 따라 적용합니다. [공식 Chat 기술 규격](https://www.synology.com/ko-kr/dsm/7.4/software_spec/chat)

현재의 ChatPlus 베타는 평가용이며 운영 환경에 설치하지 말라는 공식 안내가 있습니다. 이 구성은 일반 Synology Chat을 기준으로 합니다. [공식 ChatPlus 베타 안내](https://www.synology.com/en-us/beta/2026_Synology_ChatPlus_Server_1_0_0_Beta/release_note)

## 8. 구현 검증 기록

선택 연동 서버의 FastAPI 자동 테스트 45개와 네이티브 Windows 앱의 자동 테스트 41개가 통과했습니다. 최신 설치본의 실제 NAS 로그인, 직원 24명 조회와 본인 대화 전송·재조회를 확인했습니다. 확정한 전용 HTTPS 프록시 저장·재열람, 사외 GitHub 실행 서버의 TLS·API·미인증 접근 차단 및 기존 ERP HTTP 200·theme 표식 보존을 검증했습니다. 설치한 472개 파일의 해시 일치와 기존 설정 보존도 확인했습니다. 사외 단말의 실제 계정 인증·송수신, Docker 빌드·구동, 직원 두 명 사이 송수신 및 공식 클라이언트의 파일 전송은 추가 현장 검증 항목입니다.
