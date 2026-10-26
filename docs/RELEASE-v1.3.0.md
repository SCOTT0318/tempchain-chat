# TChat v1.3.0

- ERP 그룹에 포함된 계정만 TChat 로그인과 API 사용 허용
- ERP 그룹 제외 시 기존 세션의 접근 차단, 앱의 저장 로그인 해제 및 사용자 목록 자동 갱신
- ERP 계정 설명의 `이름 | 직급`을 `이름 직급`으로 표시하고 아래에 이메일 표시
- 오래된 Chat 프로필 대신 ERP 원본 프로필을 1분 간격으로 반영
- 잠금 화면 로고 가운데 배치, ‘TChat 잠금’ 제목 제거
- 기존 외부 HTTPS 주소 유지
- 업데이트 후 로그인·잠금 암호·알림 표시 시간·자동 잠금 설정 보존

그룹 변경은 다음 1분 주기 동기화 후 적용되며, 앱 연락처는 30초 간격으로 갱신합니다. 계정 조회 장애에는 저장 로그인을 지우지 않고 잠금 상태로 전환합니다. ERP 그룹에 속해 있어도 설명에 이름·직급이 등록되지 않은 계정은 기존 계정 이름을 표시합니다.

## 다운로드

- [TChat 설치 EXE](https://github.com/SCOTT0318/tempchain-chat/releases/download/v1.3.0/TChat-Setup-1.3.0.exe)
- [TChat ZIP](https://github.com/SCOTT0318/tempchain-chat/releases/download/v1.3.0/TChat-1.3.0-win-x64.zip)
- [SHA-256](https://github.com/SCOTT0318/tempchain-chat/releases/download/v1.3.0/SHA256SUMS.txt)

## 검증

코어 회귀 테스트 107개, WPF 화면·잠금·접근 제한 검증, 서버 접근 검사 테스트 8개를 통과했습니다. 공개 HTTPS 경로에서 실제 앱 로그인과 ERP 원본 이름·이메일 조회를 확인했습니다. 실제 직원의 그룹을 바꾸는 검증은 수행하지 않았으며, 그룹 제외·기존 세션 차단은 격리 테스트로 검증했습니다.
