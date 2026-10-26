# TChat v1.1.6

- TChat 설치 파일을 찾는 업데이트 방식 유지
- 배포 파일·체크섬 오류를 인터넷 연결 오류와 구분하여 안내
- 업데이트 파일 확인에 실패해도 조회한 최신 버전 표시
- 연결 실패, 응답 형식 오류, 저장 권한 및 디스크 오류를 구분
- 사용자 목록의 이름·이메일 표시 및 검색 포함

## 다운로드

- [TChat 설치 EXE](https://github.com/SCOTT0318/tempchain-chat/releases/download/v1.1.6/TChat-Setup-1.1.6.exe)
- [TChat ZIP](https://github.com/SCOTT0318/tempchain-chat/releases/download/v1.1.6/TChat-1.1.6-win-x64.zip)
- [SHA-256](https://github.com/SCOTT0318/tempchain-chat/releases/download/v1.1.6/SHA256SUMS.txt)

1.1.4 이하 앱은 구형 설치 파일명을 찾으므로 위 설치 파일을 직접 내려받아 업데이트해야 합니다. 1.1.5부터는 TChat 설치 파일로 자동 업데이트를 조회합니다. 설치 경로와 사용자 설정을 유지하며 TempChainChat 이름의 중복 설치 파일은 배포하지 않습니다.

## 검증

기존 1.1.4 설치본에 실제 GitHub 최신 릴리스 응답을 입력하여 설치 파일명 불일치 오류를 재현했습니다. 업데이트 오류 구분과 최신 버전 유지 회귀 검증을 추가했습니다.
