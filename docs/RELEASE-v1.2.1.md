# TChat v1.2.1

- 앱 잠금 암호 설정 화면의 ‘다음 실행부터 NAS 로그인 없이’ 안내 제거
- 잠금 화면의 ‘NAS 계정으로 다시 로그인’ 버튼 제거
- 숫자 암호는 4자리 또는 6자리 허용
- 문자를 포함하는 암호는 6자 이상 허용(최대 128자)
- 기존에 설정한 잠금 암호와 저장된 로그인, 알림 시간 및 자동 잠금 설정 유지

NAS 계정 정보가 변경된 경우에는 잠금 암호 확인 후 계정 로그인 화면으로 이동합니다.

## 다운로드

- [TChat 설치 EXE](https://github.com/SCOTT0318/tempchain-chat/releases/download/v1.2.1/TChat-Setup-1.2.1.exe)
- [TChat ZIP](https://github.com/SCOTT0318/tempchain-chat/releases/download/v1.2.1/TChat-1.2.1-win-x64.zip)
- [SHA-256](https://github.com/SCOTT0318/tempchain-chat/releases/download/v1.2.1/SHA256SUMS.txt)

## 검증

코어 회귀 테스트 103개와 WPF 화면 검증을 수행했습니다. 숫자 4·6자리 및 문자 포함 6자 이상 허용, 잘못된 길이 거부, 4자리 암호 설정·잠금 해제, 설정 안내 및 재로그인 버튼 제거를 확인했습니다.
