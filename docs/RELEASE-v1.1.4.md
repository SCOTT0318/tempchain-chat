# TChat v1.1.4

- 앱·설정·알림·설치기·바로가기 표시 이름을 **TChat**으로 변경
- 알림이 아래에서 위로 나타나도록 변경. 투명도 변화 없이 표시하고 아래로 닫힘
- 로그인·설정·알림·상태 표시의 반복 안내 문구 정리
- 기존 자동 업데이트와 사용자 설정 호환 유지

## 다운로드

- [TChat 설치 EXE](https://github.com/SCOTT0318/tempchain-chat/releases/download/v1.1.4/TChat-Setup-1.1.4.exe)
- [TChat ZIP](https://github.com/SCOTT0318/tempchain-chat/releases/download/v1.1.4/TChat-1.1.4-win-x64.zip)
- [SHA-256](https://github.com/SCOTT0318/tempchain-chat/releases/download/v1.1.4/SHA256SUMS.txt)

`TempChainChat-Setup-1.1.4.exe`는 이전 앱의 자동 업데이트용 호환 파일로, TChat 설치 파일과 내용이 같습니다. 설치 경로와 내부 실행 파일명은 유지합니다. 사용자가 이후 1.1.4를 설치·실행하면 기존 앱을 가리키는 바로가기만 TChat 이름으로 이전합니다. 없는 바로가기를 자동으로 새로 만들지는 않습니다.

## 검증

Windows 빌드와 회귀 테스트 95개, 격리된 바로가기 이전 시험 5가지가 통과했습니다. 실제 WPF 알림에서 위쪽 이동과 불투명도 100% 유지를 확인하고 로그인·설정·알림 화면을 렌더링해 확인했습니다.

요청에 따라 현재 PC의 설치본은 업데이트하지 않습니다. 배포 설치기는 별도 검증 폴더에서 추출·해시 비교하며 실제 사용자 설치 경로를 사용하지 않습니다.
