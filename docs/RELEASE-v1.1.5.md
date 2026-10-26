# TChat v1.1.5

- 사용자 목록 위에는 이름, 아래에는 이메일 표시
- NAS 사용자 프로필의 `user_props.email` 연결 및 빈 이름 필드의 대체 이름 처리
- 이메일이 없는 계정은 `이메일 미등록` 표시
- 이름과 이메일로 사용자 검색, 긴 이메일은 말줄임 및 툴팁 제공
- 설치 파일을 `TChat-Setup-1.1.5.exe` 하나로 통일하고 업데이트 조회도 TChat 파일명 사용

## 다운로드

- [TChat 설치 EXE](https://github.com/SCOTT0318/tempchain-chat/releases/download/v1.1.5/TChat-Setup-1.1.5.exe)
- [TChat ZIP](https://github.com/SCOTT0318/tempchain-chat/releases/download/v1.1.5/TChat-1.1.5-win-x64.zip)
- [SHA-256](https://github.com/SCOTT0318/tempchain-chat/releases/download/v1.1.5/SHA256SUMS.txt)

이번 릴리스는 TempChainChat 이름의 중복 설치 파일을 배포하지 않습니다. 1.1.4 이하 앱은 구형 설치 파일명을 찾으므로 위 설치 파일을 직접 내려받아 업데이트해야 합니다. 1.1.5부터는 TChat 설치 파일로 자동 업데이트를 조회합니다. 설치 경로와 내부 실행 파일명은 유지합니다.

## 검증

Windows Release 빌드와 회귀 테스트 96개가 통과했습니다. 설치기는 별도 폴더에 추출하여 게시 파일과 개수 및 SHA-256을 비교합니다. 현재 PC의 설치본은 변경하지 않습니다. 실제 NAS 계정의 이름·이메일 표시는 현장 확인이 필요합니다.
